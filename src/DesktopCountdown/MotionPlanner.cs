using System;
using System.Collections.Generic;
using System.Windows;

namespace DesktopCountdown
{
    public static class MotionPlanner
    {
        public const string NineGrid = SettingsDefaults.NineGrid;
        public const string Orbit = SettingsDefaults.Orbit;
        public const string Wander = SettingsDefaults.Wander;
        public const string Tide = SettingsDefaults.Tide;
        public const string EdgeWalk = SettingsDefaults.EdgeWalk;
        public const string FarNear = SettingsDefaults.FarNear;

        private static readonly Point[] Grid = {
            new Point(-1,-1), new Point(1,0), new Point(-1,1), new Point(0,-1),
            new Point(1,1), new Point(-1,0), new Point(0,1), new Point(1,-1), new Point(0,0)
        };
        private static readonly Point[] Ring = {
            new Point(1,0), new Point(0.7,0.7), new Point(0,1), new Point(-0.7,0.7),
            new Point(-1,0), new Point(-0.7,-0.7), new Point(0,-1), new Point(0.7,-0.7)
        };
        private static readonly Point[] Far = {
            new Point(-4,-2), new Point(4,2), new Point(-3,3), new Point(3,-3),
            new Point(0,0)
        };

        public static string[] Modes = { NineGrid, Orbit, Wander, Tide, EdgeWalk, FarNear };
        public static string[] Names = { "九宫漫游", "星轨", "随机漫步", "潮汐", "沿边散步", "远近切换" };
        private static readonly IReadOnlyList<string> SupportedModes = Array.AsReadOnly(
            new[] { NineGrid, Orbit, Wander, Tide, EdgeWalk, FarNear });
        private static readonly IReadOnlyList<string> SupportedNames = Array.AsReadOnly(
            new[] { "九宫漫游", "星轨", "随机漫步", "潮汐", "沿边散步", "远近切换" });

        internal static int ModeCount { get { return SupportedModes.Count; } }
        internal static string ModeAt(int index) { return SupportedModes[index]; }
        internal static string NameAt(int index) { return SupportedNames[index]; }
        internal static int IndexOfMode(string mode)
        {
            for (int i = 0; i < SupportedModes.Count; i++)
                if (SupportedModes[i] == mode) return i;
            return -1;
        }

        public static Point Next(string mode, int step, double amplitude, Random random, Point previous, bool atHorizontalEdge, bool atVerticalEdge)
        {
            bool supported = false;
            foreach (string value in SupportedModes)
                if (mode == value) { supported = true; break; }
            if (!supported) mode = NineGrid;
            if (mode == Wander)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    Point candidate = new Point((random.NextDouble() * 2 - 1) * amplitude,
                        (random.NextDouble() * 2 - 1) * amplitude);
                    if (Distance(candidate, previous) >= amplitude * 0.55) return candidate;
                }
                return new Point(-previous.X, -previous.Y);
            }
            Point point;
            if (mode == Orbit) point = Ring[step % Ring.Length];
            else if (mode == Tide) point = Ring[(step * 3) % Ring.Length];
            else if (mode == FarNear) point = Far[step % Far.Length];
            else if (mode == EdgeWalk)
            {
                double[] line = { -1, -0.5, 0.5, 1, 0, 0.75, -0.75 };
                double value = line[step % line.Length];
                point = atHorizontalEdge && atVerticalEdge
                    ? (step % 2 == 0 ? new Point(0, value) : new Point(value, 0))
                    : atHorizontalEdge ? new Point(0, value) : new Point(value, 0);
                if (!atHorizontalEdge && !atVerticalEdge) point = Grid[step % Grid.Length];
            }
            else point = Grid[step % Grid.Length];
            return new Point(point.X * amplitude, point.Y * amplitude);
        }

        private static double Distance(Point left, Point right)
        {
            double x = left.X - right.X;
            double y = left.Y - right.Y;
            return Math.Sqrt(x * x + y * y);
        }

        public static Point Constrain(Point anchor, Point offset, Size window, Rect work, double gutter)
        {
            double minimumX = work.Left - gutter;
            double minimumY = work.Top - gutter;
            double maximumX = Math.Max(minimumX, work.Right - window.Width + gutter);
            double maximumY = Math.Max(minimumY, work.Bottom - window.Height + gutter);
            return new Point(
                Math.Max(minimumX, Math.Min(maximumX, anchor.X + offset.X)),
                Math.Max(minimumY, Math.Min(maximumY, anchor.Y + offset.Y)));
        }

        public static Point EnsureVisibleMove(Point anchor, Point requestedOffset, Point previousOffset,
            Size window, Rect work, double gutter, double amplitude)
        {
            Point target = Constrain(anchor, requestedOffset, window, work, gutter);
            Point current = new Point(anchor.X + previousOffset.X, anchor.Y + previousOffset.Y);
            if (Distance(target, current) >= 0.75) return new Point(target.X - anchor.X, target.Y - anchor.Y);

            Point[] alternatives = {
                new Point(-amplitude, 0), new Point(amplitude, 0),
                new Point(0, -amplitude), new Point(0, amplitude),
                new Point(0, 0)
            };
            double bestDistance = 0;
            Point best = target;
            foreach (Point alternative in alternatives)
            {
                Point candidate = Constrain(anchor, alternative, window, work, gutter);
                double distance = Distance(candidate, current);
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            return new Point(best.X - anchor.X, best.Y - anchor.Y);
        }
    }
}
