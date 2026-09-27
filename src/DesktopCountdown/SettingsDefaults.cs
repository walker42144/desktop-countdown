using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DesktopCountdown
{
    internal static class SettingsDefaults
    {
        internal const string DefaultTheme = "MinimalGlass";
        internal const string DefaultCountdown = "{days:3} 天  {hours:2}:{minutes:2}:{seconds:2}";
        internal const string DefaultClock = "yyyy-MM-dd  HH:mm:ss";

        internal const string NineGrid = "NineGrid";
        internal const string Orbit = "Orbit";
        internal const string Wander = "Wander";
        internal const string Tide = "Tide";
        internal const string EdgeWalk = "EdgeWalk";
        internal const string FarNear = "FarNear";

        internal static readonly Regex CountdownToken = new Regex(@"\{([A-Za-z]+)(?::([1-4]))?\}", RegexOptions.Compiled);

        internal static bool TryValidateCountdown(string format, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(format) || format.Length > 160)
            {
                error = "倒计时格式应为 1～160 个字符。";
                return false;
            }
            string remainder = CountdownToken.Replace(format, string.Empty);
            if (remainder.IndexOf('{') >= 0 || remainder.IndexOf('}') >= 0)
            {
                error = "占位符不完整或不受支持。";
                return false;
            }
            foreach (Match match in CountdownToken.Matches(format))
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
            if (CountdownToken.Matches(format).Count == 0)
            {
                error = "至少需要一个倒计时占位符。";
                return false;
            }
            return true;
        }

        internal static bool TryValidateClock(string format, out string error)
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

        internal static bool IsMotionMode(string mode)
        {
            return mode == NineGrid || mode == Orbit || mode == Wander ||
                mode == Tide || mode == EdgeWalk || mode == FarNear;
        }
    }
}
