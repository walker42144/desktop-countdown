using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopCountdown
{
    public sealed partial class SettingsWindow
    {
        private int BuildFormSections(Grid form, string clockStatus)
        {
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
            AddWideRow(form, ref row, startupBox);
            AddLabeledRow(form, ref row, "登录启动方式", startupModeBox);
            AddHint(form, ref row, "登录任务在当前用户桌面中运行，启动前短暂等待系统桌面准备；无需后台常驻服务。");

            AddSection(form, ref row, "时间校准");
            AddLabeledRow(form, ref row, "NTP服务器", ntpBox);
            AddHint(form, ref row, clockStatus);

            AddSection(form, ref row, "实时预览");
            return row;
        }

        private void BuildWindowShell(Grid root, Grid form, bool dark)
        {
            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Background = Brushes.Transparent };
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
            cancel.Click += delegate { Close(); };
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
        }

        private void WireInteractions(Grid root, bool dark)
        {
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
    }
}
