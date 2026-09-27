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
            RenderGallery(outputDirectory);
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

            Grid cardRoot = new Grid();
            Grid decoration = new Grid { IsHitTestVisible = false };
            Panel.SetZIndex(decoration, 0);
            StackPanel content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            Panel.SetZIndex(content, 1);
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
            cardRoot.Children.Add(decoration);
            cardRoot.Children.Add(content);
            card.Child = cardRoot;
            ThemeCatalog.ApplyPreview(card, decoration, accent, title, digits, theme.Key);
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

        private static void RenderGallery(string outputDirectory)
        {
            const int width = 920;
            const int height = 1010;
            Grid root = new Grid
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(Color.FromRgb(14, 21, 33))
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
            for (int i = 0; i < 5; i++) root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(188) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock heading = new TextBlock
            {
                Text = "桌面倒计时  ·  10 款艺术风格",
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(231, 239, 244)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(22, 0, 0, 0)
            };
            Grid.SetRow(heading, 0);
            Grid.SetColumnSpan(heading, 2);
            root.Children.Add(heading);

            int index = 0;
            foreach (ThemeDefinition theme in ThemeCatalog.All())
            {
                Border cell = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(21, 30, 44)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(39, 52, 69)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(index % 2 == 0 ? 18 : 7, 7, index % 2 == 0 ? 7 : 18, 7),
                    Padding = new Thickness(8)
                };
                StackPanel content = new StackPanel();
                TextBlock name = new TextBlock
                {
                    Text = theme.DisplayName,
                    FontFamily = new FontFamily("MiSans"),
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(207, 218, 226)),
                    Margin = new Thickness(4, 0, 0, 6)
                };
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(Path.Combine(outputDirectory, theme.Key + ".png"), UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                content.Children.Add(name);
                content.Children.Add(new Image
                {
                    Source = image,
                    Width = 420,
                    Height = 146,
                    Stretch = Stretch.UniformToFill
                });
                cell.Child = content;
                Grid.SetRow(cell, index / 2 + 1);
                Grid.SetColumn(cell, index % 2);
                root.Children.Add(cell);
                index++;
            }

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(Path.Combine(outputDirectory, "ThemeGallery.png"))) encoder.Save(stream);
        }
    }
}
