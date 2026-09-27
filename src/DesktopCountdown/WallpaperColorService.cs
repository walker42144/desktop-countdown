using Microsoft.Win32;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopCountdown
{
    public sealed class WallpaperAppearance
    {
        public System.Windows.Media.Color Foreground { get; set; }
        public System.Windows.Media.Color Shadow { get; set; }
        public bool UseBackdrop { get; set; }
        public double Luminance { get; set; }
        public string Description { get; set; }
    }

    public static class WallpaperColorService
    {
        private const int SpiGetDesktopWallpaper = 0x0073;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(int action, int parameter, StringBuilder value, int flags);

        public static WallpaperAppearance Analyze(Rectangle widgetBounds)
        {
            try
            {
                string path = GetWallpaperPath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return DefaultAppearance("未找到静态壁纸");

                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromRectangle(widgetBounds);
                Rectangle destination = screen.Bounds;
                string style = ReadDesktopValue("WallpaperStyle", "10");
                bool tile = ReadDesktopValue("TileWallpaper", "0") == "1";

                if (style == "22") destination = System.Windows.Forms.SystemInformation.VirtualScreen;

                using (Bitmap bitmap = new Bitmap(path))
                {
                    return AnalyzeBitmap(bitmap, widgetBounds, destination, style, tile, Path.GetFileName(path));
                }
            }
            catch (Exception ex)
            {
                return DefaultAppearance("壁纸分析失败：" + ex.Message);
            }
        }

        private static WallpaperAppearance AnalyzeBitmap(Bitmap bitmap, Rectangle widget, Rectangle destination,
            string style, bool tile, string fileName)
        {
            double sum = 0;
            double sumSquares = 0;
            int count = 0;
            int samplesX = 18;
            int samplesY = 10;

            for (int yIndex = 0; yIndex < samplesY; yIndex++)
            {
                int screenY = widget.Top + (int)((yIndex + 0.5) * Math.Max(1, widget.Height) / samplesY);
                for (int xIndex = 0; xIndex < samplesX; xIndex++)
                {
                    int screenX = widget.Left + (int)((xIndex + 0.5) * Math.Max(1, widget.Width) / samplesX);
                    Point source;
                    if (!TryMapToWallpaper(screenX, screenY, destination, bitmap.Size, style, tile, out source)) continue;
                    Color pixel = bitmap.GetPixel(source.X, source.Y);
                    double luminance = RelativeLuminance(pixel.R, pixel.G, pixel.B);
                    sum += luminance;
                    sumSquares += luminance * luminance;
                    count++;
                }
            }

            if (count == 0) return DefaultAppearance("组件位于壁纸图像之外");

            double average = sum / count;
            double variance = Math.Max(0, sumSquares / count - average * average);
            double deviation = Math.Sqrt(variance);
            bool useLightText = average < 0.48;

            return new WallpaperAppearance
            {
                Foreground = useLightText
                    ? System.Windows.Media.Color.FromArgb(255, 245, 247, 250)
                    : System.Windows.Media.Color.FromArgb(255, 18, 20, 23),
                Shadow = useLightText
                    ? System.Windows.Media.Color.FromArgb(255, 0, 0, 0)
                    : System.Windows.Media.Color.FromArgb(255, 255, 255, 255),
                UseBackdrop = deviation > 0.20,
                Luminance = average,
                Description = string.Format("{0}，局部亮度 {1:0.00}", fileName, average)
            };
        }

        private static bool TryMapToWallpaper(int screenX, int screenY, Rectangle dest, Size image,
            string style, bool tile, out Point source)
        {
            source = Point.Empty;
            double localX = screenX - dest.Left;
            double localY = screenY - dest.Top;

            if (tile)
            {
                source.X = PositiveModulo((int)localX, image.Width);
                source.Y = PositiveModulo((int)localY, image.Height);
                return true;
            }

            double sourceX;
            double sourceY;
            if (style == "2")
            {
                sourceX = localX * image.Width / Math.Max(1, dest.Width);
                sourceY = localY * image.Height / Math.Max(1, dest.Height);
            }
            else if (style == "0")
            {
                sourceX = localX - (dest.Width - image.Width) / 2.0;
                sourceY = localY - (dest.Height - image.Height) / 2.0;
            }
            else
            {
                bool fit = style == "6";
                double xScale = dest.Width / (double)image.Width;
                double yScale = dest.Height / (double)image.Height;
                double scale = fit ? Math.Min(xScale, yScale) : Math.Max(xScale, yScale);
                double displayedWidth = image.Width * scale;
                double displayedHeight = image.Height * scale;
                double leftOffset = (dest.Width - displayedWidth) / 2.0;
                double topOffset = (dest.Height - displayedHeight) / 2.0;
                sourceX = (localX - leftOffset) / scale;
                sourceY = (localY - topOffset) / scale;
            }

            if (sourceX < 0 || sourceY < 0 || sourceX >= image.Width || sourceY >= image.Height) return false;
            source.X = Math.Max(0, Math.Min(image.Width - 1, (int)sourceX));
            source.Y = Math.Max(0, Math.Min(image.Height - 1, (int)sourceY));
            return true;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }

        private static double RelativeLuminance(byte red, byte green, byte blue)
        {
            double r = Linearize(red / 255.0);
            double g = Linearize(green / 255.0);
            double b = Linearize(blue / 255.0);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double Linearize(double value)
        {
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        private static WallpaperAppearance DefaultAppearance(string description)
        {
            return new WallpaperAppearance
            {
                Foreground = System.Windows.Media.Color.FromArgb(255, 245, 247, 250),
                Shadow = System.Windows.Media.Color.FromArgb(255, 0, 0, 0),
                UseBackdrop = true,
                Luminance = 0,
                Description = description
            };
        }

        private static string GetWallpaperPath()
        {
            StringBuilder value = new StringBuilder(1024);
            return SystemParametersInfo(SpiGetDesktopWallpaper, value.Capacity, value, 0) ? value.ToString() : string.Empty;
        }

        private static string ReadDesktopValue(string name, string fallback)
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
            {
                object value = key == null ? null : key.GetValue(name);
                return value == null ? fallback : Convert.ToString(value);
            }
        }
    }
}
