using System;
using System.Globalization;
using System.IO;
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

        internal bool Write(string context, Exception error)
        {
            if (error == null) return false;
            string safeContext = context != null && SafeContext.IsMatch(context) ? context : "Unknown";
            string typeName = error.GetType().FullName;
            if (typeName == null || typeName.Length > 120) typeName = "Exception";
            string entry = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " +
                safeContext + " " + typeName + " 0x" +
                error.HResult.ToString("X8", CultureInfo.InvariantCulture) + Environment.NewLine;
            byte[] bytes = new UTF8Encoding(false).GetBytes(entry);
            lock (gate)
            {
                try
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
                catch
                {
                    return false;
                }
            }
        }
    }
}
