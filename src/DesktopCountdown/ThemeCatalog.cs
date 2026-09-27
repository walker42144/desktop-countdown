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
        public string Decoration { get; set; }
        public string Kicker { get; set; }
        public bool AdaptiveToWallpaper { get; set; }
        public bool TransparentWhenCalm { get; set; }
        public Brush Background { get; set; }
        public Brush Border { get; set; }
        public Brush Accent { get; set; }
        public Brush SecondaryAccent { get; set; }
        public Color Foreground { get; set; }
        public Color Shadow { get; set; }
        public double CornerRadius { get; set; }
        public Thickness Padding { get; set; }
        public double ShadowOpacity { get; set; }
        public double BorderThickness { get; set; }
        public double AccentWidth { get; set; }
        public double AccentHeight { get; set; }
        public double TitleOpacity { get; set; }
        public FontWeight DigitWeight { get; set; }

        public override string ToString() { return DisplayName; }
    }

    public static class ThemeCatalog
    {
        public const string DefaultTheme = "MinimalGlass";

        public static IList<ThemeDefinition> All()
        {
            return new List<ThemeDefinition>
            {
                Theme("MinimalGlass", "极简玻璃", "Glass", "T–MINUS", true, false,
                    Solid(28, 14, 23, 31), Solid(58, 255, 255, 255), Solid(190, 103, 220, 205), Solid(130, 255, 133, 100),
                    Color.FromRgb(245, 247, 250), Colors.Black, 20, new Thickness(31, 22, 31, 24), 0.50, 1, 46, 2, 0.82, FontWeights.SemiBold),

                Theme("Air", "现代留白", "Air", "", true, true,
                    Brushes.Transparent, Brushes.Transparent, Solid(180, 103, 220, 205), Solid(150, 255, 133, 100),
                    Color.FromRgb(245, 247, 250), Colors.Black, 12, new Thickness(26, 17, 26, 21), 0.56, 0, 24, 3, 0.78, FontWeights.SemiBold),

                Theme("Editorial", "编辑部海报", "Editorial", "EDITION 01", false, false,
                    Solid(244, 244, 239, 229), Solid(150, 53, 48, 44), Solid(255, 184, 57, 43), Solid(255, 39, 36, 34),
                    Color.FromRgb(39, 36, 34), Colors.Black, 3, new Thickness(36, 25, 36, 27), 0.25, 1, 56, 3, 0.72, FontWeights.SemiBold),

                Theme("Dusk", "暮色星轨", "Orbit", "ORBIT / 083", false, false,
                    Gradient3(Color.FromArgb(242, 37, 31, 67), Color.FromArgb(242, 100, 53, 89), Color.FromArgb(242, 164, 82, 83), 18),
                    Solid(70, 255, 232, 218), Solid(255, 247, 185, 137), Solid(160, 121, 224, 211),
                    Color.FromRgb(255, 246, 235), Color.FromRgb(24, 15, 30), 24, new Thickness(34, 24, 34, 27), 0.48, 1, 48, 2, 0.80, FontWeights.SemiBold),

                Theme("Ink", "东方墨韵", "Ink", "留 白", false, false,
                    Gradient3(Color.FromArgb(244, 239, 239, 234), Color.FromArgb(244, 213, 218, 213), Color.FromArgb(244, 186, 198, 191), 0),
                    Solid(88, 35, 44, 41), Solid(255, 82, 107, 98), Solid(255, 173, 61, 49),
                    Color.FromRgb(29, 37, 34), Colors.White, 12, new Thickness(35, 24, 35, 28), 0.18, 1, 36, 2, 0.70, FontWeights.SemiBold),

                Theme("DeepSea", "深海夜航", "Horizon", "DEEP TIME", false, false,
                    Gradient3(Color.FromArgb(246, 7, 24, 42), Color.FromArgb(246, 10, 56, 70), Color.FromArgb(246, 18, 94, 96), 16),
                    Solid(76, 129, 235, 218), Solid(255, 111, 231, 210), Solid(210, 65, 151, 185),
                    Color.FromRgb(231, 250, 247), Color.FromRgb(0, 10, 18), 18, new Thickness(34, 24, 34, 27), 0.50, 1, 68, 2, 0.78, FontWeights.SemiBold),

                Theme("Porcelain", "青瓷晨雾", "Porcelain", "CELADON", false, false,
                    Gradient3(Color.FromArgb(246, 244, 242, 232), Color.FromArgb(246, 212, 226, 215), Color.FromArgb(246, 174, 205, 193), 25),
                    Solid(105, 69, 102, 92), Solid(255, 72, 125, 108), Solid(170, 183, 92, 71),
                    Color.FromRgb(31, 55, 48), Colors.White, 26, new Thickness(36, 25, 36, 28), 0.20, 1, 40, 2, 0.74, FontWeights.SemiBold),

                Theme("Neon", "霓虹夜行", "Neon", "LIVE COUNT", false, false,
                    Gradient3(Color.FromArgb(248, 7, 8, 20), Color.FromArgb(248, 17, 13, 38), Color.FromArgb(248, 17, 31, 45), 20),
                    Solid(150, 86, 246, 229), Solid(255, 75, 242, 220), Solid(255, 255, 74, 164),
                    Color.FromRgb(234, 255, 251), Color.FromRgb(0, 0, 0), 16, new Thickness(34, 24, 34, 27), 0.66, 1, 54, 2, 0.78, FontWeights.SemiBold),

                Theme("AmberFilm", "琥珀胶片", "Film", "FRAME 011", false, false,
                    Gradient3(Color.FromArgb(246, 48, 29, 20), Color.FromArgb(246, 99, 57, 27), Color.FromArgb(246, 160, 98, 39), 8),
                    Solid(95, 255, 221, 163), Solid(255, 255, 190, 96), Solid(210, 255, 226, 174),
                    Color.FromRgb(255, 239, 204), Color.FromRgb(28, 13, 5), 8, new Thickness(38, 29, 38, 31), 0.46, 1, 44, 2, 0.76, FontWeights.SemiBold),

                Theme("Blueprint", "蓝图刻度", "Blueprint", "COUNTDOWN / GRID", false, false,
                    Solid(248, 13, 55, 103), Solid(130, 126, 204, 255), Solid(255, 101, 211, 255), Solid(190, 255, 255, 255),
                    Color.FromRgb(231, 247, 255), Color.FromRgb(0, 18, 46), 2, new Thickness(36, 25, 36, 28), 0.42, 1, 72, 1, 0.76, FontWeights.SemiBold)
            };
        }

        public static ThemeDefinition Get(string key)
        {
            foreach (ThemeDefinition theme in All())
                if (string.Equals(theme.Key, key, StringComparison.OrdinalIgnoreCase)) return theme;
            return All()[0];
        }

        public static void ApplyPreview(Border panel, Grid decorationLayer, Border accentLine,
            TextBlock title, TextBlock digits, string key)
        {
            ThemeDefinition theme = Get(key);
            panel.Background = theme.Background;
            panel.BorderBrush = theme.Border;
            panel.BorderThickness = new Thickness(theme.BorderThickness);
            panel.CornerRadius = new CornerRadius(theme.CornerRadius);
            panel.Padding = theme.Padding;
            title.Foreground = new SolidColorBrush(theme.Foreground);
            title.Opacity = theme.TitleOpacity;
            digits.Foreground = new SolidColorBrush(theme.Foreground);
            digits.FontWeight = theme.DigitWeight;
            accentLine.Background = theme.Accent;
            accentLine.Width = theme.AccentWidth;
            accentLine.Height = theme.AccentHeight;
            accentLine.Visibility = theme.AccentWidth <= 0 ? Visibility.Collapsed : Visibility.Visible;
            ThemeDecorations.Apply(decorationLayer, theme, theme.Foreground,
                DateTime.Now.ToString("yyyy-MM-dd  HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        }

        private static ThemeDefinition Theme(string key, string displayName, string decoration, string kicker,
            bool adaptive, bool transparentWhenCalm, Brush background, Brush border, Brush accent, Brush secondary,
            Color foreground, Color shadow, double radius, Thickness padding, double shadowOpacity,
            double borderThickness, double accentWidth, double accentHeight, double titleOpacity, FontWeight digitWeight)
        {
            return new ThemeDefinition
            {
                Key = key,
                DisplayName = displayName,
                Decoration = decoration,
                Kicker = kicker,
                AdaptiveToWallpaper = adaptive,
                TransparentWhenCalm = transparentWhenCalm,
                Background = background,
                Border = border,
                Accent = accent,
                SecondaryAccent = secondary,
                Foreground = foreground,
                Shadow = shadow,
                CornerRadius = radius,
                Padding = padding,
                ShadowOpacity = shadowOpacity,
                BorderThickness = borderThickness,
                AccentWidth = accentWidth,
                AccentHeight = accentHeight,
                TitleOpacity = titleOpacity,
                DigitWeight = digitWeight
            };
        }

        private static Brush Solid(byte alpha, byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static Brush Gradient3(Color first, Color middle, Color last, double angle)
        {
            LinearGradientBrush brush = new LinearGradientBrush
            {
                StartPoint = angle < 12 ? new Point(0, 0.5) : new Point(0, 0),
                EndPoint = angle < 12 ? new Point(1, 0.5) : new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop(first, 0));
            brush.GradientStops.Add(new GradientStop(middle, 0.55));
            brush.GradientStops.Add(new GradientStop(last, 1));
            brush.Freeze();
            return brush;
        }
    }
}
