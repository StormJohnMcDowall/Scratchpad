using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Wpf;

namespace Scratchpad
{
    /// <summary>
    /// Measures and scrolls the rendered preview. The preview's blocks are matched by position to
    /// Scratchpad.Core.PreviewSourceMap's source blocks, so GetUnits must walk the FlowDocument by
    /// the same rules: Paragraphs and Tables are units, Sections and Lists are containers.
    /// Everything here fails soft (empty list, -1, null) so callers can leave the views alone.
    /// </summary>
    internal sealed class PreviewNavigator
    {
        private readonly ScrollViewer _scrollViewer;
        private readonly MarkdownViewer _viewer;
        private Visual _renderScope;

        public PreviewNavigator(ScrollViewer scrollViewer, MarkdownViewer viewer)
        {
            _scrollViewer = scrollViewer;
            _viewer = viewer;
        }

        /// <summary>
        /// The element TextPointer.GetCharacterRect measures against: the internal
        /// FlowDocumentView inside the FlowDocumentScrollViewer template. It isn't a public type,
        /// so it's found by name, and re-found if the template has been rebuilt.
        /// </summary>
        public Visual RenderScope
        {
            get
            {
                if (_renderScope == null || !_viewer.IsAncestorOf(_renderScope))
                {
                    _renderScope = FindByTypeName(_viewer, "FlowDocumentView");
                }
                return _renderScope;
            }
        }

        public IReadOnlyList<Block> GetUnits()
        {
            var units = new List<Block>();
            if (_viewer.Document != null)
            {
                CollectUnits(_viewer.Document.Blocks, units);
            }
            return units;
        }

        /// <summary>
        /// The first unit that is at least partly visible at the top of the preview, or -1.
        /// </summary>
        public int TopUnitIndex(IReadOnlyList<Block> units)
        {
            return UnitIndexAtExtentY(units, _scrollViewer.VerticalOffset + 1);
        }

        /// <summary>
        /// The first unit whose bottom is below y (in scroll-extent coordinates), or -1 when y is
        /// below every unit or the layout can't be measured.
        /// </summary>
        public int UnitIndexAtExtentY(IReadOnlyList<Block> units, double y)
        {
            for (var i = 0; i < units.Count; i++)
            {
                var bottom = CharacterRectInExtent(units[i].ContentEnd.GetInsertionPosition(LogicalDirection.Backward));
                if (bottom == null)
                {
                    return -1;
                }
                if (bottom.Value.Bottom > y)
                {
                    return i;
                }
            }
            return -1;
        }

        public void ScrollUnitToTop(Block unit)
        {
            var top = CharacterRectInExtent(unit.ContentStart.GetInsertionPosition(LogicalDirection.Forward));
            if (top != null)
            {
                _scrollViewer.ScrollToVerticalOffset(top.Value.Top);
            }
        }

        /// <summary>
        /// Converts a point relative to the MarkdownViewer into scroll-extent coordinates. The
        /// viewer is the ScrollViewer's content, so the two are the same.
        /// </summary>
        public Point? ToExtent(Point pointInViewer)
        {
            return pointInViewer;
        }

        public Rect? CharacterRectInExtent(TextPointer pointer)
        {
            var scope = RenderScope;
            if (scope == null || pointer == null)
            {
                return null;
            }

            try
            {
                var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
                if (rect.IsEmpty)
                {
                    return null;
                }
                return scope.TransformToAncestor(_viewer).TransformBounds(rect);
            }
            catch (InvalidOperationException)
            {
                // TransformToAncestor throws when the visuals aren't connected (preview hidden
                // before layout, template mid-rebuild).
                return null;
            }
        }

        /// <summary>
        /// The character offset within the run nearest to (at or left of) the point, on the line
        /// the point is on; 0 when the run can't be measured.
        /// </summary>
        public int OffsetInRun(Run run, Point pointInExtent)
        {
            var best = 0;
            var length = run.Text.Length;
            for (var i = 0; i < length; i++)
            {
                var rect = CharacterRectInExtent(run.ContentStart.GetPositionAtOffset(i));
                if (rect == null)
                {
                    return 0;
                }
                var r = rect.Value;
                if (pointInExtent.Y >= r.Top && pointInExtent.Y <= r.Bottom && r.Left <= pointInExtent.X)
                {
                    best = i;
                }
            }
            return best;
        }

        private static void CollectUnits(BlockCollection blocks, List<Block> units)
        {
            foreach (var block in blocks)
            {
                if (block is Paragraph || block is Table)
                {
                    units.Add(block);
                }
                else if (block is Section section)
                {
                    CollectUnits(section.Blocks, units);
                }
                else if (block is List list)
                {
                    foreach (var item in list.ListItems)
                    {
                        CollectUnits(item.Blocks, units);
                    }
                }
            }
        }

        private static Visual FindByTypeName(DependencyObject root, string typeName)
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child.GetType().Name == typeName && child is Visual visual)
                {
                    return visual;
                }
                var found = FindByTypeName(child, typeName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }
    }
}
