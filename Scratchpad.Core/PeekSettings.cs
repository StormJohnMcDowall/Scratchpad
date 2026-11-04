using System;

namespace Scratchpad.Core
{
    public struct PeekSettings : IEquatable<PeekSettings>
    {
        public int Seconds { get; set; }

        public bool Equals(PeekSettings other)
        {
            return Seconds == other.Seconds;
        }

        public override bool Equals(object obj)
        {
            return obj is PeekSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Seconds;
        }

        public static bool operator ==(PeekSettings a, PeekSettings b) => a.Equals(b);
        public static bool operator !=(PeekSettings a, PeekSettings b) => !a.Equals(b);
    }
}
