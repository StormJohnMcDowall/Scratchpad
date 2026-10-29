using System;

namespace Scratchpad.Core
{
    public static class FontSizing
    {
        public const double Default = 14;
        public const double Min = 8;
        public const double Max = 36;
        public const double Step = 2;

        public static double Clamp(double value) => Math.Max(Min, Math.Min(Max, value));

        public static double Increase(double current) => Clamp(current + Step);

        public static double Decrease(double current) => Clamp(current - Step);
    }
}
