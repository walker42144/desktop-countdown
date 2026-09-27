using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DesktopCountdown
{
    public static class ThemeDecorations
    {
        public static TextBlock Apply(Grid layer, ThemeDefinition theme, Color foreground, string dynamicText)
        {
            layer.Children.Clear();
            layer.IsHitTestVisible = false;
            layer.Opacity = 1;
            TextBlock dynamicLabel = null;

            string style = theme.Decoration ?? string.Empty;
            if (style == "Glass")
            {
                AddOrb(layer, theme.Accent, 96, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, -12, -10, 0), 0.20, 20);
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 2, 3, 0), 9, 0.45);
            }
            else if (style == "Air")
            {
                dynamicLabel = AddLabel(layer, dynamicText, foreground, HorizontalAlignment.Right,
                    VerticalAlignment.Top, new Thickness(0, 4, 3, 0), 9, 0.54);
            }
            else if (style == "Editorial")
            {
                AddRail(layer, theme.Accent, HorizontalAlignment.Left, new Thickness(-18, 11, 0, 11), 4);
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, -3, 0, 0), 9, 0.55);
                AddCorner(layer, theme.Accent, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, -12, -9));
            }
            else if (style == "Orbit")
            {
                AddOrbit(layer, theme.Accent, 104, new Thickness(0, -12, -8, 0));
                AddOrbit(layer, theme.SecondaryAccent, 64, new Thickness(-8, 0, 0, -10), HorizontalAlignment.Left, VerticalAlignment.Bottom);
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 0, 2, 0), 9, 0.50);
            }
            else if (style == "Ink")
            {
                AddOrb(layer, theme.Accent, 138, HorizontalAlignment.Left, VerticalAlignment.Center, new Thickness(-58, 0, 0, 0), 0.09, 16);
                AddSeal(layer, theme.SecondaryAccent, "时");
            }
            else if (style == "Horizon")
            {
                AddHorizon(layer, theme.Accent);
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 0, 1, 0), 9, 0.48);
            }
            else if (style == "Porcelain")
            {
                AddOrbit(layer, theme.Accent, 92, new Thickness(-10, -12, 0, 0), HorizontalAlignment.Left, VerticalAlignment.Top);
                AddSeal(layer, theme.SecondaryAccent, "瓷");
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Left, VerticalAlignment.Bottom, new Thickness(2, 0, 0, -5), 8, 0.42);
            }
            else if (style == "Neon")
            {
                AddGlowFrame(layer, theme.Accent);
                AddDot(layer, theme.SecondaryAccent, 8, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 7, 5, 0));
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, 5, 18, 0), 9, 0.58);
            }
            else if (style == "Film")
            {
                AddFilmStrip(layer, theme.SecondaryAccent, VerticalAlignment.Top);
                AddFilmStrip(layer, theme.SecondaryAccent, VerticalAlignment.Bottom);
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, -13, 0, 0), 8, 0.52);
            }
            else if (style == "Blueprint")
            {
                AddBlueprintGrid(layer, theme.SecondaryAccent);
                AddCorner(layer, theme.Accent, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(-18, -12, 0, 0));
                AddCorner(layer, theme.Accent, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, -18, -12));
                AddLabel(layer, theme.Kicker, foreground, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, -5, 0, 0), 8, 0.54);
            }
            return dynamicLabel;
        }

        private static TextBlock AddLabel(Grid layer, string text, Color color, HorizontalAlignment horizontal,
            VerticalAlignment vertical, Thickness margin, double size, double opacity)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Segoe UI Variable"),
                FontSize = size,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(color),
                Opacity = opacity,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = margin
            };
            layer.Children.Add(label);
            return label;
        }

        private static void AddOrb(Grid layer, Brush brush, double size, HorizontalAlignment horizontal,
            VerticalAlignment vertical, Thickness margin, double opacity, double blur)
        {
            Ellipse orb = new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                Opacity = opacity,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = margin,
                Effect = new BlurEffect { Radius = blur }
            };
            layer.Children.Add(orb);
        }

        private static void AddDot(Grid layer, Brush brush, double size, HorizontalAlignment horizontal,
            VerticalAlignment vertical, Thickness margin)
        {
            layer.Children.Add(new Ellipse
            {
                Width = size,
                Height = size,
                Fill = brush,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = margin
            });
        }

        private static void AddRail(Grid layer, Brush brush, HorizontalAlignment horizontal, Thickness margin, double width)
        {
            layer.Children.Add(new Border
            {
                Width = width,
                Background = brush,
                CornerRadius = new CornerRadius(width / 2),
                HorizontalAlignment = horizontal,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = margin
            });
        }

        private static void AddOrbit(Grid layer, Brush brush, double size, Thickness margin)
        {
            AddOrbit(layer, brush, size, margin, HorizontalAlignment.Right, VerticalAlignment.Top);
        }

        private static void AddOrbit(Grid layer, Brush brush, double size, Thickness margin,
            HorizontalAlignment horizontal, VerticalAlignment vertical)
        {
            layer.Children.Add(new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = brush,
                StrokeThickness = 1,
                Opacity = 0.34,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = margin
            });
        }

        private static void AddSeal(Grid layer, Brush brush, string text)
        {
            Border seal = new Border
            {
                Width = 27,
                Height = 27,
                BorderBrush = brush,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -4, -6, 0),
                Child = new TextBlock
                {
                    Text = text,
                    FontFamily = new FontFamily("MiSans"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    Foreground = brush,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            layer.Children.Add(seal);
        }

        private static void AddHorizon(Grid layer, Brush brush)
        {
            StackPanel dots = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(1, 0, 0, -5)
            };
            for (int i = 0; i < 3; i++) dots.Children.Add(new Ellipse { Width = 4, Height = 4, Fill = brush, Margin = new Thickness(0, 0, 5, 0) });
            layer.Children.Add(dots);
            layer.Children.Add(new Border
            {
                Height = 1,
                Background = brush,
                Opacity = 0.34,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(26, 0, 0, -3)
            });
        }

        private static void AddGlowFrame(Grid layer, Brush brush)
        {
            layer.Children.Add(new Border
            {
                BorderBrush = brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(-13, -9, -13, -10),
                Opacity = 0.64,
                Effect = new DropShadowEffect { Color = Color.FromRgb(75, 242, 220), BlurRadius = 14, ShadowDepth = 0, Opacity = 0.72 }
            });
        }

        private static void AddFilmStrip(Grid layer, Brush brush, VerticalAlignment vertical)
        {
            StackPanel strip = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = vertical,
                Margin = vertical == VerticalAlignment.Top ? new Thickness(-8, -17, -8, 0) : new Thickness(-8, 0, -8, -17)
            };
            for (int i = 0; i < 11; i++) strip.Children.Add(new Border
            {
                Width = 12,
                Height = 5,
                CornerRadius = new CornerRadius(1),
                Background = brush,
                Opacity = 0.35,
                Margin = new Thickness(3, 0, 3, 0)
            });
            layer.Children.Add(strip);
        }

        private static void AddBlueprintGrid(Grid layer, Brush brush)
        {
            for (int i = 1; i < 5; i++)
            {
                layer.Children.Add(new Border
                {
                    Width = 1,
                    Background = brush,
                    Opacity = 0.08,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(i * 90, -18, 0, -18)
                });
            }
            for (int i = 1; i < 3; i++)
            {
                layer.Children.Add(new Border
                {
                    Height = 1,
                    Background = brush,
                    Opacity = 0.08,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(-20, i * 55, -20, 0)
                });
            }
        }

        private static void AddCorner(Grid layer, Brush brush, HorizontalAlignment horizontal,
            VerticalAlignment vertical, Thickness margin)
        {
            Grid corner = new Grid
            {
                Width = 22,
                Height = 22,
                HorizontalAlignment = horizontal,
                VerticalAlignment = vertical,
                Margin = margin,
                Opacity = 0.72
            };
            corner.Children.Add(new Border { Width = 22, Height = 2, Background = brush, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            corner.Children.Add(new Border { Width = 2, Height = 22, Background = brush, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
            layer.Children.Add(corner);
        }
    }
}
