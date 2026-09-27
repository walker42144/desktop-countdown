using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopCountdown
{
    public sealed class ThemeDefinition
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public bool AdaptiveToWallpaper { get; set; }
        public bool TransparentWhenCalm { get; set; }
        public Brush Background { get; set; }
        public Brush Border { get; set; }
        public Brush Accent { get; set; }
        public Color Foreground { get; set; }
        public Color Shadow { get; set; }
        public double CornerRadius { get; set; }
        public Thickness Padding { get; set; }
        public double ShadowOpacity { get; set; }

        public override string ToString() { return DisplayName; }
    }

    public static class ThemeCatalog
    {
        public const string DefaultTheme = "MinimalGlass";

        public static IList<ThemeDefinition> All()
        {
            return new List<ThemeDefinition>
            {
                new ThemeDefinition
                {
                    Key = "MinimalGlass",
                    DisplayName = "极简玻璃",
                    AdaptiveToWallpaper = true,
                    TransparentWhenCalm = false,
                    Background = new SolidColorBrush(Color.FromArgb(28, 20, 23, 28)),
                    Border = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
                    Accent = new SolidColorBrush(Color.FromArgb(105, 255, 255, 255)),
                    Foreground = Color.FromRgb(245, 247, 250),
                    Shadow = Colors.Black,
                    CornerRadius = 18,
                    Padding = new Thickness(27, 18, 27, 21),
                    ShadowOpacity = 0.55
                },
                new ThemeDefinition
                {
                    Key = "Air",
                    DisplayName = "现代留白",
                    AdaptiveToWallpaper = true,
                    TransparentWhenCalm = true,
                    Background = Brushes.Transparent,
                    Border = Brushes.Transparent,
                    Accent = new SolidColorBrush(Color.FromArgb(95, 255, 255, 255)),
                    Foreground = Color.FromRgb(245, 247, 250),
                    Shadow = Colors.Black,
                    CornerRadius = 12,
                    Padding = new Thickness(24, 14, 24, 18),
                    ShadowOpacity = 0.58
                },
                new ThemeDefinition
                {
                    Key = "Editorial",
                    DisplayName = "编辑部海报",
                    AdaptiveToWallpaper = false,
                    Background = new SolidColorBrush(Color.FromArgb(238, 244, 239, 229)),
                    Border = new SolidColorBrush(Color.FromArgb(120, 66, 58, 52)),
                    Accent = new SolidColorBrush(Color.FromRgb(177, 60, 46)),
                    Foreground = Color.FromRgb(39, 36, 34),
                    Shadow = Colors.Black,
                    CornerRadius = 3,
                    Padding = new Thickness(30, 21, 30, 24),
                    ShadowOpacity = 0.28
                },
                new ThemeDefinition
                {
                    Key = "Dusk",
                    DisplayName = "暮色渐变",
                    AdaptiveToWallpaper = false,
                    Background = Gradient(Color.FromArgb(232, 47, 37, 72), Color.FromArgb(232, 151, 75, 82), 20),
                    Border = new SolidColorBrush(Color.FromArgb(58, 255, 235, 220)),
                    Accent = new SolidColorBrush(Color.FromRgb(247, 185, 137)),
                    Foreground = Color.FromRgb(255, 246, 235),
                    Shadow = Color.FromRgb(30, 19, 32),
                    CornerRadius = 22,
                    Padding = new Thickness(29, 20, 29, 23),
                    ShadowOpacity = 0.48
                },
                new ThemeDefinition
                {
                    Key = "Ink",
                    DisplayName = "东方墨韵",
                    AdaptiveToWallpaper = false,
                    Background = Gradient(Color.FromArgb(235, 236, 236, 231), Color.FromArgb(235, 190, 198, 194), 0),
                    Border = new SolidColorBrush(Color.FromArgb(76, 37, 45, 43)),
                    Accent = new SolidColorBrush(Color.FromRgb(89, 111, 103)),
                    Foreground = Color.FromRgb(31, 38, 36),
                    Shadow = Colors.White,
                    CornerRadius = 10,
                    Padding = new Thickness(30, 20, 30, 24),
                    ShadowOpacity = 0.20
                }
            };
        }

        public static ThemeDefinition Get(string key)
        {
            foreach (ThemeDefinition theme in All())
                if (string.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase)) return theme;
            return All()[0];
        }

        public static void ApplyPreview(Border panel, TextBlock title, TextBlock digits, string key)
        {
            ThemeDefinition theme = Get(key);
            panel.Background = theme.Background;
            panel.BorderBrush = theme.Border;
            panel.BorderThickness = theme.Border == Brushes.Transparent ? new Thickness(0) : new Thickness(1);
            panel.CornerRadius = new CornerRadius(theme.CornerRadius);
            title.Foreground = new SolidColorBrush(theme.Foreground);
            digits.Foreground = new SolidColorBrush(theme.Foreground);
        }

        private static Brush Gradient(Color first, Color second, double angle)
        {
            LinearGradientBrush brush = new LinearGradientBrush(first, second, angle);
            brush.Freeze();
            return brush;
        }
    }
}
