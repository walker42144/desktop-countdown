using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace DesktopCountdown
{
    public static class IconFactory
    {
        public static Icon CreateClockIcon()
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                Rectangle circle = new Rectangle(3, 3, 26, 26);
                using (LinearGradientBrush fill = new LinearGradientBrush(circle,
                    Color.FromArgb(255, 47, 72, 105), Color.FromArgb(255, 67, 132, 132), 45f))
                    graphics.FillEllipse(fill, circle);
                using (Pen rim = new Pen(Color.FromArgb(220, 235, 242, 244), 1.5f))
                    graphics.DrawEllipse(rim, circle);
                using (Pen hands = new Pen(Color.White, 2.2f))
                {
                    hands.StartCap = LineCap.Round;
                    hands.EndCap = LineCap.Round;
                    graphics.DrawLine(hands, 16, 16, 16, 9);
                    graphics.DrawLine(hands, 16, 16, 21, 19);
                }
                graphics.FillEllipse(Brushes.White, 14.5f, 14.5f, 3f, 3f);

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle)) return (Icon)temporary.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
