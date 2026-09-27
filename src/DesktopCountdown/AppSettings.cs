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
                ,ThemeName = ThemeCatalog.DefaultTheme
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
            if (string.IsNullOrWhiteSpace(ThemeName)) ThemeName = ThemeCatalog.DefaultTheme;
            if (double.IsNaN(Left) || double.IsInfinity(Left)) Left = 120;
            if (double.IsNaN(Top) || double.IsInfinity(Top)) Top = 120;
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
