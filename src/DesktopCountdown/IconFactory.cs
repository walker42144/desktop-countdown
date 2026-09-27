using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopCountdown
{
    public static class IconFactory
    {
        public static Icon CreateClockIcon()
        {
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            using (Icon associated = Icon.ExtractAssociatedIcon(executable))
            {
                if (associated == null) return (Icon)SystemIcons.Application.Clone();
                return (Icon)associated.Clone();
            }
        }

        public static ImageSource CreateWindowIcon()
        {
            using (Icon icon = CreateClockIcon())
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromWidthAndHeight(32, 32));
                source.Freeze();
                return source;
            }
        }
    }
}
