using System;
using System.Windows;
using System.Windows.Threading;

namespace DesktopCountdown
{
    internal sealed class MotionSession
    {
        private readonly DispatcherTimer stepTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer animationTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly Random random = new Random();
        private Point anchor;
        private Point offset;
        private Point from;
        private Point to;
        private DateTime startedUtc;
        private DateTime nextAt;
        private int step;
        private int duration;
        private bool applying;

        public MotionSession()
        {
            animationTimer.Interval = TimeSpan.FromMilliseconds(33);
        }

        public DispatcherTimer StepTimer { get { return stepTimer; } }
        public DispatcherTimer AnimationTimer { get { return animationTimer; } }
        public Random Random { get { return random; } }
        public Point Anchor { get { return anchor; } }
        public Point Offset { get { return offset; } }
        public Point From { get { return from; } }
        public Point To { get { return to; } }
        public DateTime NextAt { get { return nextAt; } }
        public bool IsApplying { get { return applying; } }

        public void SetAnchor(double left, double top)
        {
            anchor = new Point(left, top);
            offset = new Point();
        }

        public void Pause()
        {
            stepTimer.Stop();
            animationTimer.Stop();
        }

        public void StopAnimation()
        {
            animationTimer.Stop();
        }

        public void Configure(bool enabled, int intervalSeconds, bool active, DateTime now)
        {
            Pause();
            nextAt = DateTime.MinValue;
            if (!active || !enabled) return;
            stepTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
            ScheduleNext(now);
            stepTimer.Start();
        }

        public void ScheduleNext(DateTime now)
        {
            nextAt = now.Add(stepTimer.Interval);
        }

        public int TakeStep()
        {
            return step++;
        }

        public void BeginMove(Point target, int milliseconds, DateTime nowUtc)
        {
            from = offset;
            to = target;
            startedUtc = nowUtc;
            duration = milliseconds;
            if (duration != 0) animationTimer.Start();
        }

        public Point Interpolate(DateTime nowUtc, out bool complete)
        {
            double fraction = Math.Min(1, (nowUtc - startedUtc).TotalMilliseconds / duration);
            double eased = fraction * fraction * (3 - 2 * fraction);
            complete = fraction >= 1;
            return new Point(from.X + (to.X - from.X) * eased,
                from.Y + (to.Y - from.Y) * eased);
        }

        public void ApplyPosition(Action apply, Func<Point> position)
        {
            applying = true;
            try { apply(); }
            finally { applying = false; }
            Point actual = position();
            offset = new Point(actual.X - anchor.X, actual.Y - anchor.Y);
        }

        public void ResetPosition(Action apply)
        {
            animationTimer.Stop();
            applying = true;
            try { apply(); }
            finally { applying = false; }
            offset = new Point();
        }
    }
}
