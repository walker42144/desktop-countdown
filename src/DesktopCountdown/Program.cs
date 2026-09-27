using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

[assembly: AssemblyTitle("桌面倒计时")]
[assembly: AssemblyDescription("简洁、准确、可随壁纸自适应的 Windows 桌面倒计时")]
[assembly: AssemblyCompany("walker42144")]
[assembly: AssemblyProduct("Desktop Countdown")]
[assembly: AssemblyVersion("0.3.0.0")]
[assembly: AssemblyFileVersion("0.3.0.0")]
[assembly: ComVisible(false)]

namespace DesktopCountdown
{
    public static class Program
    {
        private const string MutexName = "Local.DesktopCountdown.6E6D2F08-3E46-48F6-A4AF-934A99BE6E44";

        [STAThread]
        public static int Main(string[] args)
        {
            if (args != null && Array.Exists(args, a => string.Equals(a, "--smoke-test", StringComparison.OrdinalIgnoreCase)))
                return RunSmokeTest();
            if (args != null && Array.Exists(args, a => string.Equals(a, "--render-previews", StringComparison.OrdinalIgnoreCase)))
                return RenderPreviews();
            if (args != null && Array.Exists(args, a => string.Equals(a, "--render-settings", StringComparison.OrdinalIgnoreCase)))
                return RenderSettings();
            if (args != null && Array.Exists(args, a => string.Equals(a, "--clock-test", StringComparison.OrdinalIgnoreCase)))
                return TestNetworkClock();
            if (args != null && Array.Exists(args, a => string.Equals(a, "--settings-preview", StringComparison.OrdinalIgnoreCase)))
            {
                Application previewApplication = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                SettingsWindow preview = new SettingsWindow(LoadSettingsWithNotice(), "设置页预览 · 不保存配置");
                foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
                    if (!screen.Primary) { preview.OpenOnScreen(screen); break; }
                previewApplication.MainWindow = preview;
                preview.Show();
                return previewApplication.Run();
            }

            bool created;
            using (Mutex mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    MessageBox.Show("桌面倒计时已经在运行，请使用系统托盘图标进行设置。", "桌面倒计时",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return 0;
                }

                Application application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                application.DispatcherUnhandledException += UnhandledException;

                AppSettings settings = LoadSettingsWithNotice();
                MainWindow window = new MainWindow(settings);
                application.MainWindow = window;
                window.Show();
                return application.Run();
            }
        }

        private static AppSettings LoadSettingsWithNotice()
        {
            try { return SettingsStore.Load(); }
            catch (Exception ex)
            {
                MessageBox.Show("无法读取设置：" + ex.Message + "\n\n本次将使用默认设置。原设置文件、临时文件和备份均未删除；请检查：\n" +
                    SettingsStore.SettingsPath + "\n" + SettingsStore.SettingsPath + ".bak",
                    "桌面倒计时", MessageBoxButton.OK, MessageBoxImage.Warning);
                return AppSettings.CreateDefault();
            }
        }

        private static void UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(SettingsStore.SettingsDirectory);
                File.AppendAllText(Path.Combine(SettingsStore.SettingsDirectory, "error.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + e.Exception + Environment.NewLine + Environment.NewLine);
            }
            catch { }

            MessageBox.Show("程序遇到错误，详情已记录到本地日志。\n\n" + e.Exception.Message,
                "桌面倒计时", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private static int RunSmokeTest()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-test.txt");
            try
            {
                AppSettings defaults = AppSettings.CreateDefault();
                defaults.Validate();
                VerifyEdgeSnapping();
                ThemeDefinition theme = ThemeCatalog.Get(defaults.ThemeName);
                VerifyThemes();
                VerifyDisplayAndMotion();
                VerifySettingsStyles();
                Rectangle area = new Rectangle(100, 100, 480, 160);
                WallpaperAppearance wallpaper = WallpaperColorService.Analyze(area);
                string result = "OK" + Environment.NewLine +
                                "Version=" + Assembly.GetExecutingAssembly().GetName().Version + Environment.NewLine +
                                "DefaultTheme=" + theme.DisplayName + Environment.NewLine +
                                "TitleFont=" + defaults.TitleFontFamily + Environment.NewLine +
                                "DigitFont=" + defaults.DigitFontFamily + Environment.NewLine +
                                "Wallpaper=" + wallpaper.Description + Environment.NewLine +
                                "Themes=" + ThemeCatalog.All().Count;
                File.WriteAllText(path, result);
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(path, "FAILED" + Environment.NewLine + ex);
                return 1;
            }
        }

        private static void VerifyEdgeSnapping()
        {
            double left = EdgeSnapCalculator.SnapAxis(0, 500, 0, 1920, 18, 32);
            double right = EdgeSnapCalculator.SnapAxis(1420, 500, 0, 1920, 18, 32);
            double middle = EdgeSnapCalculator.SnapAxis(600, 500, 0, 1920, 18, 32);
            if (Math.Abs(left - (-18)) > 0.01 || Math.Abs(right - 1438) > 0.01 || Math.Abs(middle - 600) > 0.01)
                throw new InvalidOperationException("屏幕边缘吸附计算未通过自检");
        }

        private static void VerifyThemes()
        {
            System.Collections.Generic.HashSet<string> keys = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ThemeDefinition theme in ThemeCatalog.All())
            {
                if (string.IsNullOrWhiteSpace(theme.Key) || string.IsNullOrWhiteSpace(theme.DisplayName) || !keys.Add(theme.Key))
                    throw new InvalidOperationException("主题目录包含空值或重复键");
            }
            if (keys.Count != 10) throw new InvalidOperationException("应包含10套主题");
            using (System.Drawing.Icon icon = IconFactory.CreateClockIcon())
            {
                if (icon == null || icon.Width < 16 || icon.Height < 16)
                    throw new InvalidOperationException("应用图标未正确嵌入");
            }
        }

