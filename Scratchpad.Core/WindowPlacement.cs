using System;

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
    }
}
