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
                if (handle != windowHandle)
                {
                    ClearAttachment();
                }
                else if (current == null)
                {
                    return true;
                }
                else if (current.Parent != hostHandle)
                {
                    if (original != null && !Restore(handle, original)) return true;
                    ClearAttachment();
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
            NativeWindowSnapshot hostBounds = Capture(host);
            if (hostBounds == null || !ContainsCenter(hostBounds, before)) return false;
            try { if (!ops.DpiCompatible(handle, host)) return false; }
            catch { return false; }

            try
            {
                if (!ops.SetStyle(handle, (before.Style | WsChild) & ~WsPopup))
                    throw new InvalidOperationException("SetStyle");
                if (!ops.SetParent(handle, host))
                    throw new InvalidOperationException("SetParent");
                if (!ops.SetPosition(handle, host, before.Left, before.Top, before.Width, before.Height))
                    throw new InvalidOperationException("SetPosition");
                NativeWindowSnapshot after = Capture(handle);
                if (after == null || after.Parent != host || !ContainsCenter(hostBounds, after))
                    throw new InvalidOperationException("HostGeometry");
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
            if (current == null) return true;
            if (original == null) return true;
            try
            {
                if (!ops.SetParent(handle, original.Parent))
                    throw new InvalidOperationException("SetParent");
                if (!ops.SetStyle(handle, original.Style))
                    throw new InvalidOperationException("SetStyle");
                if (!ops.SetPosition(handle, original.Parent,
                    current.Left, current.Top, current.Width, current.Height))
                    throw new InvalidOperationException("SetPosition");
                ClearAttachment();
                return false;
            }
            catch
            {
                Restore(handle, original);
                NativeWindowSnapshot recovered = Capture(handle);
                if (recovered != null && recovered.Parent == original.Parent && recovered.Style == original.Style)
                {
                    ClearAttachment();
                    return false;
                }
                return true;
            }
        }

        private void ClearAttachment()
        {
            attached = false;
            original = null;
            hostHandle = IntPtr.Zero;
        }

        private static bool ContainsCenter(NativeWindowSnapshot host, NativeWindowSnapshot child)
        {
            if (host.Width <= 0 || host.Height <= 0 || child.Width <= 0 || child.Height <= 0) return false;
            long x = (long)child.Left + child.Width / 2;
            long y = (long)child.Top + child.Height / 2;
            return x >= host.Left && x < (long)host.Left + host.Width &&
                y >= host.Top && y < (long)host.Top + host.Height;
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
