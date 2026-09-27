using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

[assembly: AssemblyTitle("桌面倒计时")]
[assembly: AssemblyDescription("简洁、准确、可随壁纸自适应的 Windows 桌面倒计时")]
[assembly: AssemblyCompany("walker42144")]
[assembly: AssemblyProduct("Desktop Countdown")]
[assembly: AssemblyVersion("0.1.1.0")]
[assembly: AssemblyFileVersion("0.1.1.0")]
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
            if (args != null && Array.Exists(args, a => string.Equals(a, "--clock-test", StringComparison.OrdinalIgnoreCase)))
                return TestNetworkClock();

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

                AppSettings settings = SettingsStore.Load();
                MainWindow window = new MainWindow(settings);
                application.MainWindow = window;
                window.Show();
                return application.Run();
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
