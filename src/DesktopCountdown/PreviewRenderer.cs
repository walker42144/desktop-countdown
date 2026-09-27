using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace DesktopCountdown
{
    public static class PreviewRenderer
    {
        public static int RenderAll(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);
            int count = 0;
            foreach (ThemeDefinition theme in ThemeCatalog.All())
            {
                RenderTheme(theme, Path.Combine(outputDirectory, theme.Key + ".png"));
                count++;
            }
            return count;
        }

        private static void RenderTheme(ThemeDefinition theme, string path)
        {
            const int width = 920;
            const int height = 320;
            Grid canvas = new Grid
            {
                Width = width,
                Height = height,
                Background = CreateWallpaperBackdrop()
            };

            Border card = new Border
            {
                Background = theme.Background,
                BorderBrush = theme.Border,
                BorderThickness = theme.Border == Brushes.Transparent ? new Thickness(0) : new Thickness(1),
                CornerRadius = new CornerRadius(theme.CornerRadius),
                Padding = theme.Padding,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 2,
                    Opacity = theme.ShadowOpacity,
                    Color = theme.Shadow
                }
            };

            StackPanel content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock title = new TextBlock
            {
                Text = "距离目标时刻还有",
                FontFamily = new FontFamily("MiSans"),
                FontSize = 22,
                Foreground = new SolidColorBrush(theme.Foreground),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Border accent = new Border
            {
                Height = 2,
                Width = 42,
                CornerRadius = new CornerRadius(1),
                Background = theme.Accent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 4)
            };
            TextBlock digits = new TextBlock
            {
                FontFamily = new FontFamily("Bahnschrift"),
                FontSize = 84,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(theme.Foreground),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Typography.SetNumeralAlignment(digits, FontNumeralAlignment.Tabular);
            digits.Inlines.Add(new Run("083"));
            digits.Inlines.Add(new Run(" 天  ")
            {
                FontFamily = new FontFamily("MiSans"),
                FontSize = 40
            });
            digits.Inlines.Add(new Run("12:36:20"));

            content.Children.Add(title);
            content.Children.Add(accent);
            content.Children.Add(digits);
            card.Child = content;
            canvas.Children.Add(card);

            canvas.Measure(new Size(width, height));
            canvas.Arrange(new Rect(0, 0, width, height));
            canvas.UpdateLayout();

            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }

        private static Brush CreateWallpaperBackdrop()
        {
            LinearGradientBrush brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(23, 36, 61), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(65, 107, 119), 0.45));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(196, 142, 112), 1));
            return brush;
        }
    }
}
