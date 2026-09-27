using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DesktopCountdown
{
    public static class DisplayFormats
    {
        public const string DefaultCountdown = "{days:3} 天  {hours:2}:{minutes:2}:{seconds:2}";
        public const string DefaultClock = "yyyy-MM-dd  HH:mm:ss";

        private static readonly Regex Token = new Regex(@"\{([A-Za-z]+)(?::([1-4]))?\}", RegexOptions.Compiled);

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
            error = null;
            if (string.IsNullOrWhiteSpace(format) || format.Length > 160)
            {
                error = "倒计时格式应为 1～160 个字符。";
                return false;
            }
            string remainder = Token.Replace(format, string.Empty);
            if (remainder.IndexOf('{') >= 0 || remainder.IndexOf('}') >= 0)
            {
                error = "占位符不完整或不受支持。";
                return false;
            }
            foreach (Match match in Token.Matches(format))
            {
                string name = match.Groups[1].Value;
                if (name != "days" && name != "hours" && name != "minutes" && name != "seconds" && name != "totalHours" && name != "sign")
                {
                    error = "未知占位符：" + name;
                    return false;
                }
                if (name == "sign" && match.Groups[2].Success)
                {
                    error = "{sign} 不能设置位数。";
                    return false;
                }
            }
            if (Token.Matches(format).Count == 0)
            {
                error = "至少需要一个倒计时占位符。";
                return false;
            }
            return true;
        }

        public static bool TryValidateClock(string format, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(format) || format.Length > 100)
            {
                error = "日期时间格式应为 1～100 个字符。";
                return false;
            }
            try
            {
                DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
                return true;
            }
            catch (FormatException)
            {
                error = "日期时间格式无效，请检查引号和格式字符。";
                return false;
            }
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
