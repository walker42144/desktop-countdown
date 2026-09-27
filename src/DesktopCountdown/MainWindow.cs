using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;

namespace DesktopCountdown
{
    public sealed class MainWindow : Window
    {
        private const double ShadowGutter = 18;
        private const double EdgeSnapDistance = 32;
        private AppSettings settings;
        private readonly AccurateClock clock;
        private readonly DesktopHostService desktopHost = new DesktopHostService();
        private readonly DispatcherTimer countdownTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer appearanceTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly Border surface;
        private readonly TextBlock titleText;
        private readonly TextBlock countdownText;
        private readonly Run dayRun;
        private readonly Run unitRun;
        private readonly Run timeRun;
        private readonly Border accentLine;
        private readonly DropShadowEffect shadow;
        private readonly System.Windows.Forms.NotifyIcon trayIcon;
        private readonly System.Windows.Forms.ToolStripMenuItem lockTrayItem;
        private readonly System.Windows.Forms.ToolStripMenuItem desktopTrayItem;
        private readonly System.Windows.Forms.ToolStripMenuItem topmostTrayItem;
        private readonly System.Windows.Forms.ToolStripMenuItem visibilityTrayItem;
        private readonly IList<System.Windows.Forms.ToolStripMenuItem> themeTrayItems = new List<System.Windows.Forms.ToolStripMenuItem>();
        private bool exiting;
        private bool sourceReady;
        private string wallpaperStatus = "尚未分析壁纸";

        public MainWindow(AppSettings initialSettings)
        {
            settings = initialSettings;
            clock = new AccurateClock(settings.NtpServer);

            Title = "桌面倒计时";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Left = settings.Left;
            Top = settings.Top;
            MinWidth = 280;

            shadow = new DropShadowEffect
            {
                BlurRadius = 10,
                ShadowDepth = 1,
                Opacity = 0.62,
                Color = Colors.Black
            };

            surface = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(24, 16, 24, 18),
                Effect = shadow
            };

