using System;
using System.Collections.Generic;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace Scratchpad.Core
{
    /// <summary>
    /// The source character range of one block the preview draws. End is exclusive.
    /// </summary>
    public struct SourceBlock
    {
        public SourceBlock(int start, int end)
            : this(start, end, null)
        {
        }

        public SourceBlock(int start, int end, string codeLanguage)
        {
            Start = start;
            End = end;
            CodeLanguage = codeLanguage;
        }

        public int Start { get; }

        public int End { get; }

        /// <summary>
        /// A fenced code block's language tag (the first word after the backticks), which
        /// Markdig.Wpf drops when it renders; null for every other block.
        /// </summary>
        public string CodeLanguage { get; }
    }

    public static class PreviewSourceMap
    {
        private static readonly MarkdownPipeline Pipeline = NotePipeline.Create();

        /// <summary>
        /// Lists, in document order, the source range of every block the preview draws as a
        /// single WPF Paragraph or Table. The rules mirror Markdig.Wpf's renderers: paragraphs,
        /// headings, code blocks and thematic breaks each become one Paragraph; quotes and lists
        /// are containers; a table is one unit; HTML blocks and link reference definitions have
        /// no renderer and draw nothing. The Nth entry therefore pairs with the Nth such WPF block.
        /// Fenced code blocks also carry their language tag, for the preview's highlighting.
        /// </summary>
        public static IReadOnlyList<SourceBlock> Build(string markdown)
        {
            var blocks = new List<SourceBlock>();
            if (string.IsNullOrEmpty(markdown))
            {
                return blocks;
            }

            Collect(Markdown.Parse(markdown, Pipeline), blocks);
            return blocks;
        }

        /// <summary>
        /// Returns the index of the block containing the offset, or the next block when the offset
        /// falls between blocks. Past the last block, returns the last; with no blocks, -1.
        /// </summary>
        public static int BlockIndexAt(IReadOnlyList<SourceBlock> blocks, int offset)
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].End > offset)
                {
                    return i;
                }
            }

            return blocks.Count - 1;
        }

        /// <summary>
        /// Finds a rendered run of text inside the block's source and returns the source offset of
        /// the character offsetInRun into it. Text that can't be found (escaped or entity-encoded
        /// in the source, say) falls back to the start of the block.
        /// </summary>
        public static int LocateInBlock(string markdown, SourceBlock block, string runText, int offsetInRun)
        {
            if (string.IsNullOrEmpty(runText) || string.IsNullOrEmpty(markdown))
            {
                return block.Start;
            }

            var length = Math.Min(block.End, markdown.Length) - block.Start;
            if (length <= 0)
            {
                return block.Start;
            }

            var found = markdown.IndexOf(runText, block.Start, length, StringComparison.Ordinal);
            if (found < 0)
            {
                return block.Start;
            }

            return Math.Min(found + Math.Max(offsetInRun, 0), block.End);
        }

        private static void Collect(ContainerBlock container, List<SourceBlock> blocks)
        {
            foreach (var block in container)
            {
                if (block is ParagraphBlock || block is HeadingBlock || block is CodeBlock
                    || block is ThematicBreakBlock || block is Table)
                {
                    var language = block is FencedCodeBlock fenced && !string.IsNullOrWhiteSpace(fenced.Info) ? fenced.Info : null;
                    blocks.Add(new SourceBlock(block.Span.Start, block.Span.End + 1, language));
                }
                else if (block is ContainerBlock child)
                {
                    Collect(child, blocks);
                }
            }
        }
    }
}
