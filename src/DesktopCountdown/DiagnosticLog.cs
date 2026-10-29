using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopCountdown
{
    internal sealed class DiagnosticLog
    {
        private const int DefaultMaxBytes = 256 * 1024;
        private const int MaxSampleProcesses = 3;
        private const uint GuiResourceGdi = 0;
        private const uint GuiResourceUser = 1;
        private static readonly Regex SafeContext = new Regex(@"^[A-Za-z0-9_.-]{1,64}$", RegexOptions.Compiled);
        private static readonly DiagnosticLog Shared = new DiagnosticLog(SettingsStore.SettingsDirectory, DefaultMaxBytes);
        private readonly object gate = new object();
        private readonly string path;
        private readonly int maxBytes;

        internal DiagnosticLog(string directory, int limitBytes)
        {
            path = Path.Combine(directory, "diagnostics.log");
            maxBytes = Math.Max(512, limitBytes);
        }

        internal static bool Record(string context, Exception error)
        {
            return Shared.Write(context, error);
        }

        internal static bool RecordMemory(string context)
        {
            try
            {
                using (Process process = Process.GetCurrentProcess())
                {
                    long commitBytes = 0;
                    long commitLimitBytes = 0;
                    long availablePhysicalBytes = 0;
                    try
                    {
                        MemoryStatus status = new MemoryStatus();
                        status.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
                        if (GlobalMemoryStatusEx(ref status))
                        {
                            commitLimitBytes = (long)status.TotalPageFile;
                            commitBytes = (long)(status.TotalPageFile - status.AvailablePageFile);
                            availablePhysicalBytes = (long)status.AvailablePhysical;
                        }
                    }
                    catch { }

                    return Shared.WriteMemorySample(context, process.PrivateMemorySize64,
                        process.WorkingSet64, GC.GetTotalMemory(false), process.HandleCount,
                        commitBytes, commitLimitBytes, availablePhysicalBytes,
                        SafeGuiResources(process.Handle, GuiResourceGdi),
                        SafeGuiResources(process.Handle, GuiResourceUser), TopProcesses());
                }
            }
            catch { return false; }
        }

        internal bool WriteMemorySample(string context, long privateBytes, long workingBytes,
            long managedBytes, int handles, long commitBytes, long commitLimitBytes,
            long availablePhysicalBytes, int gdiObjects, int userObjects, string topProcesses)
        {
            try
            {
                string entry = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " +
                    SafeName(context) + " Memory private=" + privateBytes.ToString(CultureInfo.InvariantCulture) +
                    " working=" + workingBytes.ToString(CultureInfo.InvariantCulture) +
                    " managed=" + managedBytes.ToString(CultureInfo.InvariantCulture) +
                    " handles=" + handles.ToString(CultureInfo.InvariantCulture) +
                    " gdi=" + gdiObjects.ToString(CultureInfo.InvariantCulture) +
                    " user=" + userObjects.ToString(CultureInfo.InvariantCulture) +
                    " commit=" + commitBytes.ToString(CultureInfo.InvariantCulture) +
                    " limit=" + commitLimitBytes.ToString(CultureInfo.InvariantCulture) +
                    " availablePhysical=" + availablePhysicalBytes.ToString(CultureInfo.InvariantCulture) +
                    (topProcesses != null && topProcesses.Length > 0 ? " top=" + topProcesses : string.Empty) +
                    Environment.NewLine;
                return Append(entry);
            }
            catch { return false; }
        }

        internal bool Write(string context, Exception error)
        {
            if (error == null) return false;
            try
            {
                string typeName = error.GetType().FullName;
                if (typeName == null || typeName.Length > 120) typeName = "Exception";
                string frames = SafeFrames(error);
                string entry = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " +
                    SafeName(context) + " " + typeName + " 0x" +
                    error.HResult.ToString("X8", CultureInfo.InvariantCulture) +
                    (frames.Length == 0 ? string.Empty : " at=" + frames) + Environment.NewLine;
                return Append(entry);
            }
            catch { return false; }
        }

        internal bool WriteMemory(string context, long privateBytes, long workingBytes,
            long managedBytes, int handles)
        {
            try
            {
                string entry = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " +
                    SafeName(context) + " Memory private=" + privateBytes.ToString(CultureInfo.InvariantCulture) +
                    " working=" + workingBytes.ToString(CultureInfo.InvariantCulture) +
                    " managed=" + managedBytes.ToString(CultureInfo.InvariantCulture) +
                    " handles=" + handles.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;
                return Append(entry);
            }
            catch { return false; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr process, uint flags);

        private static int SafeGuiResources(IntPtr process, uint kind)
        {
            try { return (int)GetGuiResources(process, kind); }
            catch { return -1; }
        }

        private static string TopProcesses()
        {
            Process[] processes = null;
            try
            {
                processes = Process.GetProcesses();
                string[] names = new string[MaxSampleProcesses];
                long[] sizes = new long[MaxSampleProcesses];
                foreach (Process item in processes)
                {
                    if (item == null) continue;
                    long size;
                    string name;
                    try
                    {
                        size = item.WorkingSet64;
                        name = item.ProcessName;
                    }
                    catch { continue; }

                    int slot = -1;
                    for (int i = 0; i < names.Length; i++)
                        if (names[i] == null || size > sizes[i]) { slot = i; break; }
                    if (slot < 0) continue;
                    for (int i = names.Length - 1; i > slot; i--)
                    {
                        names[i] = names[i - 1];
                        sizes[i] = sizes[i - 1];
                    }
                    names[slot] = SafeProcessName(name);
                    sizes[slot] = size;
                }

                StringBuilder value = new StringBuilder();
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i] == null) continue;
                    if (value.Length > 0) value.Append('|');
                    value.Append(names[i]).Append(':').Append(sizes[i] / (1024 * 1024));
                }
                return value.ToString();
            }
            catch { return string.Empty; }
            finally
            {
                if (processes != null)
                    foreach (Process item in processes)
                        if (item != null) item.Dispose();
            }
        }

        private static string SafeProcessName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unknown";
            string value = Regex.Replace(name, "[^A-Za-z0-9._-]", "_");
            return value.Length > 32 ? value.Substring(0, 32) : value;
        }

        private static string SafeName(string context)
        {
            return context != null && SafeContext.IsMatch(context) ? context : "Unknown";
        }

        private static string SafeFrames(Exception error)
        {
            try
            {
                StackFrame[] frames = new StackTrace(error, false).GetFrames();
                if (frames == null) return string.Empty;
                StringBuilder value = new StringBuilder();
                for (int i = 0; i < frames.Length && i < 4; i++)
                {
                    MethodBase method = frames[i].GetMethod();
                    if (method == null) continue;
                    string name = (method.DeclaringType == null ? string.Empty : method.DeclaringType.FullName + ".") + method.Name;
                    name = Regex.Replace(name, "[^A-Za-z0-9_.+`]", "_");
                    if (name.Length > 60) name = name.Substring(0, 60);
                    if (value.Length > 0) value.Append('>');
                    value.Append(name);
                }
                return value.ToString();
            }
            catch { return string.Empty; }
        }

        private bool Append(string entry)
        {
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(entry);
                lock (gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > maxBytes)
                    {
                        File.Copy(path, path + ".1", true);
                        File.WriteAllText(path, string.Empty, new UTF8Encoding(false));
                    }
                    using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                        stream.Write(bytes, 0, bytes.Length);
                    return true;
                }
            }
            catch { return false; }
        }
    }
}