            StackPanel stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            titleText = new TextBlock
            {
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            countdownText = new TextBlock
            {
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.NoWrap
            };
            Typography.SetNumeralAlignment(countdownText, FontNumeralAlignment.Tabular);
            Typography.SetNumeralStyle(countdownText, FontNumeralStyle.Lining);

            dayRun = new Run("---");
            unitRun = new Run(" 天  ");
            timeRun = new Run("--:--:--");
            countdownText.Inlines.Add(dayRun);
            countdownText.Inlines.Add(unitRun);
            countdownText.Inlines.Add(timeRun);
            accentLine = new Border
            {
                Height = 2,
                Width = 42,
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            stack.Children.Add(titleText);
            stack.Children.Add(accentLine);
            stack.Children.Add(countdownText);
            surface.Child = stack;
            Grid shell = new Grid { Margin = new Thickness(ShadowGutter) };
            shell.Children.Add(surface);
            Content = shell;

            countdownTimer.Tick += delegate
            {
                countdownTimer.Stop();
                UpdateCountdown();
                ScheduleNextTick();
            };
            appearanceTimer.Interval = TimeSpan.FromMilliseconds(350);
            appearanceTimer.Tick += delegate
            {
                appearanceTimer.Stop();
                UpdateAppearance();
            };

            trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = IconFactory.CreateClockIcon(),
                Text = "桌面倒计时",
                Visible = true
            };
            System.Windows.Forms.ContextMenuStrip trayMenu = new System.Windows.Forms.ContextMenuStrip();
            trayMenu.Items.Add("设置…", null, delegate { RunOnUi(OpenSettings); });
            lockTrayItem = new System.Windows.Forms.ToolStripMenuItem("锁定并穿透");
            lockTrayItem.Click += delegate { RunOnUi(ToggleLock); };
            trayMenu.Items.Add(lockTrayItem);
            desktopTrayItem = new System.Windows.Forms.ToolStripMenuItem("桌面层模式");
            desktopTrayItem.Click += delegate { RunOnUi(ToggleDesktopMode); };
            trayMenu.Items.Add(desktopTrayItem);
            topmostTrayItem = new System.Windows.Forms.ToolStripMenuItem("始终置顶");
            topmostTrayItem.Click += delegate { RunOnUi(ToggleTopmost); };
            trayMenu.Items.Add(topmostTrayItem);
            System.Windows.Forms.ToolStripMenuItem themeTrayMenu = new System.Windows.Forms.ToolStripMenuItem("艺术风格");
            foreach (ThemeDefinition theme in ThemeCatalog.All())
            {
                string themeKey = theme.Key;
                System.Windows.Forms.ToolStripMenuItem item = new System.Windows.Forms.ToolStripMenuItem(theme.DisplayName);
                item.Click += delegate { RunOnUi(delegate { SwitchTheme(themeKey); }); };
                themeTrayItems.Add(item);
                themeTrayMenu.DropDownItems.Add(item);
            }
            trayMenu.Items.Add(themeTrayMenu);
            visibilityTrayItem = new System.Windows.Forms.ToolStripMenuItem("隐藏");
            visibilityTrayItem.Click += delegate { RunOnUi(ToggleVisibility); };
            trayMenu.Items.Add(visibilityTrayItem);
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add("立即校准时间", null, delegate { clock.SyncAsync(); });
            trayMenu.Items.Add("退出", null, delegate { RunOnUi(ExitApplication); });
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.DoubleClick += delegate { RunOnUi(OpenSettings); };

            ContextMenu windowMenu = new ContextMenu();
            MenuItem settingsItem = new MenuItem { Header = "设置…" };
            settingsItem.Click += delegate { OpenSettings(); };
            MenuItem lockItem = new MenuItem { Header = "锁定并穿透" };
            lockItem.Click += delegate { ToggleLock(); };
            MenuItem syncItem = new MenuItem { Header = "立即校准时间" };
            syncItem.Click += delegate { clock.SyncAsync(); };
            MenuItem exitItem = new MenuItem { Header = "退出" };
            exitItem.Click += delegate { ExitApplication(); };
            windowMenu.Items.Add(settingsItem);
            windowMenu.Items.Add(lockItem);
            MenuItem themeWindowMenu = new MenuItem { Header = "艺术风格" };
            foreach (ThemeDefinition theme in ThemeCatalog.All())
            {
                string themeKey = theme.Key;
                MenuItem themeItem = new MenuItem { Header = theme.DisplayName };
                themeItem.Click += delegate { SwitchTheme(themeKey); };
                themeWindowMenu.Items.Add(themeItem);
            }
            windowMenu.Items.Add(themeWindowMenu);
            windowMenu.Items.Add(syncItem);
            windowMenu.Items.Add(new Separator());
            windowMenu.Items.Add(exitItem);
            ContextMenu = windowMenu;

            Loaded += WindowLoaded;
            SourceInitialized += delegate { sourceReady = true; };
            LocationChanged += delegate { QueueAppearanceUpdate(); };
            SizeChanged += delegate { QueueAppearanceUpdate(); };
            MouseLeftButtonDown += WindowMouseLeftButtonDown;
            MouseDoubleClick += delegate { if (!settings.Locked) OpenSettings(); };
            Closing += WindowClosing;

            clock.StatusChanged += delegate
            {
                RunOnUi(delegate { ToolTip = BuildTooltip(); });
            };
            SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
            SystemEvents.DisplaySettingsChanged += SystemDisplaySettingsChanged;
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            EnsureVisiblePosition();
            ApplySettingsToView();
            UpdateCountdown();
            ScheduleNextTick();
            QueueAppearanceUpdate();
            clock.Start();
            UpdateTrayChecks();

            Dispatcher.BeginInvoke(new Action(delegate
            {
                SnapVisibleSurfaceToNearestEdge();
                settings.Left = Left;
                settings.Top = Top;
            }), DispatcherPriority.Loaded);

            if (!settings.HasConfiguredTarget)
                Dispatcher.BeginInvoke(new Action(OpenSettings), DispatcherPriority.ApplicationIdle);
        }

