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
using System.Windows.Media.Animation;
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
        private readonly DispatcherTimer motionTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer motionAnimationTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer breathingTimer = new DispatcherTimer(DispatcherPriority.Background);
        private readonly Random motionRandom = new Random();
        private double anchorLeft;
        private double anchorTop;
        private Point motionOffset;
        private Point motionFrom;
        private Point motionTo;
        private DateTime motionStarted;
        private DateTime nextMotionAt;
        private int motionStep;
        private bool internalMotion;
        private bool breathingDimmed;
        private readonly Border surface;
        private readonly TextBlock titleText;
        private readonly TextBlock countdownText;
        private readonly Run dayRun;
        private readonly Run unitRun;
        private readonly Run timeRun;
        private readonly Border accentLine;
        private readonly Grid decorationLayer;
        private TextBlock liveThemeLabel;
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
            Icon = IconFactory.CreateWindowIcon();
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Left = settings.Left;
            Top = settings.Top;
            anchorLeft = settings.Left;
            anchorTop = settings.Top;
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

            Grid cardRoot = new Grid();
            decorationLayer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(decorationLayer, 0);
            StackPanel stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            Panel.SetZIndex(stack, 1);
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
            cardRoot.Children.Add(decorationLayer);
            cardRoot.Children.Add(stack);
            surface.Child = cardRoot;
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
            motionTimer.Tick += delegate { MoveOnce(); };
            motionAnimationTimer.Interval = TimeSpan.FromMilliseconds(33);
            motionAnimationTimer.Tick += delegate { AnimateMotion(); };
            breathingTimer.Interval = TimeSpan.FromSeconds(45);
            breathingTimer.Tick += delegate { ToggleBreathing(); };

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
            LocationChanged += delegate
            {
                if (!internalMotion) { anchorLeft = Left; anchorTop = Top; motionOffset = new Point(); }
                QueueAppearanceUpdate();
            };
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
            ConfigureMotion();

            Dispatcher.BeginInvoke(new Action(delegate
            {
                SnapVisibleSurfaceToNearestEdge();
                settings.Left = Left;
                settings.Top = Top;
                anchorLeft = Left;
                anchorTop = Top;
            }), DispatcherPriority.Loaded);

            if (!settings.HasConfiguredTarget || settings.TargetNeedsRepair)
                Dispatcher.BeginInvoke(new Action(OpenSettings), DispatcherPriority.ApplicationIdle);
        }

        private void ApplySettingsToView()
        {
            titleText.Text = TargetHeading(settings);
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
            ConfigureMotion();
        }

        private static string TargetHeading(AppSettings value)
        {
            if (value.TargetNeedsRepair) return "目标时间无效，请重新设置";
            return value.HasConfiguredTarget ? value.Title : "双击设置目标时间";
        }

        private void UpdateCountdown()
        {
            DateTime target;
            if (settings.HasConfiguredTarget && !settings.TryGetTargetLocal(out target))
                settings.Validate();
            if (!settings.HasConfiguredTarget || settings.TargetNeedsRepair)
            {
                dayRun.Text = "---";
                unitRun.Text = " 天  ";
                timeRun.Text = settings.ShowSeconds ? "--:--:--" : "--:--";
                UpdateLiveThemeLabel();
                ToolTip = BuildTooltip();
                return;
            }

            DateTime now = clock.LocalNow;
            target = settings.GetTargetLocal();
            TimeSpan remaining = target - now;
            bool elapsed = remaining < TimeSpan.Zero;
            if (elapsed) remaining = remaining.Negate();

            CountdownDisplay display = DisplayFormats.RenderCountdown(settings.CountdownFormat,
                remaining, elapsed, settings.ShowSeconds);
            dayRun.Text = display.Days;
            unitRun.Text = display.Unit;
            timeRun.Text = display.Time;
            UpdateLiveThemeLabel();
            ToolTip = BuildTooltip();
        }

        private void ScheduleNextTick()
        {
            DateTime now = clock.LocalNow;
            double milliseconds;
            bool countdownSeconds = DisplayFormats.CountdownNeedsSeconds(settings.CountdownFormat, settings.ShowSeconds);
            bool clockSeconds = string.Equals(settings.ThemeName, "Air", StringComparison.OrdinalIgnoreCase) &&
                DisplayFormats.ClockNeedsSeconds(settings.ClockFormat);
            if (countdownSeconds || clockSeconds)
                milliseconds = 1000 - now.Millisecond + 12;
            else
                milliseconds = (60 - now.Second) * 1000 - now.Millisecond + 12;
            countdownTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(100, milliseconds));
            countdownTimer.Start();
        }

        private string BuildTooltip()
        {
            string motionStatus = !settings.MotionEnabled ? "防烧屏：未启用"
                : nextMotionAt <= DateTime.MinValue ? "防烧屏：已暂停"
                : "防烧屏：下次位移约 " + nextMotionAt.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            return clock.Status + Environment.NewLine + wallpaperStatus + Environment.NewLine + motionStatus;
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
            titleText.Opacity = theme.TitleOpacity;
            countdownText.Foreground = brush;
            countdownText.FontWeight = theme.DigitWeight;
            shadow.Color = shadowColor;
            shadow.Opacity = theme.ShadowOpacity;
            surface.CornerRadius = new CornerRadius(theme.CornerRadius);
            surface.Padding = theme.Padding;
            surface.BorderBrush = theme.Border;
            surface.BorderThickness = new Thickness(theme.BorderThickness);
            accentLine.Background = theme.Accent;
            accentLine.Width = theme.AccentWidth;
            accentLine.Height = theme.AccentHeight;
            accentLine.Visibility = theme.AccentWidth <= 0 ? Visibility.Collapsed : Visibility.Visible;
            liveThemeLabel = ThemeDecorations.Apply(decorationLayer, theme, foreground, FormatCurrentDateTime());
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

        private string FormatCurrentDateTime()
        {
            return clock.LocalNow.ToString(settings.ClockFormat, CultureInfo.InvariantCulture);
        }

        private void UpdateLiveThemeLabel()
        {
            if (liveThemeLabel != null) liveThemeLabel.Text = FormatCurrentDateTime();
        }

        private void ConfigureMotion()
        {
            motionTimer.Stop();
            motionAnimationTimer.Stop();
            breathingTimer.Stop();
            surface.BeginAnimation(OpacityProperty, null);
            surface.Opacity = 1;
            breathingDimmed = false;
            nextMotionAt = DateTime.MinValue;
            if (!IsLoaded || !IsVisible) return;
            if (settings.MotionEnabled)
            {
                motionTimer.Interval = TimeSpan.FromSeconds(settings.MotionIntervalSeconds);
                nextMotionAt = DateTime.Now.Add(motionTimer.Interval);
                motionTimer.Start();
            }
            if (settings.VisualBreathing) breathingTimer.Start();
            ToolTip = BuildTooltip();
        }

        private void MoveOnce()
        {
            MoveOnceCore(settings.MotionMode, settings.MotionAmplitudePixels,
                settings.MotionTransitionMilliseconds, false);
            nextMotionAt = DateTime.Now.Add(motionTimer.Interval);
            ToolTip = BuildTooltip();
        }

        private double MoveOnceCore(string mode, int amplitudePixels, int transitionMilliseconds, bool trial)
        {
            if ((!trial && !settings.MotionEnabled) || !IsVisible || ActualWidth <= 0 || ActualHeight <= 0) return 0;
            bool horizontalEdge = false;
            bool verticalEdge = false;
            try
            {
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point((int)PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2)).X,
                        (int)PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2)).Y));
                Rect work = GetWorkArea(screen);
                horizontalEdge = Math.Abs(anchorLeft + ShadowGutter - work.Left) < 8 ||
                    Math.Abs(anchorLeft + ActualWidth - ShadowGutter - work.Right) < 8;
                verticalEdge = Math.Abs(anchorTop + ShadowGutter - work.Top) < 8 ||
                    Math.Abs(anchorTop + ActualHeight - ShadowGutter - work.Bottom) < 8;
            }
            catch (Exception ex) { DiagnosticLog.Record("MainWindow.MotionEdge", ex); }
            Point previousPixels = motionOffset;
            PresentationSource dpiSource = PresentationSource.FromVisual(this);
            if (dpiSource != null && dpiSource.CompositionTarget != null)
            {
                Vector previous = dpiSource.CompositionTarget.TransformToDevice.Transform(
                    new Vector(motionOffset.X, motionOffset.Y));
                previousPixels = new Point(previous.X, previous.Y);
            }
            Point nextPixels = MotionPlanner.Next(mode, motionStep++, amplitudePixels,
                motionRandom, previousPixels, horizontalEdge, verticalEdge);
            motionTo = nextPixels;
            if (dpiSource != null && dpiSource.CompositionTarget != null)
            {
                Vector next = dpiSource.CompositionTarget.TransformFromDevice.Transform(
                    new Vector(nextPixels.X, nextPixels.Y));
                motionTo = new Point(next.X, next.Y);
            }
            try
            {
                Point center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
                Rect work = GetWorkArea(System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point((int)center.X, (int)center.Y)));
                double amplitudeDip = amplitudePixels;
                if (dpiSource != null && dpiSource.CompositionTarget != null)
                    amplitudeDip = dpiSource.CompositionTarget.TransformFromDevice.Transform(
                        new Vector(amplitudePixels, 0)).Length;
                motionTo = MotionPlanner.EnsureVisibleMove(new Point(anchorLeft, anchorTop), motionTo,
                    motionOffset, new Size(ActualWidth, ActualHeight), work, ShadowGutter, amplitudeDip);
            }
            catch (Exception ex) { DiagnosticLog.Record("MainWindow.MotionBounds", ex); }
            motionFrom = motionOffset;
            motionStarted = DateTime.UtcNow;
            int duration = transitionMilliseconds;
            if (duration == 0 && mode == MotionPlanner.Tide) duration = 1400;
            if (duration == 0 && mode == MotionPlanner.Orbit) duration = 450;
            currentMotionDuration = duration;
            if (duration == 0) ApplyMotionOffset(motionTo);
            else motionAnimationTimer.Start();
            Vector distance = new Vector(motionTo.X - motionFrom.X, motionTo.Y - motionFrom.Y);
            return dpiSource != null && dpiSource.CompositionTarget != null
                ? dpiSource.CompositionTarget.TransformToDevice.Transform(distance).Length
                : distance.Length;
        }

        private int currentMotionDuration;

        private void AnimateMotion()
        {
            double fraction = Math.Min(1, (DateTime.UtcNow - motionStarted).TotalMilliseconds / currentMotionDuration);
            double eased = fraction * fraction * (3 - 2 * fraction);
            ApplyMotionOffset(new Point(motionFrom.X + (motionTo.X - motionFrom.X) * eased,
                motionFrom.Y + (motionTo.Y - motionFrom.Y) * eased));
            if (fraction >= 1) motionAnimationTimer.Stop();
        }

        private void ApplyMotionOffset(Point offset)
        {
            Point desired = new Point(anchorLeft + offset.X, anchorTop + offset.Y);
            try
            {
                Point center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
                Rect work = GetWorkArea(System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point((int)center.X, (int)center.Y)));
                desired = MotionPlanner.Constrain(new Point(anchorLeft, anchorTop), offset,
                    new Size(ActualWidth, ActualHeight), work, ShadowGutter);
            }
            catch (Exception ex) { DiagnosticLog.Record("MainWindow.MotionConstrain", ex); }
            internalMotion = true;
            try { Left = desired.X; Top = desired.Y; }
            finally { internalMotion = false; }
            motionOffset = new Point(Left - anchorLeft, Top - anchorTop);
        }

        private Rect GetWorkArea(System.Windows.Forms.Screen screen)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return new Rect(
                screen.WorkingArea.Left, screen.WorkingArea.Top,
                screen.WorkingArea.Width, screen.WorkingArea.Height);
            Matrix transform = source.CompositionTarget.TransformFromDevice;
            Point start = transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
            Point end = transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
            return new Rect(start, end);
        }

        private void ResetMotionPosition()
        {
            motionAnimationTimer.Stop();
            internalMotion = true;
            try { Left = anchorLeft; Top = anchorTop; }
            finally { internalMotion = false; }
            motionOffset = new Point();
        }

        private void ToggleBreathing()
        {
            if (!IsVisible || !settings.VisualBreathing) return;
            breathingDimmed = !breathingDimmed;
            surface.BeginAnimation(OpacityProperty, new DoubleAnimation(
                breathingDimmed ? 0.88 : 1.0, TimeSpan.FromMilliseconds(1200))
            { FillBehavior = FillBehavior.HoldEnd });
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
                ResetMotionPosition();
                DragMove();
                SnapVisibleSurfaceToNearestEdge();
                anchorLeft = Left;
                anchorTop = Top;
                SaveSettings(false);
                QueueAppearanceUpdate();
            }
            catch { }
        }

        private void OpenSettings()
        {
            if (exiting) return;
            motionTimer.Stop();
            motionAnimationTimer.Stop();
            ResetMotionPosition();
            SettingsWindow dialog = new SettingsWindow(settings, clock.Status);
            dialog.MotionTrialRequested += delegate(object sender, MotionTrialEventArgs trial)
            {
                trial.PlannedDistancePixels = MoveOnceCore(trial.Mode, trial.AmplitudePixels,
                    trial.TransitionMilliseconds, true);
            };
            if (!desktopHost.IsAttached)
            {
                dialog.Owner = this;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                Point center = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
                dialog.OpenOnScreen(System.Windows.Forms.Screen.FromPoint(
                    new System.Drawing.Point((int)center.X, (int)center.Y)));
            }
            bool? accepted = dialog.ShowDialog();
            if (accepted == true)
            {
                AppSettings previousSettings = settings;
                settings = dialog.Result;
                settings.Validate();
                ApplySettingsToView();
                // Retry even when the selection is unchanged: an earlier task creation
                // may have failed after its desired mode was saved to settings.json.
                if (!SaveSettings(true))
                {
                    settings = previousSettings;
                    ApplySettingsToView();
                }
                clock.SyncAsync();
            }
            ResetMotionPosition();
            ConfigureMotion();
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
                motionTimer.Stop();
                motionAnimationTimer.Stop();
                breathingTimer.Stop();
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
            countdownTimer.Stop();
            ScheduleNextTick();
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

        private bool SaveSettings(bool applyStartup)
        {
            try
            {
                settings.Left = anchorLeft;
                settings.Top = anchorTop;
                if (applyStartup)
                    StartupService.ApplyAndSave(settings.StartWithWindows, settings.StartupMode,
                        Process.GetCurrentProcess().MainModule.FileName, () => SettingsStore.Save(settings));
                else
                    SettingsStore.Save(settings);
                return true;
            }
            catch (Exception ex)
            {
                if (applyStartup)
                    MessageBox.Show("设置未能保存。" + ex.Message,
                        "桌面倒计时", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
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
                ResetMotionPosition();
                EnsureVisiblePosition();
                anchorLeft = Left;
                anchorTop = Top;
                ConfigureMotion();
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
                motionTimer.Stop();
                motionAnimationTimer.Stop();
                breathingTimer.Stop();
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
            motionTimer.Stop();
            motionAnimationTimer.Stop();
            breathingTimer.Stop();
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
