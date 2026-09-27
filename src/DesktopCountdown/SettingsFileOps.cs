using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace DesktopCountdown
{
    internal sealed class SettingsFileOps
    {
        private readonly string settingsPath;
        private readonly Action<string, string, string> replace;

        public SettingsFileOps(string settingsPath)
            : this(settingsPath, (source, destination, backup) => File.Replace(source, destination, backup, true))
        {
        }

        public SettingsFileOps(string settingsPath, Action<string, string, string> replace)
        {
            if (string.IsNullOrWhiteSpace(settingsPath)) throw new ArgumentException("Settings path is required.", "settingsPath");
            this.settingsPath = settingsPath;
            if (replace == null) throw new ArgumentNullException("replace");
            this.replace = replace;
        }

        public AppSettings Load()
        {
            FileStream stream;
            try { stream = File.OpenRead(settingsPath); }
            catch (FileNotFoundException)
            {
                if (File.Exists(settingsPath + ".tmp") || File.Exists(settingsPath + ".bak"))
                    throw new InvalidDataException("配置主文件不存在，但发现临时文件或备份。请检查并恢复设置文件。");
                return AppSettings.CreateDefault();
            }
            catch (DirectoryNotFoundException)
            {
                return AppSettings.CreateDefault();
            }

            using (stream)
            {
                try
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                    AppSettings value = serializer.ReadObject(stream) as AppSettings;
                    if (value == null) throw new InvalidDataException("配置文件不包含有效设置。");
                    value.Validate();
                    return value;
                }
                catch (SerializationException ex)
                {
                    throw new InvalidDataException("配置文件内容损坏，原文件已保留。", ex);
                }
            }
        }

        public void Save(AppSettings settings)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            settings.Validate();
            // Do not replace a damaged primary file and thereby overwrite a usable backup.
            if (File.Exists(settingsPath)) Load();
            string directory = Path.GetDirectoryName(settingsPath);
            Directory.CreateDirectory(directory);
            string temporaryPath = settingsPath + ".tmp";
            string backupPath = settingsPath + ".bak";

            using (FileStream stream = File.Create(temporaryPath))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AppSettings));
                serializer.WriteObject(stream, settings);
                stream.Flush(true);
            }

            if (File.Exists(settingsPath))
            {
                // A failed atomic replace must not fall back to overwriting the only good copy.
                replace(temporaryPath, settingsPath, backupPath);
            }
            else
            {
                File.Move(temporaryPath, settingsPath);
            }
        }
    }
}
