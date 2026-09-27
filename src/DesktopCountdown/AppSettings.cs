using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace DesktopCountdown
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember(Order = 1)] public bool HasConfiguredTarget { get; set; }
        [DataMember(Order = 2)] public string Title { get; set; }
        [DataMember(Order = 3)] public string TargetLocal { get; set; }
        [DataMember(Order = 4)] public string TitleFontFamily { get; set; }
        [DataMember(Order = 5)] public string DigitFontFamily { get; set; }
        [DataMember(Order = 6)] public double TitleFontSize { get; set; }
        [DataMember(Order = 7)] public double DigitFontSize { get; set; }
        [DataMember(Order = 8)] public bool ShowSeconds { get; set; }
        [DataMember(Order = 9)] public bool AutoColor { get; set; }
        [DataMember(Order = 10)] public string ManualForeground { get; set; }
        [DataMember(Order = 11)] public bool Locked { get; set; }
        [DataMember(Order = 12)] public bool AlwaysOnTop { get; set; }
        [DataMember(Order = 13)] public bool DesktopMode { get; set; }
        [DataMember(Order = 14)] public bool StartWithWindows { get; set; }
        [DataMember(Order = 15)] public double Left { get; set; }
        [DataMember(Order = 16)] public double Top { get; set; }
        [DataMember(Order = 17)] public string NtpServer { get; set; }
        [DataMember(Order = 18)] public string ThemeName { get; set; }
        [DataMember(Order = 19)] public string CountdownFormat { get; set; }
        [DataMember(Order = 20)] public string ClockFormat { get; set; }
        [DataMember(Order = 21)] public bool MotionEnabled { get; set; }
        [DataMember(Order = 22)] public string MotionMode { get; set; }
        [DataMember(Order = 23)] public int MotionIntervalSeconds { get; set; }
        [DataMember(Order = 24)] public int MotionAmplitudePixels { get; set; }
        [DataMember(Order = 25)] public int MotionTransitionMilliseconds { get; set; }
        [DataMember(Order = 26)] public bool VisualBreathing { get; set; }
        [DataMember(Order = 27)] public string StartupMode { get; set; }

        public static AppSettings CreateDefault()
        {
            DateTime initialTarget = DateTime.Now.Date.AddDays(30).AddHours(9);
            return new AppSettings
            {
                HasConfiguredTarget = false,
                Title = "距离目标时刻还有",
                TargetLocal = initialTarget.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                TitleFontFamily = "MiSans",
                DigitFontFamily = "Bahnschrift",
                TitleFontSize = 22,
                DigitFontSize = 84,
                ShowSeconds = true,
                AutoColor = true,
                ManualForeground = "#FFF5F7FA",
                Locked = false,
                AlwaysOnTop = false,
                DesktopMode = false,
                StartWithWindows = false,
                Left = 120,
                Top = 120,
                NtpServer = "time.windows.com"
                ,ThemeName = SettingsDefaults.DefaultTheme,
                CountdownFormat = SettingsDefaults.DefaultCountdown,
                ClockFormat = SettingsDefaults.DefaultClock,
                MotionEnabled = false,
                MotionMode = SettingsDefaults.NineGrid,
                MotionIntervalSeconds = 120,
                MotionAmplitudePixels = 6,
                MotionTransitionMilliseconds = 0,
                VisualBreathing = false,
                StartupMode = "Registry"
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Title)) Title = "距离目标时刻还有";
            if (string.IsNullOrWhiteSpace(TargetLocal))
                TargetLocal = DateTime.Now.Date.AddDays(30).AddHours(9).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(TitleFontFamily)) TitleFontFamily = "MiSans";
            if (string.IsNullOrWhiteSpace(DigitFontFamily)) DigitFontFamily = "Bahnschrift";
            if (TitleFontSize < 12 || TitleFontSize > 72) TitleFontSize = 22;
            if (DigitFontSize < 32 || DigitFontSize > 240) DigitFontSize = 84;
            if (string.IsNullOrWhiteSpace(ManualForeground)) ManualForeground = "#FFF5F7FA";
            if (string.IsNullOrWhiteSpace(NtpServer)) NtpServer = "time.windows.com";
            if (string.IsNullOrWhiteSpace(ThemeName)) ThemeName = SettingsDefaults.DefaultTheme;
            if (double.IsNaN(Left) || double.IsInfinity(Left)) Left = 120;
            if (double.IsNaN(Top) || double.IsInfinity(Top)) Top = 120;
            string ignored;
            if (!SettingsDefaults.TryValidateCountdown(CountdownFormat, out ignored)) CountdownFormat = SettingsDefaults.DefaultCountdown;
            if (!SettingsDefaults.TryValidateClock(ClockFormat, out ignored)) ClockFormat = SettingsDefaults.DefaultClock;
            if (!SettingsDefaults.IsMotionMode(MotionMode)) MotionMode = SettingsDefaults.NineGrid;
            if (MotionIntervalSeconds < 30 || MotionIntervalSeconds > 1800) MotionIntervalSeconds = 120;
            if (MotionAmplitudePixels < 1 || MotionAmplitudePixels > 20) MotionAmplitudePixels = 6;
            if (MotionTransitionMilliseconds < 0 || MotionTransitionMilliseconds > 2000) MotionTransitionMilliseconds = 0;
            if (StartupMode != "Task") StartupMode = "Registry";
        }

        public DateTime GetTargetLocal()
        {
            DateTime value;
            if (DateTime.TryParseExact(TargetLocal, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out value))
                return DateTime.SpecifyKind(value, DateTimeKind.Local);

            if (DateTime.TryParse(TargetLocal, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out value))
                return DateTime.SpecifyKind(value, DateTimeKind.Local);

            return DateTime.Now;
        }

        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }
    }
}