        private void ApplySettingsToView()
        {
            titleText.Text = settings.HasConfiguredTarget ? settings.Title : "双击设置目标时间";
            titleText.FontFamily = SafeFont(settings.TitleFontFamily, "MiSans");
            titleText.FontSize = settings.TitleFontSize;
            titleText.FontWeight = FontWeights.Normal;
            countdownText.FontFamily = SafeFont(settings.DigitFontFamily, "Bahnschrift");
            countdownText.FontSize = settings.DigitFontSize;
            countdownText.FontWeight = FontWeights.SemiBold;
            unitRun.FontFamily = SafeFont(settings.TitleFontFamily, "MiSans");
            unitRun.FontSize = settings.DigitFontSize * 0.48;
            Topmost = settings.AlwaysOnTop && !settings.DesktopMode;
            clock.ChangeServer(settings.NtpServer);

            if (sourceReady)
            {
                bool attached = desktopHost.ApplyDesktopMode(this, settings.DesktopMode);
                if (settings.DesktopMode && !attached)
                {
                    settings.DesktopMode = false;
                    Topmost = settings.AlwaysOnTop;
                }
                desktopHost.ApplyClickThrough(this, settings.Locked);
            }

            UpdateCountdown();
            QueueAppearanceUpdate();
            UpdateTrayChecks();
        }

        private void UpdateCountdown()
        {
            if (!settings.HasConfiguredTarget)
            {
                dayRun.Text = "---";
                unitRun.Text = " 天  ";
                timeRun.Text = settings.ShowSeconds ? "--:--:--" : "--:--";
                ToolTip = BuildTooltip();
                return;
            }

            DateTime now = clock.LocalNow;
            DateTime target = settings.GetTargetLocal();
            TimeSpan remaining = target - now;
            bool elapsed = remaining < TimeSpan.Zero;
            if (elapsed) remaining = remaining.Negate();

            long totalDays = (long)Math.Floor(remaining.TotalDays);
            string daysText = totalDays < 1000
                ? totalDays.ToString("000", CultureInfo.InvariantCulture)
                : totalDays.ToString(CultureInfo.InvariantCulture);
            dayRun.Text = elapsed ? "+" + daysText : daysText;
            unitRun.Text = " 天  ";
            timeRun.Text = settings.ShowSeconds
                ? string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", remaining.Hours, remaining.Minutes, remaining.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", remaining.Hours, remaining.Minutes);
            ToolTip = BuildTooltip();
        }