        private static void VerifyDisplayAndMotion()
        {
            string error;
            if (!DisplayFormats.TryValidateCountdown(DisplayFormats.DefaultCountdown, out error) ||
                !DisplayFormats.TryValidateClock(DisplayFormats.DefaultClock, out error))
                throw new InvalidOperationException("默认显示格式无效：" + error);
            string sample = DisplayFormats.Countdown(DisplayFormats.DefaultCountdown,
                new TimeSpan(83, 12, 36, 20), false, true);
            if (sample != "083 天  12:36:20") throw new InvalidOperationException("倒计时默认格式发生变化");
            string compact = DisplayFormats.Countdown("{sign}{totalHours}h {minutes:2}m",
                new TimeSpan(2, 3, 4, 5), true, false);
            if (compact != "+51h 04m") throw new InvalidOperationException("自定义倒计时格式异常");
            if (DisplayFormats.TryValidateCountdown("{unknown}", out error) ||
                DisplayFormats.ClockNeedsSeconds("yyyy-MM-dd HH:mm"))
                throw new InvalidOperationException("格式校验异常");
            if (MotionPlanner.Modes.Length != 6) throw new InvalidOperationException("位移方式数量异常");
            foreach (string mode in MotionPlanner.Modes)
            {
                System.Windows.Point point = MotionPlanner.Next(mode, 1, 6, new Random(1),
                    new System.Windows.Point(0, 0), false, false);
                if (double.IsNaN(point.X) || double.IsNaN(point.Y))
                    throw new InvalidOperationException("位移路径无效：" + mode);
            }
            System.Windows.Point constrained = MotionPlanner.Constrain(new System.Windows.Point(-18, -18),
                new System.Windows.Point(-20, 10), new System.Windows.Size(500, 200),
                new System.Windows.Rect(0, 0, 1920, 1080), 18);
            if (Math.Abs(constrained.X - (-18)) > 0.01 || Math.Abs(constrained.Y - (-8)) > 0.01)
                throw new InvalidOperationException("屏幕边界位移约束异常");
            System.Windows.Point visible = MotionPlanner.EnsureVisibleMove(
                new System.Windows.Point(-18, -18), new System.Windows.Point(-10, -10),
                new System.Windows.Point(0, 0), new System.Windows.Size(500, 200),
                new System.Windows.Rect(0, 0, 1920, 1080), 18, 10);
            if (Math.Abs(visible.X) + Math.Abs(visible.Y) < 1)
                throw new InvalidOperationException("贴边时位移不能保持可见");
        }

        private static void VerifySettingsStyles()
        {
            SettingsWindow window = new SettingsWindow(AppSettings.CreateDefault(), "校时状态测试");
            try
            {
                if (!window.Resources.Contains(typeof(System.Windows.Controls.CheckBox)) ||
                    !window.Resources.Contains(typeof(System.Windows.Controls.Primitives.ScrollBar)) ||
                    !window.Resources.Contains("FluentAccent"))
                    throw new InvalidOperationException("设置页的 Fluent 样式缺失");
            }
            finally { window.Close(); }
        }

        private static int RenderSettings()
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-previews");
            Directory.CreateDirectory(directory);
            try
            {
                foreach (bool dark in new[] { false, true })
                {
                    SettingsWindow window = new SettingsWindow(AppSettings.CreateDefault(), "时间校准状态", dark);
                    try
                    {
                        FrameworkElement content = (FrameworkElement)window.Content;
                        content.Measure(new System.Windows.Size(950, 810));
                        content.Arrange(new System.Windows.Rect(0, 0, 950, 810));
                        content.UpdateLayout();
                        RenderTargetBitmap bitmap = new RenderTargetBitmap(950, 810, 96, 96,
                            System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(content);
                        PngBitmapEncoder encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using (FileStream stream = File.Create(Path.Combine(directory, dark ? "dark.png" : "light.png")))
                            encoder.Save(stream);
                    }
                    finally { window.Close(); }
                }
                File.WriteAllText(Path.Combine(directory, "result.txt"), "OK");
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(directory, "result.txt"), "FAILED\r\n" + ex);
                return 1;
            }
        }

        private static int RenderPreviews()
        {
            string output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "previews");
            try
            {
                int count = PreviewRenderer.RenderAll(output);
                File.WriteAllText(Path.Combine(output, "render-result.txt"), "OK\r\nThemes=" + count);
                return count == ThemeCatalog.All().Count ? 0 : 1;
            }
            catch (Exception ex)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "render-result.txt"), "FAILED\r\n" + ex);
                return 1;
            }
        }

        private static int TestNetworkClock()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "clock-test.txt");
            try
            {
                using (AccurateClock clock = new AccurateClock("time.windows.com"))
                {
                    clock.SyncAsync().Wait();
                    File.WriteAllText(path, "OK\r\nStatus=" + clock.Status +
                        "\r\nOffsetMilliseconds=" + clock.Offset.TotalMilliseconds.ToString("0.0"));
                }
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(path, "FAILED\r\n" + ex);
                return 1;
            }
        }
    }
}
