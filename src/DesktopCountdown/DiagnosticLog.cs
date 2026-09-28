using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopCountdown
{
    internal sealed class DiagnosticLog
    {
        private const int DefaultMaxBytes = 256 * 1024;
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
                    return Shared.WriteMemory(context, process.PrivateMemorySize64,
                        process.WorkingSet64, GC.GetTotalMemory(false), process.HandleCount);
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
