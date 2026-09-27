using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopCountdown
{
    public sealed class DesktopHostService
    {
        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const int WsChild = 0x40000000;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const uint SwpNoActivate = 0x0010;
        private const uint SmtoNormal = 0x0000;

        private IntPtr windowHandle;
        private IntPtr desktopHost;
        private int originalStyle;
        private bool attached;

        public bool IsAttached { get { return attached; } }

        public bool ApplyDesktopMode(Window window, bool enabled)
        {
            windowHandle = new WindowInteropHelper(window).Handle;
            if (windowHandle == IntPtr.Zero) return false;
            if (enabled == attached) return attached;

            if (enabled)
            {
                desktopHost = FindWallpaperWorker();
                if (desktopHost == IntPtr.Zero) return false;

                NativeRect rect;
                GetWindowRect(windowHandle, out rect);
                originalStyle = GetWindowLong(windowHandle, GwlStyle);
                SetWindowLong(windowHandle, GwlStyle, (originalStyle | WsChild) & ~WsPopup);
                SetParent(windowHandle, desktopHost);
                NativePoint point = new NativePoint { X = rect.Left, Y = rect.Top };
                ScreenToClient(desktopHost, ref point);
                SetWindowPos(windowHandle, IntPtr.Zero, point.X, point.Y,
                    rect.Right - rect.Left, rect.Bottom - rect.Top, SwpNoActivate);
                attached = true;
                return true;
            }

            NativeRect current;
            GetWindowRect(windowHandle, out current);
            SetParent(windowHandle, IntPtr.Zero);
            SetWindowLong(windowHandle, GwlStyle, originalStyle == 0 ? GetWindowLong(windowHandle, GwlStyle) & ~WsChild : originalStyle);
            SetWindowPos(windowHandle, IntPtr.Zero, current.Left, current.Top,
                current.Right - current.Left, current.Bottom - current.Top, SwpNoActivate);
            attached = false;
            desktopHost = IntPtr.Zero;
            return false;
        }

        public void ApplyClickThrough(Window window, bool enabled)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int style = GetWindowLong(handle, GwlExStyle);
            style |= WsExToolWindow;
            if (enabled) style |= WsExTransparent | WsExNoActivate;
            else style &= ~(WsExTransparent | WsExNoActivate);
            SetWindowLong(handle, GwlExStyle, style);
        }

        private static IntPtr FindWallpaperWorker()
        {
            IntPtr progman = FindWindow("Progman", null);
            IntPtr ignored;
            SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SmtoNormal, 1000, out ignored);

            IntPtr result = IntPtr.Zero;
            EnumWindows(delegate(IntPtr topWindow, IntPtr parameter)
            {
                IntPtr shellView = FindWindowEx(topWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    result = FindWindowEx(IntPtr.Zero, topWindow, "WorkerW", null);
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return result != IntPtr.Zero ? result : progman;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);
        [DllImport("user32.dll", SetLastError = true)] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", SetLastError = true)] private static extern int SetWindowLong(IntPtr window, int index, int newValue);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    }
}
