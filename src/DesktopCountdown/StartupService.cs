using System;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Xml;

namespace DesktopCountdown
{
    public static class StartupService
    {
        public static void Apply(bool enabled, string mode, string executable)
        {
            ApplyWithBackend(new StartupBackend(), enabled, mode, executable, null);
        }

        internal static void ApplyAndSave(bool enabled, string mode, string executable, Action saveSettings)
        {
            ApplyWithBackend(new StartupBackend(), enabled, mode, executable, saveSettings);
        }

        internal static void ApplyWithBackend(StartupBackend backend, bool enabled, string mode,
            string executable, Action saveSettings)
        {
            if (backend == null) throw new ArgumentNullException("backend");
            StartupState previous = backend.Capture();
            try
            {
                if (enabled && mode == "Task")
                {
                    backend.SetRegistry(null);
                    backend.SetTask(CreateTaskXml(executable));
                }
                else
                {
                    backend.RemoveTask();
                    backend.SetRegistry(enabled ? "\"" + executable + "\"" : null);
                }
                VerifyDesired(backend.Capture(), enabled, mode, executable);
                if (saveSettings != null) saveSettings();
            }
            catch (Exception ex)
            {
                string recovery = Restore(backend, previous);
                throw new InvalidOperationException("登录启动或设置保存失败：" + ex.Message +
                    "；补偿结果：" + recovery + "；当前状态：" + DescribeState(backend), ex);
            }
        }

        private static string CreateTaskXml(string executable)
        {
            string temporary = Path.Combine(Path.GetTempPath(), "DesktopCountdown-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                WriteTaskXml(temporary, executable);
                return File.ReadAllText(temporary, Encoding.Unicode);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void VerifyDesired(StartupState actual, bool enabled, string mode, string executable)
        {
            string expectedRegistry = enabled && mode != "Task" ? "\"" + executable + "\"" : null;
            bool taskExpected = enabled && mode == "Task";
            if (!string.Equals(actual.RegistryCommand, expectedRegistry, StringComparison.Ordinal) ||
                (actual.TaskXml != null) != taskExpected ||
                (taskExpected && !string.Equals(TaskCommand(actual.TaskXml), executable, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("读取到的实际登录启动状态与请求不一致。");
        }

        private static string TaskCommand(string xml)
        {
            XmlDocument document = new XmlDocument();
            document.LoadXml(xml);
            XmlNode command = document.SelectSingleNode("//*[local-name()='Actions']/*[local-name()='Exec']/*[local-name()='Command']");
            return command == null ? null : command.InnerText;
        }

        private static string Restore(StartupBackend backend, StartupState previous)
        {
            string taskError = null;
            string registryError = null;
            try
            {
                if (previous.TaskXml == null) backend.RemoveTask();
                else backend.SetTask(previous.TaskXml);
            }
            catch (Exception ex) { taskError = ex.Message; }
            try { backend.SetRegistry(previous.RegistryCommand); }
            catch (Exception ex) { registryError = ex.Message; }

            if (taskError != null || registryError != null)
                return "未完全恢复（任务：" + (taskError ?? "已恢复") + "；注册表：" + (registryError ?? "已恢复") + "）";
            try
            {
                StartupState actual = backend.Capture();
                if (string.Equals(actual.RegistryCommand, previous.RegistryCommand, StringComparison.Ordinal) &&
                    string.Equals(actual.TaskXml, previous.TaskXml, StringComparison.Ordinal))
                    return "已恢复原状态";
                return "读取状态与原状态不一致，需人工核对";
            }
            catch (Exception ex) { return "无法核验恢复结果：" + ex.Message; }
        }

        private static string DescribeState(StartupBackend backend)
        {
            try
            {
                StartupState actual = backend.Capture();
                return "注册表=" + (actual.RegistryCommand ?? "无") + "，计划任务=" +
                    (actual.TaskXml == null ? "无" : (TaskCommand(actual.TaskXml) ?? "存在，命令未识别"));
            }
            catch (Exception ex) { return "无法读取（" + ex.Message + "）"; }
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

    }
}
