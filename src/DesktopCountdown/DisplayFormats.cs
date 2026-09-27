using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopCountdown
{
    public static class DisplayFormats
    {
        public const string DefaultCountdown = SettingsDefaults.DefaultCountdown;
        public const string DefaultClock = SettingsDefaults.DefaultClock;

        private static readonly Regex Token = SettingsDefaults.CountdownToken;

        public static string Countdown(string format, TimeSpan duration, bool elapsed, bool showSeconds)
        {
            string template = string.IsNullOrEmpty(format) ? DefaultCountdown : format;
            if (!showSeconds && template == DefaultCountdown)
                template = "{days:3} 天  {hours:2}:{minutes:2}";
            long days = (long)Math.Floor(duration.TotalDays);
            long totalHours = (long)Math.Floor(duration.TotalHours);
            return Token.Replace(template, match =>
            {
                string name = match.Groups[1].Value;
                if (name == "sign") return elapsed ? "+" : string.Empty;
                long value;
                switch (name)
                {
                    case "days": value = days; break;
                    case "hours": value = duration.Hours; break;
                    case "minutes": value = duration.Minutes; break;
                    case "seconds": value = duration.Seconds; break;
                    case "totalHours": value = totalHours; break;
                    default: throw new FormatException("未知倒计时占位符：" + name);
                }
                int width = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
                if (name == "days" && template == DefaultCountdown) width = 3;
                string number = value.ToString("D" + width, CultureInfo.InvariantCulture);
                return name == "days" && elapsed && template == DefaultCountdown ? "+" + number : number;
            });
        }

        public static bool TryValidateCountdown(string format, out string error)
        {
            return SettingsDefaults.TryValidateCountdown(format, out error);
        }

        public static bool TryValidateClock(string format, out string error)
        {
            return SettingsDefaults.TryValidateClock(format, out error);
        }

        public static bool ClockNeedsSeconds(string format)
        {
            string value = format ?? DefaultClock;
            bool quoted = false;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\\') { i++; continue; }
                if (value[i] == '\'' || value[i] == '"') { quoted = !quoted; continue; }
                if (!quoted && (value[i] == 's' || value[i] == 'f' || value[i] == 'F')) return true;
            }
            return false;
        }
    }
}
