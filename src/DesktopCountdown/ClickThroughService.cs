using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopCountdown
{
    public sealed class ClickThroughService
    {
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;

        private readonly Func<IntPtr, int?> readStyle;
        private readonly Func<IntPtr, int, bool> writeStyle;

        public ClickThroughService() : this(ReadStyle, WriteStyle) { }

        internal ClickThroughService(Func<IntPtr, int?> readStyle,
            Func<IntPtr, int, bool> writeStyle)
        {
            if (readStyle == null) throw new ArgumentNullException("readStyle");
            if (writeStyle == null) throw new ArgumentNullException("writeStyle");
            this.readStyle = readStyle;
            this.writeStyle = writeStyle;
        }

        public void Apply(Window window, bool enabled)
        {
            if (window == null) throw new ArgumentNullException("window");
            TryApply(new WindowInteropHelper(window).Handle, enabled);
        }

        internal bool TryApply(IntPtr handle, bool enabled)
        {
            if (handle == IntPtr.Zero) return false;
            int? before;
            try { before = readStyle(handle); }
            catch { return false; }
            if (!before.HasValue) return false;

            int style = before.Value | WsExToolWindow;
            if (enabled) style |= WsExTransparent | WsExNoActivate;
            else style &= ~(WsExTransparent | WsExNoActivate);
            try { if (writeStyle(handle, style)) return true; }
            catch { }
            try { writeStyle(handle, before.Value); }
            catch { }
            return false;
        }

        private static int? ReadStyle(IntPtr handle)
        {
            if (!IsWindow(handle)) return null;
            SetLastError(0);
            int value = GetWindowLong(handle, GwlExStyle);
            return value != 0 || Marshal.GetLastWin32Error() == 0 ? (int?)value : null;
        }

        private static bool WriteStyle(IntPtr handle, int value)
        {
            SetLastError(0);
            int previous = SetWindowLong(handle, GwlExStyle, value);
            return previous != 0 || Marshal.GetLastWin32Error() == 0;
        }

        [DllImport("kernel32.dll")] private static extern void SetLastError(uint errorCode);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
        [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(IntPtr window, int index, int newValue);
    }
}
