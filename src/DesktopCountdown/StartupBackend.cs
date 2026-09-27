using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopCountdown
{
    internal sealed class StartupState
    {
        public string RegistryCommand { get; private set; }
        public string TaskXml { get; private set; }

        public StartupState(string registryCommand, string taskXml)
        {
            RegistryCommand = registryCommand;
            TaskXml = taskXml;
        }
    }

    internal sealed class StartupBackend
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValue = "DesktopCountdown";
        private const string TaskName = "DesktopCountdown_UserLogon";
        private readonly Func<object> readRegistry;
        private readonly Action<string> writeRegistry;
        private readonly Func<object> readTask;
        private readonly Action<string> writeTask;
        private readonly Action deleteTask;

        public StartupBackend()
            : this(() => ReadNativeRegistry(), WriteNativeRegistry, () => ReadNativeTask(), WriteNativeTask, DeleteNativeTask)
        {
        }

        public StartupBackend(Func<object> readRegistry, Action<string> writeRegistry,
            Func<object> readTask, Action<string> writeTask, Action deleteTask)
        {
            if (readRegistry == null) throw new ArgumentNullException("readRegistry");
            if (writeRegistry == null) throw new ArgumentNullException("writeRegistry");
            if (readTask == null) throw new ArgumentNullException("readTask");
            if (writeTask == null) throw new ArgumentNullException("writeTask");
            if (deleteTask == null) throw new ArgumentNullException("deleteTask");
            this.readRegistry = readRegistry;
            this.writeRegistry = writeRegistry;
            this.readTask = readTask;
            this.writeTask = writeTask;
            this.deleteTask = deleteTask;
        }

        public StartupState Capture()
        {
            return new StartupState(readRegistry() as string, readTask() as string);
        }

        public void SetRegistry(string command) { writeRegistry(command); }
        public void SetTask(string xml) { writeTask(xml); }
        public void RemoveTask()
        {
            if (readTask() != null) deleteTask();
        }

        private static string ReadNativeRegistry()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key == null ? null : key.GetValue(RunValue) as string;
        }

        private static void WriteNativeRegistry(string command)
        {
            WriteRegistry(command, true);
        }

        internal static void SetLegacyRegistry(string command)
        {
            WriteRegistry(command, false);
        }

        private static void WriteRegistry(string command, bool failWhenUnavailable)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null)
                {
                    if (failWhenUnavailable) throw new InvalidOperationException("无法访问当前用户的启动项。");
                    return;
                }
                if (command == null) key.DeleteValue(RunValue, false);
                else key.SetValue(RunValue, command);
            }
        }

        private static string ReadNativeTask()
        {
            object service = null;
            object folder = null;
            object task = null;
            try
            {
                Type type = Type.GetTypeFromProgID("Schedule.Service", true);
                service = Activator.CreateInstance(type);
                InvokeCom(service, "Connect");
                folder = InvokeCom(service, "GetFolder", @"\");
                try { task = InvokeCom(folder, "GetTask", TaskName); }
                catch (Exception ex)
                {
                    if (IsMissingTask(ex)) return null;
                    throw;
                }
                return (string)task.GetType().InvokeMember("Xml", BindingFlags.GetProperty,
                    null, task, null);
            }
            finally
            {
                ReleaseCom(task);
                ReleaseCom(folder);
                ReleaseCom(service);
            }
        }

        private static bool IsMissingTask(Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
                if (current.HResult == unchecked((int)0x80070002)) return true;
            return false;
        }

        private static object InvokeCom(object target, string member, params object[] arguments)
        {
            return target.GetType().InvokeMember(member, BindingFlags.InvokeMethod,
                null, target, arguments);
        }

        private static void ReleaseCom(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
        }

        private static void WriteNativeTask(string xml)
        {
            string temporary = Path.Combine(Path.GetTempPath(), "DesktopCountdown-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(temporary, xml, Encoding.Unicode);
                RunSchtasks("/Create /F /TN \"" + TaskName + "\" /XML \"" + temporary + "\"");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static void DeleteNativeTask()
        {
            RunSchtasks("/Delete /F /TN \"" + TaskName + "\"");
        }

        private static void RunSchtasks(string arguments)
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
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("无法配置登录启动任务：" + error + output);
            }
        }
    }
}
