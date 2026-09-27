using System.Windows;
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
}
