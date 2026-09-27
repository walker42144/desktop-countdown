using System;
using System.Runtime.InteropServices;

namespace DesktopCountdown
{
    internal sealed class NativeWindowSnapshot
    {
        internal readonly IntPtr Parent;
        internal readonly int Style;
        internal readonly int ExStyle;
        internal readonly int Left;
        internal readonly int Top;
        internal readonly int Width;
        internal readonly int Height;

        internal NativeWindowSnapshot(IntPtr parent, int style, int exStyle,
            int left, int top, int width, int height)
        {
            Parent = parent;
            Style = style;
            ExStyle = exStyle;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }
    }

    internal sealed class NativeWindowOps
    {
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const uint SwpNoActivate = 0x0010;
        private const uint SmtoNormal = 0x0000;

        internal readonly Func<IntPtr> FindHost;
        internal readonly Func<IntPtr, object> Capture;
        internal readonly Func<IntPtr, int, bool> SetStyle;
        internal readonly Func<IntPtr, int, bool> SetExStyle;
        internal readonly Func<IntPtr, IntPtr, bool> SetParent;
        internal readonly Func<IntPtr, IntPtr, int, int, int, int, bool> SetPosition;
        internal readonly Func<IntPtr, IntPtr, bool> DpiCompatible;

        internal NativeWindowOps()
            : this(FindWallpaperWorker, CaptureNative, SetStyleNative, SetExStyleNative,
                SetParentNative, SetPositionNative, HasCompatibleDpi)
        {
        }

        internal NativeWindowOps(Func<IntPtr> findHost, Func<IntPtr, object> capture,
            Func<IntPtr, int, bool> setStyle, Func<IntPtr, int, bool> setExStyle,
            Func<IntPtr, IntPtr, bool> setParent,
            Func<IntPtr, IntPtr, int, int, int, int, bool> setPosition)
            : this(findHost, capture, setStyle, setExStyle, setParent, setPosition,
                delegate { return true; })
        {
        }

        internal NativeWindowOps(Func<IntPtr> findHost, Func<IntPtr, object> capture,
            Func<IntPtr, int, bool> setStyle, Func<IntPtr, int, bool> setExStyle,
            Func<IntPtr, IntPtr, bool> setParent,
            Func<IntPtr, IntPtr, int, int, int, int, bool> setPosition,
            Func<IntPtr, IntPtr, bool> dpiCompatible)
        {
            if (findHost == null || capture == null || setStyle == null || setExStyle == null ||
                setParent == null || setPosition == null || dpiCompatible == null)
                throw new ArgumentNullException("nativeOps");
            FindHost = findHost;
            Capture = capture;
            SetStyle = setStyle;
            SetExStyle = setExStyle;
            SetParent = setParent;
            SetPosition = setPosition;
            DpiCompatible = dpiCompatible;
        }

        private static bool HasCompatibleDpi(IntPtr child, IntPtr host)
        {
            try
            {
                IntPtr childContext = GetWindowDpiAwarenessContext(child);
                IntPtr hostContext = GetWindowDpiAwarenessContext(host);
                return childContext != IntPtr.Zero && hostContext != IntPtr.Zero &&
                    AreDpiAwarenessContextsEqual(childContext, hostContext);
            }
            catch (EntryPointNotFoundException) { return false; }
        }

        private static object CaptureNative(IntPtr handle)
        {
            if (!IsWindow(handle)) return null;
            NativeRect rect;
            int style;
            int exStyle;
            if (!GetWindowRect(handle, out rect) || !TryGetWindowLong(handle, GwlStyle, out style) ||
                !TryGetWindowLong(handle, GwlExStyle, out exStyle)) return null;
            return new NativeWindowSnapshot(GetParent(handle), style, exStyle,
                rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        private static bool TryGetWindowLong(IntPtr handle, int index, out int value)
        {
            SetLastError(0);
            value = GetWindowLong(handle, index);
            return value != 0 || Marshal.GetLastWin32Error() == 0;
        }

        private static bool SetStyleNative(IntPtr handle, int style)
        {
            return TrySetWindowLong(handle, GwlStyle, style);
        }

        private static bool SetExStyleNative(IntPtr handle, int style)
        {
            return TrySetWindowLong(handle, GwlExStyle, style);
        }

        private static bool TrySetWindowLong(IntPtr handle, int index, int value)
        {
            SetLastError(0);
            int previous = SetWindowLong(handle, index, value);
            return previous != 0 || Marshal.GetLastWin32Error() == 0;
        }

        private static bool SetParentNative(IntPtr handle, IntPtr parent)
        {
            SetLastError(0);
            IntPtr previous = SetParentWindow(handle, parent);
            return previous != IntPtr.Zero || Marshal.GetLastWin32Error() == 0;
        }

        private static bool SetPositionNative(IntPtr handle, IntPtr parent,
            int left, int top, int width, int height)
        {
            NativePoint point = new NativePoint { X = left, Y = top };
            if (parent != IntPtr.Zero && !ScreenToClient(parent, ref point)) return false;
            return SetWindowPos(handle, IntPtr.Zero, point.X, point.Y, width, height, SwpNoActivate);
        }

        private static IntPtr FindWallpaperWorker()
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return IntPtr.Zero;
            IntPtr ignored;
            if (SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero,
                SmtoNormal, 1000, out ignored) == IntPtr.Zero) return IntPtr.Zero;

            IntPtr result = IntPtr.Zero;
            bool enumerated = EnumWindows(delegate(IntPtr topWindow, IntPtr parameter)
            {
                if (result == IntPtr.Zero && FindWindowEx(topWindow, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                    result = FindWindowEx(IntPtr.Zero, topWindow, "WorkerW", null);
                return true;
            }, IntPtr.Zero);
            if (!enumerated) return IntPtr.Zero;
            return result != IntPtr.Zero && IsWindowVisible(result) ? result : IntPtr.Zero;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

        [DllImport("kernel32.dll")] private static extern void SetLastError(uint errorCode);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr handle);
        [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(IntPtr left, IntPtr right);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr handle);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll", EntryPoint = "SetParent", SetLastError = true)] private static extern IntPtr SetParentWindow(IntPtr child, IntPtr newParent);
        [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(IntPtr window, int index, int newValue);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }
}
