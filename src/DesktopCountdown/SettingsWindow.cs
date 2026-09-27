using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DesktopCountdown
{
    public sealed class MotionTrialEventArgs : EventArgs
    {
        public string Mode { get; private set; }
        public int AmplitudePixels { get; private set; }
        public int TransitionMilliseconds { get; private set; }
        public double PlannedDistancePixels { get; set; }

        public MotionTrialEventArgs(string mode, int amplitudePixels, int transitionMilliseconds)
        {
            Mode = mode;
            AmplitudePixels = amplitudePixels;
            TransitionMilliseconds = transitionMilliseconds;
        }
    }

    public sealed class SettingsWindow : Window
    {
        public event EventHandler<MotionTrialEventArgs> MotionTrialRequested;
        private readonly AppSettings working;
        private readonly TextBox titleBox;
        private readonly TextBox targetBox;
        private readonly ComboBox titleFontBox;
        private readonly ComboBox digitFontBox;
        private readonly TextBox titleSizeBox;
        private readonly TextBox digitSizeBox;
        private readonly ComboBox themeBox;
        private readonly CheckBox secondsBox;
        private readonly CheckBox autoColorBox;
        private readonly TextBox manualColorBox;
        private readonly CheckBox lockedBox;
        private readonly CheckBox topmostBox;
        private readonly CheckBox desktopModeBox;
        private readonly CheckBox startupBox;
        private readonly TextBox ntpBox;
        private readonly ComboBox countdownPresetBox;
        private readonly TextBox countdownFormatBox;
        private readonly ComboBox clockPresetBox;
        private readonly TextBox clockFormatBox;
        private readonly CheckBox motionBox;
        private readonly ComboBox motionModeBox;
        private readonly TextBox motionIntervalBox;
        private readonly TextBox motionAmplitudeBox;
        private readonly ComboBox motionTransitionBox;
        private readonly CheckBox breathingBox;
        private readonly ComboBox startupModeBox;
        private readonly TextBlock formatPreview;
        private readonly TextBlock clockPreview;
        private readonly TextBlock motionDescription;
        private readonly TextBlock motionStatus;
        private readonly Canvas motionCanvas;
        private readonly Ellipse motionDot;
        private readonly DispatcherTimer motionPreviewTimer = new DispatcherTimer();
        private readonly Random previewRandom = new Random(3);
        private Point previewPrevious;
        private int previewStep;
        private System.Windows.Forms.Screen requestedScreen;
        private readonly Dictionary<string, FrameworkElement> sectionAnchors = new Dictionary<string, FrameworkElement>();
        private readonly TextBlock previewTitle;
        private readonly TextBlock previewDigits;
        private readonly Border previewBorder;
        private readonly Grid previewDecoration;
        private readonly Border previewAccent;
        private readonly FluentTheme theme;
        private string popupMaterialStatus = "未打开弹层";
        private int sectionStartRow = -1;

        public AppSettings Result { get { return working; } }

        public SettingsWindow(AppSettings source, string clockStatus, bool? darkOverride = null)
        {
            working = source.Clone();
            Title = "桌面倒计时设置";
            Icon = IconFactory.CreateWindowIcon();
            Width = 950;
            Height = 810;
            MinWidth = 800;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            bool dark = darkOverride ?? IsSystemDark();
            theme = new FluentTheme(dark);
            theme.Install(Resources);
            Background = theme.Background;
            Foreground = theme.Text;
            FontFamily = new FontFamily("Segoe UI Variable, Microsoft YaHei UI, system-ui, sans-serif");
            FontSize = 14;
            InstallModernStyles(dark);

            Grid root = new Grid { Background = theme.Background };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(188) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Brushes.Transparent };
            Grid form = new Grid { Margin = new Thickness(25, 12, 25, 20) };
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(142) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            titleBox = new TextBox { Text = working.Title };
            targetBox = new TextBox { Text = working.TargetLocal };
            titleFontBox = CreateFontBox(working.TitleFontFamily);
            digitFontBox = CreateFontBox(working.DigitFontFamily);
            titleSizeBox = new TextBox { Text = working.TitleFontSize.ToString("0", CultureInfo.InvariantCulture) };
            digitSizeBox = new TextBox { Text = working.DigitFontSize.ToString("0", CultureInfo.InvariantCulture) };
            themeBox = new ComboBox { MaxDropDownHeight = 220 };
            foreach (ThemeDefinition definition in ThemeCatalog.All()) themeBox.Items.Add(definition);
            foreach (ThemeDefinition definition in themeBox.Items)
                if (definition.Key == working.ThemeName) { themeBox.SelectedItem = definition; break; }
            if (themeBox.SelectedIndex < 0) themeBox.SelectedIndex = 0;
            secondsBox = new CheckBox { Content = "显示秒", IsChecked = working.ShowSeconds };
            autoColorBox = new CheckBox { Content = "根据壁纸自动选择黑色或白色", IsChecked = working.AutoColor };
            manualColorBox = new TextBox { Text = working.ManualForeground, IsEnabled = !working.AutoColor };
            lockedBox = new CheckBox { Content = "锁定后启用鼠标穿透", IsChecked = working.Locked };
            topmostBox = new CheckBox { Content = "始终置顶", IsChecked = working.AlwaysOnTop };
            desktopModeBox = new CheckBox { Content = "挂接到桌面层（实验性）", IsChecked = working.DesktopMode };
            startupBox = new CheckBox { Content = "登录 Windows 后自动启动", IsChecked = working.StartWithWindows };
            ntpBox = new TextBox { Text = working.NtpServer };
            countdownPresetBox = new ComboBox();
            string[] countdownPresets = { "当前样式", "简洁数字", "紧凑单位", "总小时数", "自定义" };
            foreach (string item in countdownPresets) countdownPresetBox.Items.Add(item);
            countdownFormatBox = new TextBox { Text = working.CountdownFormat };
            countdownPresetBox.SelectedIndex = CountdownPresetIndex(working.CountdownFormat);
            clockPresetBox = new ComboBox();
            string[] clockPresets = { "完整日期时间", "仅时间", "简短日期", "日期与分钟", "自定义" };
            foreach (string item in clockPresets) clockPresetBox.Items.Add(item);
            clockFormatBox = new TextBox { Text = working.ClockFormat };
            clockPresetBox.SelectedIndex = ClockPresetIndex(working.ClockFormat);
            motionBox = new CheckBox { Content = "启用防烧屏微位移", IsChecked = working.MotionEnabled };
            motionModeBox = new ComboBox();
            for (int i = 0; i < MotionPlanner.Modes.Length; i++) motionModeBox.Items.Add(MotionPlanner.Names[i]);
            motionModeBox.SelectedIndex = Math.Max(0, Array.IndexOf(MotionPlanner.Modes, working.MotionMode));
            motionIntervalBox = new TextBox { Text = working.MotionIntervalSeconds.ToString(CultureInfo.InvariantCulture) };
            motionAmplitudeBox = new TextBox { Text = working.MotionAmplitudePixels.ToString(CultureInfo.InvariantCulture) };
            motionTransitionBox = new ComboBox();
            motionTransitionBox.Items.Add("依方式推荐");
            motionTransitionBox.Items.Add("轻柔 0.4 秒");
            motionTransitionBox.Items.Add("舒缓 1 秒");
            motionTransitionBox.Items.Add("缓慢 2 秒");
            int[] durations = { 0, 400, 1000, 2000 };
            motionTransitionBox.SelectedIndex = Math.Max(0, Array.IndexOf(durations, working.MotionTransitionMilliseconds));
            breathingBox = new CheckBox { Content = "视觉呼吸：偶尔轻微降低卡片亮度", IsChecked = working.VisualBreathing };
            startupModeBox = new ComboBox();
            startupModeBox.Items.Add("普通启动（当前用户启动项）");
            startupModeBox.Items.Add("登录任务（任务计划程序）");
            startupModeBox.SelectedIndex = working.StartupMode == "Task" ? 1 : 0;
            formatPreview = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            clockPreview = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            motionDescription = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
            motionStatus = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap,
                Foreground = theme.SecondaryText,
                Text = working.MotionEnabled
                    ? "设置页打开期间自动位移暂停；保存并关闭后约 " +
                        working.MotionIntervalSeconds.ToString(CultureInfo.InvariantCulture) + " 秒首次移动。"
                    : "自动位移当前未启用；仍可试运行一次查看桌面卡片的效果。" };
            motionCanvas = new Canvas { Width = 320, Height = 78, HorizontalAlignment = HorizontalAlignment.Left,
                Background = theme.Panel };
            motionDot = new Ellipse { Width = 12, Height = 12, Fill = theme.Accent };
            motionCanvas.Children.Add(motionDot);
            Canvas.SetLeft(motionDot, 154);
            Canvas.SetTop(motionDot, 33);

            int row = 0;
            AddHeader(form, ref row, "桌面倒计时", "外观、时间与桌面行为都在这里调整");
            AddSection(form, ref row, "倒计时");
            AddLabeledRow(form, ref row, "标题", titleBox);
            AddLabeledRow(form, ref row, "目标时间", targetBox);
            AddHint(form, ref row, "格式：yyyy-MM-dd HH:mm:ss，按当前 Windows 时区解释。可设置过去的时间。");
            AddLabeledRow(form, ref row, "显示样式", countdownPresetBox);
            AddLabeledRow(form, ref row, "倒计时格式", countdownFormatBox);
            AddHint(form, ref row, "占位符：{days:3}、{hours:2}、{minutes:2}、{seconds:2}、{totalHours}、{sign}。关闭“显示秒”仅影响当前样式。");
            AddWideRow(form, ref row, formatPreview);

            AddSection(form, ref row, "字体与显示");
            AddLabeledRow(form, ref row, "标题字体", titleFontBox);
            AddLabeledRow(form, ref row, "数字字体", digitFontBox);
            AddLabeledRow(form, ref row, "标题字号", titleSizeBox);
            AddLabeledRow(form, ref row, "数字字号", digitSizeBox);
            AddLabeledRow(form, ref row, "艺术风格", themeBox);
            AddWideRow(form, ref row, secondsBox);
            AddWideRow(form, ref row, autoColorBox);
            AddLabeledRow(form, ref row, "手动颜色", manualColorBox);
            AddHint(form, ref row, "颜色使用 #AARRGGBB，例如 #FFF5F7FA。复杂壁纸会自动增加轻微底板。");

            AddSection(form, ref row, "右上角日期时间 · 现代留白");
            AddLabeledRow(form, ref row, "日期时间样式", clockPresetBox);
            AddLabeledRow(form, ref row, "日期时间格式", clockFormatBox);
            AddHint(form, ref row, "使用 yyyy、MM、dd、ddd、HH、mm、ss 等 .NET 日期格式；可选择数字预设或自行填写。");
            AddWideRow(form, ref row, clockPreview);

            AddSection(form, ref row, "防烧屏微位移");
            AddWideRow(form, ref row, motionBox);
            AddLabeledRow(form, ref row, "移动方式", motionModeBox);
            AddLabeledRow(form, ref row, "间隔（秒）", motionIntervalBox);
            AddLabeledRow(form, ref row, "幅度（像素）", motionAmplitudeBox);
            AddLabeledRow(form, ref row, "移动过渡", motionTransitionBox);
            AddWideRow(form, ref row, breathingBox);
            AddWideRow(form, ref row, motionDescription);
            AddWideRow(form, ref row, motionCanvas);
            Button tryMotion = new Button { Content = "让桌面卡片试运行一次", Width = 190,
                HorizontalAlignment = HorizontalAlignment.Left };
            tryMotion.Click += delegate { TryMotion(); };
            AddWideRow(form, ref row, tryMotion);
            AddWideRow(form, ref row, motionStatus);
            AddHint(form, ref row, "实际位置会始终限制在当前屏幕工作区内。此功能可降低固定图形长期停留的风险，不能保证完全避免烧屏。");

            AddSection(form, ref row, "窗口行为");
            AddWideRow(form, ref row, lockedBox);
            AddWideRow(form, ref row, topmostBox);
            AddWideRow(form, ref row, desktopModeBox);
            AddWideRow(form, ref row, startupBox);
            AddLabeledRow(form, ref row, "登录启动方式", startupModeBox);
            AddHint(form, ref row, "登录任务在当前用户桌面中运行，启动前短暂等待系统桌面准备；无需后台常驻服务。");

            AddSection(form, ref row, "时间校准");
            AddLabeledRow(form, ref row, "NTP服务器", ntpBox);
            AddHint(form, ref row, clockStatus);

            AddSection(form, ref row, "实时预览");
            previewBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(39, 43, 49)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 4, 0, 10)
            };
            Grid previewRoot = new Grid();
            previewDecoration = new Grid { IsHitTestVisible = false };
            Panel.SetZIndex(previewDecoration, 0);
            StackPanel previewStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            Panel.SetZIndex(previewStack, 1);
            previewTitle = new TextBlock
            {
                Text = working.Title,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            previewDigits = new TextBlock
            {
                Text = "083 天  12:36:20",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            previewAccent = new Border
            {
                Width = 42,
                Height = 2,
                CornerRadius = new CornerRadius(1),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };
            Typography.SetNumeralAlignment(previewDigits, FontNumeralAlignment.Tabular);
            previewStack.Children.Add(previewTitle);
            previewStack.Children.Add(previewAccent);
            previewStack.Children.Add(previewDigits);
            previewRoot.Children.Add(previewDecoration);
            previewRoot.Children.Add(previewStack);
            previewBorder.Child = previewRoot;
            AddWideRow(form, ref row, previewBorder);
            CloseSection(form, row);

            Border card = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(22, 16, 22, 24),
                Margin = new Thickness(24, 22, 24, 18),
                Child = form
            };
            form.Margin = new Thickness();
            scroll.Content = card;
            Grid.SetRow(scroll, 0);
            Grid.SetColumn(scroll, 1);
            root.Children.Add(scroll);
            Border sidebar = BuildSidebar(scroll, dark);
            Grid.SetRow(sidebar, 0);
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);

            Border buttonBar = new Border
            {
                Background = theme.Panel,
                BorderBrush = theme.Border,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 12, 18, 12)
            };
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "取消", Width = 92, Height = 34, Margin = new Thickness(0, 0, 10, 0), IsCancel = true };
            Button save = new Button { Content = "保存", Width = 92, Height = 34, IsDefault = true };
            save.Background = theme.Accent;
            save.Foreground = theme.AccentText;
            save.Click += SaveClicked;
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            buttonBar.Child = buttons;
            Grid.SetRow(buttonBar, 1);
            Grid.SetColumnSpan(buttonBar, 2);
            root.Children.Add(buttonBar);
            Content = root;
            SourceInitialized += delegate { ApplyWindowBackdrop(dark); };
            Loaded += delegate { AttachAcrylicToCombos(root); };

            titleBox.TextChanged += delegate { UpdatePreview(); };
            titleFontBox.SelectionChanged += delegate { UpdatePreview(); };
            titleFontBox.LostKeyboardFocus += delegate { UpdatePreview(); };
            digitFontBox.SelectionChanged += delegate { UpdatePreview(); };
            digitFontBox.LostKeyboardFocus += delegate { UpdatePreview(); };
            titleSizeBox.TextChanged += delegate { UpdatePreview(); };
            digitSizeBox.TextChanged += delegate { UpdatePreview(); };
            themeBox.SelectionChanged += delegate { UpdatePreview(); };
            secondsBox.Checked += delegate { UpdatePreview(); };
            secondsBox.Unchecked += delegate { UpdatePreview(); };
            countdownPresetBox.SelectionChanged += delegate { ApplyCountdownPreset(); };
            clockPresetBox.SelectionChanged += delegate { ApplyClockPreset(); };
            countdownFormatBox.TextChanged += delegate
            {
                countdownPresetBox.SelectedIndex = CountdownPresetIndex(countdownFormatBox.Text);
                UpdatePreview();
            };
            clockFormatBox.TextChanged += delegate
            {
                clockPresetBox.SelectedIndex = ClockPresetIndex(clockFormatBox.Text);
                UpdatePreview();
            };
            motionModeBox.SelectionChanged += delegate { UpdateMotionPreview(); };
            motionAmplitudeBox.TextChanged += delegate { UpdateMotionPreview(); };
            motionIntervalBox.TextChanged += delegate { UpdateMotionPreview(); };
            motionBox.Checked += delegate { UpdateMotionPreview(); };
            motionBox.Unchecked += delegate { UpdateMotionPreview(); };
            autoColorBox.Checked += delegate { manualColorBox.IsEnabled = false; };
            autoColorBox.Unchecked += delegate { manualColorBox.IsEnabled = true; };
            startupBox.Checked += delegate { startupModeBox.IsEnabled = true; };
            startupBox.Unchecked += delegate { startupModeBox.IsEnabled = false; };
            startupModeBox.IsEnabled = startupBox.IsChecked == true;
            UpdatePreview();
            UpdateMotionPreview();
            motionPreviewTimer.Interval = TimeSpan.FromMilliseconds(900);
            motionPreviewTimer.Tick += delegate { if (motionBox.IsChecked == true) StepMotionPreview(); };
            Loaded += delegate { motionPreviewTimer.Start(); };
            Loaded += delegate { if (requestedScreen != null) MoveToRequestedScreen(); };
            Closed += delegate { motionPreviewTimer.Stop(); };
        }

        public void OpenOnScreen(System.Windows.Forms.Screen screen)
        {
            requestedScreen = screen;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        private void MoveToRequestedScreen()
        {
            IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeRect rect;
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out rect)) return;
            System.Drawing.Rectangle work = requestedScreen.WorkingArea;
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            int x = work.Left + Math.Max(0, (work.Width - width) / 2);
            int y = work.Top + Math.Max(0, (work.Height - height) / 2);
            SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
        }

        private static ComboBox CreateFontBox(string selected)
        {
            ComboBox box = new ComboBox { IsEditable = true, MaxDropDownHeight = 300 };
            foreach (string font in Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
                box.Items.Add(font);
            box.Text = selected;
            return box;
        }

        private static SolidColorBrush Brush(string hex)
        {
            SolidColorBrush brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static bool IsSystemDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    return key != null && Convert.ToInt32(key.GetValue("AppsUseLightTheme", 1)) == 0;
            }
            catch { return false; }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct Margins { public int Left, Right, Top, Bottom; }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

        private void ApplyWindowBackdrop(bool dark)
        {
            try
            {
                IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int corner = 2;
                int darkCaption = dark ? 1 : 0;
                DwmSetWindowAttribute(handle, 33, ref corner, sizeof(int));
                DwmSetWindowAttribute(handle, 20, ref darkCaption, sizeof(int));
                if (Environment.OSVersion.Version.Build >= 22621)
                {
                    int mica = 2;
                    DwmSetWindowAttribute(handle, 38, ref mica, sizeof(int));
                }
            }
            catch (Exception ex) { DiagnosticLog.Record("SettingsWindow.Backdrop", ex); }
        }

        private void InstallModernStyles(bool dark)
        {
            Brush border = theme.Border;
            Brush field = theme.Field;
            Brush text = theme.Text;
            Brush accent = theme.Accent;

            Style input = new Style(typeof(TextBox));
            input.Setters.Add(new Setter(Control.BackgroundProperty, field));
            input.Setters.Add(new Setter(Control.ForegroundProperty, text));
            input.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            input.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            input.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
            ControlTemplate textTemplate = new ControlTemplate(typeof(TextBox));
            FrameworkElementFactory textBorder = new FrameworkElementFactory(typeof(Border));
            textBorder.Name = "InputBorder";
            textBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            textBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            textBorder.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            textBorder.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            textBorder.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory host = new FrameworkElementFactory(typeof(ScrollViewer));
            host.Name = "PART_ContentHost";
            textBorder.AppendChild(host);
            textTemplate.VisualTree = textBorder;
            Trigger focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
            focus.Setters.Add(new Setter(Border.BorderBrushProperty, accent, "InputBorder"));
            focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1, 1, 1, 2), "InputBorder"));
            textTemplate.Triggers.Add(focus);
            input.Setters.Add(new Setter(Control.TemplateProperty, textTemplate));
            Resources.Add(typeof(TextBox), input);

            Style button = new Style(typeof(Button));
            button.Setters.Add(new Setter(Control.BackgroundProperty, theme.Card));
            button.Setters.Add(new Setter(Control.ForegroundProperty, text));
            button.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            button.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 7, 14, 7)));
            button.Setters.Add(new Setter(Control.MinHeightProperty, 34.0));
            ControlTemplate buttonTemplate = new ControlTemplate(typeof(Button));
            FrameworkElementFactory buttonBorder = new FrameworkElementFactory(typeof(Border));
            buttonBorder.Name = "ButtonBorder";
            buttonBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            buttonBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            buttonBorder.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            buttonBorder.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,
                new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.VerticalAlignmentProperty,
                new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.MarginProperty,
                new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            presenter.SetBinding(TextElement.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            buttonBorder.AppendChild(presenter);
            buttonTemplate.VisualTree = buttonBorder;
            Trigger hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BorderBrushProperty, accent, "ButtonBorder"));
            buttonTemplate.Triggers.Add(hover);
            button.Setters.Add(new Setter(Control.TemplateProperty, buttonTemplate));
            Resources.Add(typeof(Button), button);

            Style combo = new Style(typeof(ComboBox));
            combo.Setters.Add(new Setter(Control.BackgroundProperty, field));
            combo.Setters.Add(new Setter(Control.ForegroundProperty, text));
            combo.Setters.Add(new Setter(Control.BorderBrushProperty, border));
            combo.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            combo.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            using (Stream templateStream = typeof(SettingsWindow).Assembly.GetManifestResourceStream("DesktopCountdown.ComboBoxTemplate.xaml"))
            {
                if (templateStream != null)
                    combo.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Load(templateStream)));
            }
            Resources.Add(typeof(ComboBox), combo);

            Style itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, text));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, field));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 5, 8, 5)));
            Resources.Add(typeof(ComboBoxItem), itemStyle);

            InstallStyle("DesktopCountdown.CheckBoxStyle.xaml", typeof(CheckBox));
            InstallStyle("DesktopCountdown.ScrollBarStyle.xaml", typeof(System.Windows.Controls.Primitives.ScrollBar));
        }

        private void InstallStyle(string resourceName, Type controlType)
        {
            using (Stream stream = typeof(SettingsWindow).Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) throw new InvalidOperationException("缺少界面样式资源：" + resourceName);
                Resources.Add(controlType, (Style)XamlReader.Load(stream));
            }
        }

        private void AttachAcrylicToCombos(DependencyObject parent)
        {
            ComboBox combo = parent as ComboBox;
            if (combo != null)
            {
                combo.DropDownOpened -= ComboDropDownOpened;
                combo.DropDownOpened += ComboDropDownOpened;
            }
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                AttachAcrylicToCombos(VisualTreeHelper.GetChild(parent, i));
        }

        private void ComboDropDownOpened(object sender, EventArgs e)
        {
            ComboBox box = sender as ComboBox;
            if (box == null) return;
            popupMaterialStatus = "已收到展开事件";
            Dispatcher.BeginInvoke(new Action(delegate
            {
                Popup popup = box.Template.FindName("PART_Popup", box) as Popup;
                Border panel = popup == null ? null : popup.Child as Border;
                if (panel == null || !popup.IsOpen) { popupMaterialStatus = "未取得弹层"; return; }
                panel.Background = theme.Card;
                if (SystemParameters.HighContrast)
                { popupMaterialStatus = "系统高对比度模式已禁用 Acrylic"; return; }
                try
                {
                    System.Windows.Interop.HwndSource source =
                        System.Windows.PresentationSource.FromVisual(panel) as System.Windows.Interop.HwndSource;
                    if (source == null) { popupMaterialStatus = "未取得独立窗口句柄"; return; }
                    int acrylic = 3;
                    int setResult = DwmSetWindowAttribute(source.Handle, 38, ref acrylic, sizeof(int));
                    if (setResult != 0) { popupMaterialStatus = "设置 Acrylic 失败：" + setResult; return; }
                    Margins margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                    int extendResult = DwmExtendFrameIntoClientArea(source.Handle, ref margins);
                    if (extendResult != 0) { popupMaterialStatus = "扩展背景失败：" + extendResult; return; }
                    int rounded = 2;
                    DwmSetWindowAttribute(source.Handle, 33, ref rounded, sizeof(int));
                    source.CompositionTarget.BackgroundColor = Colors.Transparent;
                    panel.Background = theme.Flyout;
                    popupMaterialStatus = "已请求系统 Acrylic";
                }
                catch (Exception ex) { panel.Background = theme.Card; popupMaterialStatus = ex.GetType().Name; }
            }), DispatcherPriority.Loaded);
        }

        private static void AddHeader(Grid grid, ref int row, string title, string subtitle)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel heading = new StackPanel { Margin = new Thickness(0, 1, 0, 10) };
            heading.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Opacity = 0.65, Margin = new Thickness(0, 3, 0, 0) });
            Grid.SetRow(heading, row);
            Grid.SetColumnSpan(heading, 2);
            grid.Children.Add(heading);
            row++;
        }

        private void AddSection(Grid grid, ref int row, string text)
        {
            CloseSection(grid, row);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock title = new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = Foreground,
                Margin = new Thickness(0, row == 0 ? 0 : 22, 0, 10)
            };
            Grid.SetRow(title, row);
            Grid.SetColumnSpan(title, 2);
            grid.Children.Add(title);
            sectionAnchors[text] = title;
            row++;
            sectionStartRow = row;
        }

        private void CloseSection(Grid grid, int row)
        {
            if (sectionStartRow < 0 || row <= sectionStartRow) return;
            Border sectionCard = new Border
            {
                Background = theme.Card,
                BorderBrush = theme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(-14, -3, -14, 0),
                IsHitTestVisible = false
            };
            Grid.SetRow(sectionCard, sectionStartRow);
            Grid.SetRowSpan(sectionCard, row - sectionStartRow);
            Grid.SetColumnSpan(sectionCard, 2);
            Panel.SetZIndex(sectionCard, -1);
            grid.Children.Add(sectionCard);
        }

        private Border BuildSidebar(ScrollViewer scroll, bool dark)
        {
            Border rail = new Border
            {
                Background = theme.Panel,
                BorderBrush = theme.Border,
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(14, 24, 12, 16)
            };
            StackPanel stack = new StackPanel();
            StackPanel brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(7, 0, 0, 33) };
            Border logoTile = new Border
            {
                Width = 44, Height = 44,
                Background = theme.Selected,
                CornerRadius = new CornerRadius(12),
                Child = new Image { Source = IconFactory.CreateWindowIcon(), Width = 34, Height = 34, Stretch = Stretch.Uniform }
            };
            StackPanel brandText = new StackPanel { Margin = new Thickness(9, 2, 0, 0) };
            brandText.Children.Add(new TextBlock { Text = "倒计时", FontSize = 15, FontWeight = FontWeights.SemiBold });
            brandText.Children.Add(new TextBlock { Text = "版本 0.3", FontSize = 10, Foreground = theme.SecondaryText, Margin = new Thickness(0, 3, 0, 0) });
            brand.Children.Add(logoTile);
            brand.Children.Add(brandText);
            stack.Children.Add(brand);

            string[] destinations = { "倒计时", "字体与显示", "右上角日期时间 · 现代留白", "防烧屏微位移", "窗口行为", "时间校准", "实时预览" };
            string[] labels = { "倒计时", "外观与字体", "日期与时间", "防烧屏", "窗口与启动", "时间校准", "实时预览" };
            string[] glyphs = { "\uE823", "\uE771", "\uE787", "\uE7F4", "\uE713", "\uE916", "\uE890" };
            List<Button> navigation = new List<Button>();
            for (int i = 0; i < destinations.Length; i++)
            {
                string destination = destinations[i];
                Button item = new Button
                {
                    Content = NavigationContent(glyphs[i], labels[i]),
                    Height = 40,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 2, 0, 2),
                    BorderThickness = new Thickness(0),
                    Background = i == 0 ? theme.Selected : Brushes.Transparent,
                    Foreground = Foreground
                };
                item.Click += delegate
                {
                    foreach (Button other in navigation) other.Background = Brushes.Transparent;
                    item.Background = theme.Selected;
                    FrameworkElement anchor;
                    if (sectionAnchors.TryGetValue(destination, out anchor))
                    {
                        Point position = anchor.TransformToAncestor(scroll).Transform(new Point(0, 0));
                        scroll.ScrollToVerticalOffset(scroll.VerticalOffset + position.Y - 26);
                    }
                };
                navigation.Add(item);
                stack.Children.Add(item);
            }
            stack.Children.Add(new TextBlock
            {
                Text = "设置保存在本机",
                FontSize = 11,
                Opacity = 0.52,
                Margin = new Thickness(8, 36, 0, 0)
            });
            rail.Child = stack;
            return rail;
        }

        private static StackPanel NavigationContent(string glyph, string label)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 16, Width = 28, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            return row;
        }

        private void AddLabeledRow(Grid grid, ref int row, string label, Control editor)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Foreground,
                Opacity = 0.78,
                Margin = new Thickness(0, 0, 12, 10)
            };
            editor.Margin = new Thickness(0, 0, 0, 10);
            editor.MinHeight = 30;
            editor.VerticalContentAlignment = VerticalAlignment.Center;
            Grid.SetRow(labelBlock, row);
            Grid.SetColumn(labelBlock, 0);
            Grid.SetRow(editor, row);
            Grid.SetColumn(editor, 1);
            grid.Children.Add(labelBlock);
            grid.Children.Add(editor);
            row++;
        }

        private static void AddWideRow(Grid grid, ref int row, UIElement element)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            FrameworkElement frameworkElement = element as FrameworkElement;
            if (frameworkElement != null && frameworkElement.Margin == new Thickness())
                frameworkElement.Margin = new Thickness(0, 3, 0, 8);
            Grid.SetRow(element, row);
            Grid.SetColumn(element, 1);
            grid.Children.Add(element);
            row++;
        }

        private void AddHint(Grid grid, ref int row, string text)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock hint = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Foreground,
                Opacity = 0.62,
                FontSize = 12,
                Margin = new Thickness(0, -3, 0, 8)
            };
            Grid.SetRow(hint, row);
            Grid.SetColumn(hint, 1);
            grid.Children.Add(hint);
            row++;
        }

        private void UpdatePreview()
        {
            if (previewTitle == null || previewDigits == null) return;
            previewTitle.Text = titleBox.Text;
            previewTitle.FontFamily = SafeFont(titleFontBox.Text, "MiSans");
            previewDigits.FontFamily = SafeFont(digitFontBox.Text, "Bahnschrift");

            double size;
            if (double.TryParse(titleSizeBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out size) && size >= 12 && size <= 72)
                previewTitle.FontSize = size;
            if (double.TryParse(digitSizeBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out size) && size >= 32 && size <= 240)
                previewDigits.FontSize = Math.Min(size, 62);
            ThemeDefinition theme = themeBox.SelectedItem as ThemeDefinition;
            string error;
            if (DisplayFormats.TryValidateCountdown(countdownFormatBox.Text, out error))
            {
                previewDigits.Text = DisplayFormats.Countdown(countdownFormatBox.Text,
                    new TimeSpan(83, 12, 36, 20), false, secondsBox.IsChecked == true);
                formatPreview.Text = "预览  " + previewDigits.Text;
            }
            else formatPreview.Text = error;
            if (DisplayFormats.TryValidateClock(clockFormatBox.Text, out error))
                clockPreview.Text = "预览  " + DateTime.Now.ToString(clockFormatBox.Text, CultureInfo.InvariantCulture);
            else clockPreview.Text = error;
            ThemeCatalog.ApplyPreview(previewBorder, previewDecoration, previewAccent, previewTitle, previewDigits,
                theme == null ? ThemeCatalog.DefaultTheme : theme.Key, clockFormatBox.Text);
        }

        private static string CountdownPreset(int index)
        {
            switch (index)
            {
                case 0: return DisplayFormats.DefaultCountdown;
                case 1: return "{sign}{days:3} · {hours:2}:{minutes:2}:{seconds:2}";
                case 2: return "{sign}{days}天 {hours:2}时 {minutes:2}分 {seconds:2}秒";
                case 3: return "{sign}{totalHours}小时 {minutes:2}分";
                default: return null;
            }
        }

        private static int CountdownPresetIndex(string format)
        {
            for (int i = 0; i < 4; i++) if (format == CountdownPreset(i)) return i;
            return 4;
        }

        private static string ClockPreset(int index)
        {
            switch (index)
            {
                case 0: return DisplayFormats.DefaultClock;
                case 1: return "HH:mm:ss";
                case 2: return "MM/dd  HH:mm";
                case 3: return "yyyy-MM-dd  HH:mm";
                default: return null;
            }
        }

        private static int ClockPresetIndex(string format)
        {
            for (int i = 0; i < 4; i++) if (format == ClockPreset(i)) return i;
            return 4;
        }

        private void ApplyCountdownPreset()
        {
            if (countdownPresetBox == null || countdownFormatBox == null) return;
            string format = CountdownPreset(countdownPresetBox.SelectedIndex);
            if (format != null) countdownFormatBox.Text = format;
            UpdatePreview();
        }

        private void ApplyClockPreset()
        {
            if (clockPresetBox == null || clockFormatBox == null) return;
            string format = ClockPreset(clockPresetBox.SelectedIndex);
            if (format != null) clockFormatBox.Text = format;
            UpdatePreview();
        }

        private void UpdateMotionPreview()
        {
            if (motionModeBox == null || motionDescription == null) return;
            string[] descriptions = {
                "在九个位置间轮换，安静而均匀。",
                "沿微型椭圆轨道前进，适合星轨主题。",
                "每次寻找不同落点，避免反复回到同两点。",
                "以缓慢过渡漂移到下一处位置。",
                "靠近边缘时顺着边缘移动。",
                "在较远的几个位置之间低频切换。"
            };
            int index = Math.Max(0, motionModeBox.SelectedIndex);
            motionDescription.Text = descriptions[Math.Min(index, descriptions.Length - 1)];
            motionDescription.Opacity = motionBox.IsChecked == true ? 0.78 : 0.48;
            int interval;
            bool validInterval = int.TryParse(motionIntervalBox.Text, out interval) && interval >= 30 && interval <= 1800;
            motionStatus.Text = motionBox.IsChecked == true
                ? validInterval
                    ? "设置页打开期间自动位移暂停；保存并关闭后约 " +
                        interval.ToString(CultureInfo.InvariantCulture) + " 秒首次移动。"
                    : "请将间隔设为 30～1800 秒。"
                : "自动位移当前未启用；仍可试运行一次查看桌面卡片的效果。";
        }

        private void StepMotionPreview()
        {
            if (motionCanvas == null || motionDot == null || motionModeBox == null) return;
            int amplitude;
            if (!int.TryParse(motionAmplitudeBox.Text, out amplitude)) amplitude = 6;
            amplitude = Math.Max(1, Math.Min(20, amplitude));
            string mode = MotionPlanner.Modes[Math.Max(0, motionModeBox.SelectedIndex)];
            Point next = MotionPlanner.Next(mode, previewStep++, amplitude, previewRandom,
                previewPrevious, mode == MotionPlanner.EdgeWalk, false);
            previewPrevious = next;
            double scale = mode == MotionPlanner.FarNear ? 1.2 : 3.5;
            Canvas.SetLeft(motionDot, 154 + next.X * scale);
            Canvas.SetTop(motionDot, 33 + next.Y * Math.Min(scale, 2));
        }

        private void TryMotion()
        {
            int amplitude;
            if (!int.TryParse(motionAmplitudeBox.Text, out amplitude) || amplitude < 1 || amplitude > 20)
            {
                motionStatus.Text = "请先将位移幅度设为 1～20 像素。";
                return;
            }
            StepMotionPreview();
            EventHandler<MotionTrialEventArgs> handler = MotionTrialRequested;
            if (handler == null)
            {
                motionStatus.Text = "当前是独立设置预览，未连接桌面卡片。";
                return;
            }
            int[] transitionValues = { 0, 400, 1000, 2000 };
            int index = Math.Max(0, motionTransitionBox.SelectedIndex);
            MotionTrialEventArgs args = new MotionTrialEventArgs(
                MotionPlanner.Modes[Math.Max(0, motionModeBox.SelectedIndex)], amplitude,
                transitionValues[index]);
            handler(this, args);
            motionStatus.Text = args.PlannedDistancePixels >= 0.75
                ? "桌面卡片已试运行，预计移动 " +
                    args.PlannedDistancePixels.ToString("0.#", CultureInfo.CurrentCulture) + " 像素；关闭设置页后恢复原位。"
                : "当前位置没有足够空间完成位移，请稍微移开屏幕边缘后重试。";
        }

        private static FontFamily SafeFont(string value, string fallback)
        {
            try { return new FontFamily(string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()); }
            catch { return new FontFamily(fallback); }
        }

        private void SaveClicked(object sender, RoutedEventArgs e)
        {
            DateTime target;
            if (!DateTime.TryParseExact(targetBox.Text.Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out target))
            {
                MessageBox.Show(this, "目标时间格式应为 yyyy-MM-dd HH:mm:ss。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
                targetBox.Focus();
                return;
            }

            double titleSize;
            double digitSize;
            if (!double.TryParse(titleSizeBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out titleSize) || titleSize < 12 || titleSize > 72)
            {
                MessageBox.Show(this, "标题字号应在 12～72 之间。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!double.TryParse(digitSizeBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out digitSize) || digitSize < 32 || digitSize > 240)
            {
                MessageBox.Show(this, "数字字号应在 32～240 之间。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (autoColorBox.IsChecked != true)
            {
                try { ColorConverter.ConvertFromString(manualColorBox.Text.Trim()); }
                catch
                {
                    MessageBox.Show(this, "手动颜色格式无效，请使用 #AARRGGBB。", "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            string formatError;
            if (!DisplayFormats.TryValidateCountdown(countdownFormatBox.Text, out formatError))
            {
                MessageBox.Show(this, formatError, "倒计时格式无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                countdownFormatBox.Focus();
                return;
            }
            if (!DisplayFormats.TryValidateClock(clockFormatBox.Text, out formatError))
            {
                MessageBox.Show(this, formatError, "日期时间格式无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                clockFormatBox.Focus();
                return;
            }
            int interval;
            int amplitudePixels;
            if (!int.TryParse(motionIntervalBox.Text, out interval) || interval < 30 || interval > 1800 ||
                !int.TryParse(motionAmplitudeBox.Text, out amplitudePixels) || amplitudePixels < 1 || amplitudePixels > 20)
            {
                MessageBox.Show(this, "位移间隔应为 30～1800 秒，幅度应为 1～20 像素。", "位移设置无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            working.HasConfiguredTarget = true;
            working.Title = string.IsNullOrWhiteSpace(titleBox.Text) ? "距离目标时刻还有" : titleBox.Text.Trim();
            working.TargetLocal = target.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            working.TitleFontFamily = string.IsNullOrWhiteSpace(titleFontBox.Text) ? "MiSans" : titleFontBox.Text.Trim();
            working.DigitFontFamily = string.IsNullOrWhiteSpace(digitFontBox.Text) ? "Bahnschrift" : digitFontBox.Text.Trim();
            working.TitleFontSize = titleSize;
            working.DigitFontSize = digitSize;
            ThemeDefinition selectedTheme = themeBox.SelectedItem as ThemeDefinition;
            working.ThemeName = selectedTheme == null ? ThemeCatalog.DefaultTheme : selectedTheme.Key;
            working.ShowSeconds = secondsBox.IsChecked == true;
            working.AutoColor = autoColorBox.IsChecked == true;
            working.ManualForeground = manualColorBox.Text.Trim();
            working.Locked = lockedBox.IsChecked == true;
            working.AlwaysOnTop = topmostBox.IsChecked == true;
            working.DesktopMode = desktopModeBox.IsChecked == true;
            working.StartWithWindows = startupBox.IsChecked == true;
            working.NtpServer = string.IsNullOrWhiteSpace(ntpBox.Text) ? "time.windows.com" : ntpBox.Text.Trim();
            working.CountdownFormat = countdownFormatBox.Text;
            working.ClockFormat = clockFormatBox.Text;
            working.MotionEnabled = motionBox.IsChecked == true;
            working.MotionMode = MotionPlanner.Modes[Math.Max(0, motionModeBox.SelectedIndex)];
            working.MotionIntervalSeconds = interval;
            working.MotionAmplitudePixels = amplitudePixels;
            int[] transitionValues = { 0, 400, 1000, 2000 };
            working.MotionTransitionMilliseconds = transitionValues[Math.Max(0, motionTransitionBox.SelectedIndex)];
            working.VisualBreathing = breathingBox.IsChecked == true;
            working.StartupMode = startupModeBox.SelectedIndex == 1 ? "Task" : "Registry";
            DialogResult = true;
        }
    }
}
