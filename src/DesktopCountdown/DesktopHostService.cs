using System;
using System.Windows;
using System.Windows.Interop;

namespace DesktopCountdown
{
    public sealed class DesktopHostService
    {
        private const int WsChild = 0x40000000;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;

        private readonly NativeWindowOps ops;
        private IntPtr windowHandle;
        private IntPtr hostHandle;
        private NativeWindowSnapshot original;
        private bool attached;

        public DesktopHostService() : this(new NativeWindowOps()) { }

        internal DesktopHostService(NativeWindowOps nativeOps)
        {
            if (nativeOps == null) throw new ArgumentNullException("nativeOps");
            ops = nativeOps;
        }

        public bool IsAttached { get { return attached; } }

        public bool ApplyDesktopMode(Window window, bool enabled)
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            return ApplyWithHandle(handle, enabled);
        }

        internal bool ApplyWithHandle(IntPtr handle, bool enabled)
        {
            if (handle == IntPtr.Zero) return attached;
            if (attached)
            {
                NativeWindowSnapshot current = Capture(handle);
                if (handle != windowHandle || current == null || current.Parent != hostHandle)
                {
                    bool restored = handle != windowHandle || original == null || Restore(handle, original);
                    attached = false;
                    original = null;
                    hostHandle = IntPtr.Zero;
                    if (!restored) return false;
                }
            }
            if (enabled == attached) return attached;
            return enabled ? Attach(handle) : Detach(handle);
        }

        private bool Attach(IntPtr handle)
        {
            IntPtr host;
            NativeWindowSnapshot before;
            try
            {
                host = ops.FindHost();
                before = Capture(handle);
            }
            catch { return false; }
            if (host == IntPtr.Zero || before == null) return false;

            try
            {
                if (!ops.SetStyle(handle, (before.Style | WsChild) & ~WsPopup))
                    throw new InvalidOperationException("SetStyle");
                if (!ops.SetParent(handle, host))
                    throw new InvalidOperationException("SetParent");
                if (!ops.SetPosition(handle, host, before.Left, before.Top, before.Width, before.Height))
                    throw new InvalidOperationException("SetPosition");
                windowHandle = handle;
                hostHandle = host;
                original = before;
                attached = true;
                return true;
            }
            catch
            {
                Restore(handle, before);
                return false;
            }
        }

        private bool Detach(IntPtr handle)
        {
            NativeWindowSnapshot current = Capture(handle);
            if (current == null || original == null) return true;
            try
            {
                if (!ops.SetParent(handle, original.Parent))
                    throw new InvalidOperationException("SetParent");
                if (!ops.SetStyle(handle, original.Style))
                    throw new InvalidOperationException("SetStyle");
                if (!ops.SetPosition(handle, original.Parent,
                    current.Left, current.Top, current.Width, current.Height))
                    throw new InvalidOperationException("SetPosition");
                attached = false;
                original = null;
                hostHandle = IntPtr.Zero;
                return false;
            }
            catch
            {
                Restore(handle, current);
                return true;
            }
        }

        private NativeWindowSnapshot Capture(IntPtr handle)
        {
            try { return ops.Capture(handle) as NativeWindowSnapshot; }
            catch { return null; }
        }

        private bool Restore(IntPtr handle, NativeWindowSnapshot state)
        {
            bool restored = true;
            try { if (!ops.SetParent(handle, state.Parent)) restored = false; } catch { restored = false; }
            try { if (!ops.SetStyle(handle, state.Style)) restored = false; } catch { restored = false; }
            try { if (!ops.SetExStyle(handle, state.ExStyle)) restored = false; } catch { restored = false; }
            try
            {
                if (!ops.SetPosition(handle, state.Parent, state.Left, state.Top, state.Width, state.Height))
                    restored = false;
            }
            catch { restored = false; }
            return restored;
        }

        public void ApplyClickThrough(Window window, bool enabled)
        {
            TryApplyClickThrough(new WindowInteropHelper(window).Handle, enabled);
        }

        internal bool TryApplyClickThrough(IntPtr handle, bool enabled)
        {
            if (handle == IntPtr.Zero) return false;
            NativeWindowSnapshot state = Capture(handle);
            if (state == null) return false;
            int style = state.ExStyle | WsExToolWindow;
            if (enabled) style |= WsExTransparent | WsExNoActivate;
            else style &= ~(WsExTransparent | WsExNoActivate);
            try
            {
                if (ops.SetExStyle(handle, style)) return true;
            }
            catch { }
            try { ops.SetExStyle(handle, state.ExStyle); } catch { }
            return false;
        }
    }
}
