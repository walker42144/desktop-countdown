using System;

namespace DesktopCountdown
{
    public static class EdgeSnapCalculator
    {
        public static double SnapAxis(double windowStart, double windowSize,
            double workStart, double workEnd, double gutter, double snapDistance)
        {
            double visibleStart = windowStart + gutter;
            double visibleEnd = windowStart + windowSize - gutter;
            double startDistance = Math.Abs(visibleStart - workStart);
            double endDistance = Math.Abs(visibleEnd - workEnd);

            if (Math.Min(startDistance, endDistance) > snapDistance) return windowStart;
            return startDistance <= endDistance
                ? workStart - gutter
                : workEnd - windowSize + gutter;
        }
    }
}
