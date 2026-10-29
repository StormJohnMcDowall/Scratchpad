using System;
using System.Collections.Generic;

namespace Scratchpad.Core
{
    public struct WindowBounds : IEquatable<WindowBounds>
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public bool Equals(WindowBounds other)
        {
            return Left.Equals(other.Left) && Top.Equals(other.Top)
                && Width.Equals(other.Width) && Height.Equals(other.Height);
        }

        public override bool Equals(object obj)
        {
            return obj is WindowBounds other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Left.GetHashCode();
                hash = hash * 31 + Top.GetHashCode();
                hash = hash * 31 + Width.GetHashCode();
                hash = hash * 31 + Height.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(WindowBounds a, WindowBounds b) => a.Equals(b);
        public static bool operator !=(WindowBounds a, WindowBounds b) => !a.Equals(b);
    }

    public static class WindowPlacement
    {
        public static WindowBounds Clamp(WindowBounds requested, WindowBounds screenBounds)
        {
            var left = Math.Max(screenBounds.Left, Math.Min(requested.Left, screenBounds.Left + screenBounds.Width - requested.Width));
            var top = Math.Max(screenBounds.Top, Math.Min(requested.Top, screenBounds.Top + screenBounds.Height - requested.Height));

            return new WindowBounds
            {
                Left = left,
                Top = top,
                Width = requested.Width,
                Height = requested.Height
            };
        }

        /// <summary>
        /// Clamps against the screen the requested bounds overlap most — needed because a
        /// multi-monitor virtual screen's bounding box is often not itself a real screen (e.g.
        /// monitors offset vertically), so clamping to the bounding box alone can still land a
        /// window in a gap no monitor actually covers.
        /// </summary>
        public static WindowBounds Clamp(WindowBounds requested, IReadOnlyList<WindowBounds> screens)
        {
            var best = screens.Count > 0
                ? screens[0]
                : new WindowBounds { Left = 0, Top = 0, Width = requested.Width, Height = requested.Height };
            var bestOverlap = -1.0;

            foreach (var screen in screens)
            {
                var overlap = IntersectionArea(requested, screen);
                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    best = screen;
                }
            }

            return Clamp(requested, best);
        }

        private static double IntersectionArea(WindowBounds a, WindowBounds b)
        {
            var left = Math.Max(a.Left, b.Left);
            var top = Math.Max(a.Top, b.Top);
            var right = Math.Min(a.Left + a.Width, b.Left + b.Width);
            var bottom = Math.Min(a.Top + a.Height, b.Top + b.Height);
            var width = Math.Max(0, right - left);
            var height = Math.Max(0, bottom - top);
            return width * height;
        }
    }
}
