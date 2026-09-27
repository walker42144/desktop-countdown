using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopCountdown
{
    internal sealed class FluentTheme
    {
        public bool IsDark { get; private set; }
        public Brush Background { get; private set; }
        public Brush Card { get; private set; }
        public Brush Panel { get; private set; }
        public Brush Flyout { get; private set; }
        public Brush Border { get; private set; }
        public Brush Field { get; private set; }
        public Brush Text { get; private set; }
        public Brush SecondaryText { get; private set; }
        public Brush Accent { get; private set; }
        public Brush AccentText { get; private set; }
        public Brush Selected { get; private set; }
        public Brush ScrollThumb { get; private set; }

        public FluentTheme(bool dark)
        {
            IsDark = dark;
            Background = Solid(dark ? "#202020" : "#F3F3F3");
            Card = Solid(dark ? "#2B2B2B" : "#FFFFFF");
            Panel = Solid(dark ? "#262626" : "#F8F8F8");
            Flyout = Solid(dark ? "#D92B2B2B" : "#E8FFFFFF");
            Border = Solid(dark ? "#3A3A3A" : "#E5E5E5");
            Field = Solid(dark ? "#353535" : "#FFFFFF");
            Text = Solid(dark ? "#F7F7F7" : "#1C1C1C");
            SecondaryText = Solid(dark ? "#BDBDBD" : "#606060");
            Color accent = SystemParameters.WindowGlassColor;
            if (accent.A == 0) accent = Color.FromRgb(0, 103, 192);
            if (dark && Contrast(accent) < 0.43)
            {
                double mix = Math.Min(0.60, (0.48 - Contrast(accent)) / (1.0 - Contrast(accent)));
                accent = Color.FromRgb((byte)(accent.R + (255 - accent.R) * mix),
                    (byte)(accent.G + (255 - accent.G) * mix),
                    (byte)(accent.B + (255 - accent.B) * mix));
            }
            Accent = new SolidColorBrush(accent);
            AccentText = Contrast(accent) > 0.48 ? Brushes.Black : Brushes.White;
            Selected = new SolidColorBrush(Color.FromArgb(dark ? (byte)75 : (byte)35, accent.R, accent.G, accent.B));
            ScrollThumb = Solid(dark ? "#777777" : "#8C8C8C");
        }

        public void Install(ResourceDictionary resources)
        {
            resources["FluentBackground"] = Background;
            resources["FluentCard"] = Card;
            resources["FluentPanel"] = Panel;
            resources["FluentFlyout"] = Flyout;
            resources["FluentBorder"] = Border;
            resources["FluentField"] = Field;
            resources["FluentText"] = Text;
            resources["FluentSecondaryText"] = SecondaryText;
            resources["FluentAccent"] = Accent;
            resources["FluentAccentText"] = AccentText;
            resources["FluentSelected"] = Selected;
            resources["FluentScrollThumb"] = ScrollThumb;
        }

        private static Brush Solid(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static double Contrast(Color color)
        {
            return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
        }
    }
}
