using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.Serialization.Json;

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
            try
            {
                if (!File.Exists(SettingsPath)) return AppSettings.CreateDefault();
                using (FileStream stream = File.OpenRead(SettingsPath))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    AppSettings value = serializer.ReadObject(stream) as AppSettings;
                    if (value == null) return AppSettings.CreateDefault();
                    value.Validate();
                    return value;
                }
            }
            catch
            {
                return AppSettings.CreateDefault();
            }
        }

        public static void Save(AppSettings settings)
        {
            settings.Validate();
            Directory.CreateDirectory(SettingsDirectory);
            string temporaryPath = SettingsPath + ".tmp";
            string backupPath = SettingsPath + ".bak";

            using (FileStream stream = File.Create(temporaryPath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                serializer.WriteObject(stream, settings);
                stream.Flush(true);
            }

            if (File.Exists(SettingsPath))
            {
                try
                {
                    File.Replace(temporaryPath, SettingsPath, backupPath, true);
                }
                catch
                {
                    File.Copy(temporaryPath, SettingsPath, true);
                    File.Delete(temporaryPath);
                }
            }
            else
            {
                File.Move(temporaryPath, SettingsPath);
            }
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
