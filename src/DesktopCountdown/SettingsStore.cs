using System;
using System.IO;

namespace DesktopCountdown
{
    public static class SettingsStore
    {
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
            // Retain the legacy registry-only contract for external callers.
            StartupBackend.SetLegacyRegistry(enabled ? "\"" + executablePath + "\"" : null);
        }
    }
}