        private void ScheduleNextTick()
        {
            DateTime now = clock.LocalNow;
            double milliseconds;
            if (settings.ShowSeconds)
                milliseconds = 1000 - now.Millisecond + 12;
            else
                milliseconds = (60 - now.Second) * 1000 - now.Millisecond + 12;
            countdownTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, milliseconds));
            countdownTimer.Start();
        }

        private string BuildTooltip()
        {
            return clock.Status + Environment.NewLine + wallpaperStatus;
        }

        private void QueueAppearanceUpdate()
        {
            if (!IsLoaded) return;
            appearanceTimer.Stop();
            appearanceTimer.Start();
        }

        private void UpdateAppearance()
        {
            ThemeDefinition theme = ThemeCatalog.Get(settings.ThemeName);
            System.Windows.Media.Color foreground;
            System.Windows.Media.Color shadowColor;
            bool useBackdrop = false;

            if (settings.AutoColor && theme.AdaptiveToWallpaper)
            {
                DrawingRectangle bounds = GetPhysicalBounds();
                WallpaperAppearance appearance = WallpaperColorService.Analyze(bounds);
                foreground = appearance.Foreground;
                shadowColor = appearance.Shadow;
                useBackdrop = appearance.UseBackdrop;
                wallpaperStatus = appearance.Description;
            }
            else
            {
                if (settings.AutoColor)
                    foreground = theme.Foreground;
                else
                {
                    try { foreground = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(settings.ManualForeground); }
                    catch { foreground = theme.Foreground; }
                }
                double luminance = (0.2126 * foreground.R + 0.7152 * foreground.G + 0.0722 * foreground.B) / 255.0;
                shadowColor = settings.AutoColor ? theme.Shadow : (luminance > 0.55 ? Colors.Black : Colors.White);
                wallpaperStatus = settings.AutoColor ? "艺术主题使用内置高对比配色" : "正在使用手动文字颜色";
            }

            SolidColorBrush brush = new SolidColorBrush(foreground);
            brush.Freeze();
            titleText.Foreground = brush;
            countdownText.Foreground = brush;
            shadow.Color = shadowColor;
            shadow.Opacity = theme.ShadowOpacity;
            surface.CornerRadius = new CornerRadius(theme.CornerRadius);
            surface.Padding = theme.Padding;
            surface.BorderBrush = theme.Border;
            surface.BorderThickness = theme.Border == Brushes.Transparent ? new Thickness(0) : new Thickness(1);
            accentLine.Background = theme.Accent;
            accentLine.Visibility = theme.Accent == Brushes.Transparent ? Visibility.Collapsed : Visibility.Visible;
            if (!theme.AdaptiveToWallpaper)
                surface.Background = theme.Background;
            else if (theme.TransparentWhenCalm && !useBackdrop)
                surface.Background = Brushes.Transparent;
            else if (useBackdrop)
                surface.Background = new SolidColorBrush(foreground.R > 128
                    ? System.Windows.Media.Color.FromArgb(50, 0, 0, 0)
                    : System.Windows.Media.Color.FromArgb(44, 255, 255, 255));
            else
                surface.Background = theme.Background;
            ToolTip = BuildTooltip();
        }

        private DrawingRectangle GetPhysicalBounds()
        {
            try
            {
                System.Windows.Point topLeft = PointToScreen(new System.Windows.Point(0, 0));
                System.Windows.Point bottomRight = PointToScreen(new System.Windows.Point(Math.Max(1, ActualWidth), Math.Max(1, ActualHeight)));
                return DrawingRectangle.FromLTRB((int)topLeft.X, (int)topLeft.Y, (int)bottomRight.X, (int)bottomRight.Y);
            }
            catch
            {
                return new DrawingRectangle((int)Left, (int)Top, Math.Max(1, (int)ActualWidth), Math.Max(1, (int)ActualHeight));
            }
        }

        private void WindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (settings.Locked || e.ChangedButton != MouseButton.Left) return;
            try
            {
                DragMove();
                SnapVisibleSurfaceToNearestEdge();
                settings.Left = Left;
                settings.Top = Top;
                SaveSettings(false);
                QueueAppearanceUpdate();
            }
            catch { }
        }

        private void OpenSettings()
        {
            if (exiting) return;
            SettingsWindow dialog = new SettingsWindow(settings, clock.Status);
            if (!desktopHost.IsAttached) dialog.Owner = this;
            bool? accepted = dialog.ShowDialog();
            if (accepted == true)
            {
                settings = dialog.Result;
                ApplySettingsToView();
                SaveSettings(true);
                clock.SyncAsync();
            }
        }

        private void ToggleLock()
        {
            settings.Locked = !settings.Locked;
            desktopHost.ApplyClickThrough(this, settings.Locked);
            UpdateTrayChecks();
            SaveSettings(false);
        }

        private void ToggleDesktopMode()
        {
            settings.DesktopMode = !settings.DesktopMode;
            ApplySettingsToView();
            SaveSettings(false);
        }

        private void ToggleTopmost()
        {
            settings.AlwaysOnTop = !settings.AlwaysOnTop;
            ApplySettingsToView();
            SaveSettings(false);
        }

        private void ToggleVisibility()
        {
            if (IsVisible)
            {
                Hide();
                visibilityTrayItem.Text = "显示";
            }
            else
            {
                Show();
                visibilityTrayItem.Text = "隐藏";
                ApplySettingsToView();
            }
        }

        private void SwitchTheme(string themeKey)
        {
            settings.ThemeName = themeKey;
            UpdateAppearance();
            UpdateTrayChecks();
            SaveSettings(false);
        }

        private void UpdateTrayChecks()
        {
            lockTrayItem.Checked = settings.Locked;
            desktopTrayItem.Checked = settings.DesktopMode;
            topmostTrayItem.Checked = settings.AlwaysOnTop;
            visibilityTrayItem.Text = IsVisible ? "隐藏" : "显示";
            IList<ThemeDefinition> themes = ThemeCatalog.All();
            for (int i = 0; i < themeTrayItems.Count && i < themes.Count; i++)
                themeTrayItems[i].Checked = string.Equals(settings.ThemeName, themes[i].Key, StringComparison.OrdinalIgnoreCase);
        }

        private void SaveSettings(bool applyStartup)
        {
            try
            {
                settings.Left = Left;
                settings.Top = Top;
                SettingsStore.Save(settings);
                if (applyStartup)
                    SettingsStore.ApplyStartupSetting(settings.StartWithWindows, Process.GetCurrentProcess().MainModule.FileName);
            }
            catch (Exception ex)
            {
                if (applyStartup)
                    MessageBox.Show("设置未能完整保存：" + ex.Message, "桌面倒计时", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void EnsureVisiblePosition()
        {
            double virtualLeft = SystemParameters.VirtualScreenLeft;
            double virtualTop = SystemParameters.VirtualScreenTop;
            double virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
            double virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
            if (Left < virtualLeft - 100 || Top < virtualTop - 100 || Left > virtualRight - 50 || Top > virtualBottom - 50)
            {
                Left = virtualLeft + Math.Max(40, (SystemParameters.VirtualScreenWidth - 500) / 2);
                Top = virtualTop + 80;
            }
        }

        private void SnapVisibleSurfaceToNearestEdge()
        {
            if (!IsLoaded || desktopHost.IsAttached || ActualWidth <= ShadowGutter * 2 || ActualHeight <= ShadowGutter * 2)
                return;

            try
            {
                System.Windows.Point physicalCenter = PointToScreen(new System.Windows.Point(ActualWidth / 2, ActualHeight / 2));
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point((int)physicalCenter.X, (int)physicalCenter.Y));
                PresentationSource source = PresentationSource.FromVisual(this);
                if (source == null || source.CompositionTarget == null) return;

                Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
                System.Windows.Point workTopLeft = fromDevice.Transform(
                    new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
                System.Windows.Point workBottomRight = fromDevice.Transform(
                    new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));

                Left = EdgeSnapCalculator.SnapAxis(Left, ActualWidth,
                    workTopLeft.X, workBottomRight.X, ShadowGutter, EdgeSnapDistance);
                Top = EdgeSnapCalculator.SnapAxis(Top, ActualHeight,
                    workTopLeft.Y, workBottomRight.Y, ShadowGutter, EdgeSnapDistance);
            }
            catch
            {
                // DPI or monitor topology can change while dragging; keep the user's position if snapping cannot be resolved.
            }
        }

        private static System.Windows.Media.FontFamily SafeFont(string name, string fallback)
        {
            try { return new System.Windows.Media.FontFamily(string.IsNullOrWhiteSpace(name) ? fallback : name); }
            catch { return new System.Windows.Media.FontFamily(fallback); }
        }

        private void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            RunOnUi(QueueAppearanceUpdate);
        }

        private void SystemDisplaySettingsChanged(object sender, EventArgs e)
        {
            RunOnUi(delegate
            {
                EnsureVisiblePosition();
                QueueAppearanceUpdate();
            });
        }

        private void RunOnUi(Action action)
        {
            if (Dispatcher.CheckAccess()) action();
            else Dispatcher.BeginInvoke(action);
        }

        private void WindowClosing(object sender, CancelEventArgs e)
        {
            if (!exiting)
            {
                e.Cancel = true;
                Hide();
                visibilityTrayItem.Text = "显示";
            }
        }

        private void ExitApplication()
        {
            if (exiting) return;
            exiting = true;
            SaveSettings(false);
            countdownTimer.Stop();
            appearanceTimer.Stop();
            clock.Dispose();
            trayIcon.Visible = false;
            if (trayIcon.Icon != null) trayIcon.Icon.Dispose();
            trayIcon.Dispose();
            SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= SystemDisplaySettingsChanged;
            if (desktopHost.IsAttached) desktopHost.ApplyDesktopMode(this, false);
            Close();
            Application.Current.Shutdown();
        }
    }
}
