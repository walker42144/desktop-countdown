using Microsoft.Win32;
using System;
using System.IO;

namespace DesktopCountdown
{
    public static class SettingsStore
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "DesktopCountdown";

        public static string SettingsDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCountdown");
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(SettingsDirectory, "settings.json"); }
        }

        public static AppSettings Load()
        {
            return new SettingsFileOps(SettingsPath).Load();
        }

        public static void Save(AppSettings settings)
        {
            new SettingsFileOps(SettingsPath).Save(settings);
        }

        public static void ApplyStartupSetting(bool enabled, string executablePath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null) return;
                if (enabled)
                    key.SetValue(RunValueName, "\"" + executablePath + "\"");
                else
                    key.DeleteValue(RunValueName, false);
            }
        }
    }
}
