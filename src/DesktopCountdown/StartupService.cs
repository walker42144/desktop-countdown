using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace DesktopCountdown
{
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "DesktopCountdown";
        private const string TaskName = "DesktopCountdown_UserLogon";

        public static void Apply(bool enabled, string mode, string executable)
        {
            if (enabled && mode == "Task")
            {
                RegisterTask(executable);
                SetRegistry(false, executable);
            }
            else
            {
                SetRegistry(enabled, executable);
                RemoveTaskIfPresent();
            }
        }

        private static void SetRegistry(bool enabled, string executable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) throw new InvalidOperationException("无法访问当前用户的启动项。");
                if (enabled) key.SetValue(RunValue, "\"" + executable + "\"");
                else key.DeleteValue(RunValue, false);
            }
        }

        private static void RegisterTask(string executable)
        {
            string temporary = Path.Combine(Path.GetTempPath(), "DesktopCountdown-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                WriteTaskXml(temporary, executable);
                RunSchtasks("/Create /F /TN \"" + TaskName + "\" /XML \"" + temporary + "\"");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void WriteTaskXml(string path, string executable)
        {
            // Task Scheduler consumes task XML through a Unicode COM API. Keep the
            // file declaration and byte encoding consistent with that interface.
            XmlWriterSettings options = new XmlWriterSettings { Encoding = Encoding.Unicode, Indent = true };
            using (XmlWriter writer = XmlWriter.Create(path, options))
            {
                string ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
                string user = WindowsIdentity.GetCurrent().User.Value;
                writer.WriteStartDocument();
                writer.WriteStartElement("Task", ns);
                writer.WriteAttributeString("version", "1.2");
                writer.WriteStartElement("Triggers", ns);
                writer.WriteStartElement("LogonTrigger", ns);
                writer.WriteElementString("Enabled", ns, "true");
                writer.WriteElementString("UserId", ns, user);
                writer.WriteElementString("Delay", ns, "PT10S");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteStartElement("Principals", ns);
                writer.WriteStartElement("Principal", ns);
                writer.WriteAttributeString("id", "Author");
                writer.WriteElementString("UserId", ns, user);
                writer.WriteElementString("LogonType", ns, "InteractiveToken");
                writer.WriteElementString("RunLevel", ns, "LeastPrivilege");
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteStartElement("Settings", ns);
                writer.WriteElementString("MultipleInstancesPolicy", ns, "IgnoreNew");
                writer.WriteElementString("DisallowStartIfOnBatteries", ns, "false");
                writer.WriteElementString("StopIfGoingOnBatteries", ns, "false");
                writer.WriteElementString("ExecutionTimeLimit", ns, "PT0S");
                writer.WriteElementString("Enabled", ns, "true");
                writer.WriteEndElement();
                writer.WriteStartElement("Actions", ns);
                writer.WriteAttributeString("Context", "Author");
                writer.WriteStartElement("Exec", ns);
                writer.WriteElementString("Command", ns, executable);
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private static void RemoveTaskIfPresent()
        {
            if (RunSchtasks("/Query /TN \"" + TaskName + "\"", true) == 0)
                RunSchtasks("/Delete /F /TN \"" + TaskName + "\"");
        }

        private static int RunSchtasks(string arguments, bool ignoreFailure = false)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("schtasks.exe", arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0 && !ignoreFailure)
                    throw new InvalidOperationException("无法配置登录启动任务：" + error + output);
                return process.ExitCode;
            }
        }
    }
}
