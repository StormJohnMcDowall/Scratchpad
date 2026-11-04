using System;

namespace Scratchpad.Core
{
    public static class PeekSlide
    {
        /// <summary>
        /// Where the note's edge is at <paramref name="progress"/> (0 to 1, clamped) of a slide.
        /// Accelerating (ease-in) suits leaving, so the note eases off then gets out of the way;
        /// decelerating (ease-out) suits arriving, so it settles into place.
        /// </summary>
        public static double Interpolate(double from, double to, double progress, bool accelerate)
        {
            var p = Math.Max(0.0, Math.Min(1.0, progress));
            var eased = accelerate ? p * p : 1 - (1 - p) * (1 - p);
            return from + (to - from) * eased;
        }
    }
}
