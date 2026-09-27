using System.Diagnostics;
using System.Drawing;
using System.Reflection;
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
            using (System.IO.Stream stream = typeof(IconFactory).Assembly.GetManifestResourceStream("DesktopCountdown.Brand.ico"))
            {
                if (stream != null)
                {
                    using (Icon embedded = new Icon(stream)) return (Icon)embedded.Clone();
                }
            }
            string executable = Process.GetCurrentProcess().MainModule.FileName;
            using (Icon associated = Icon.ExtractAssociatedIcon(executable))
            {
                return associated == null ? (Icon)SystemIcons.Application.Clone() : (Icon)associated.Clone();
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
