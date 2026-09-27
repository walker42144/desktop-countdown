using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DesktopCountdown
{
    public sealed class SettingsWindow : Window
    {
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
        private readonly TextBlock previewTitle;
        private readonly TextBlock previewDigits;
        private readonly Border previewBorder;

        public AppSettings Result { get { return working; } }

        public SettingsWindow(AppSettings source, string clockStatus)
        {
            working = source.Clone();
            Title = "桌面倒计时设置";
            Width = 580;
            Height = 760;
            MinWidth = 520;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Background = new SolidColorBrush(Color.FromRgb(246, 247, 249));
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 14;

            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            ScrollViewer scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid form = new Grid { Margin = new Thickness(24, 20, 24, 12) };
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(125) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            titleBox = new TextBox { Text = working.Title };
            targetBox = new TextBox { Text = working.TargetLocal };
            titleFontBox = CreateFontBox(working.TitleFontFamily);
            digitFontBox = CreateFontBox(working.DigitFontFamily);
            titleSizeBox = new TextBox { Text = working.TitleFontSize.ToString("0", CultureInfo.InvariantCulture) };
            digitSizeBox = new TextBox { Text = working.DigitFontSize.ToString("0", CultureInfo.InvariantCulture) };
            themeBox = new ComboBox { MaxDropDownHeight = 220 };
            foreach (ThemeDefinition theme in ThemeCatalog.All()) themeBox.Items.Add(theme);
            foreach (ThemeDefinition theme in themeBox.Items)
                if (theme.Key == working.ThemeName) { themeBox.SelectedItem = theme; break; }
            if (themeBox.SelectedIndex < 0) themeBox.SelectedIndex = 0;
            secondsBox = new CheckBox { Content = "显示秒", IsChecked = working.ShowSeconds };
            autoColorBox = new CheckBox { Content = "根据壁纸自动选择黑色或白色", IsChecked = working.AutoColor };
            manualColorBox = new TextBox { Text = working.ManualForeground, IsEnabled = !working.AutoColor };
            lockedBox = new CheckBox { Content = "锁定后启用鼠标穿透", IsChecked = working.Locked };
            topmostBox = new CheckBox { Content = "始终置顶", IsChecked = working.AlwaysOnTop };
            desktopModeBox = new CheckBox { Content = "挂接到桌面层（实验性）", IsChecked = working.DesktopMode };
            startupBox = new CheckBox { Content = "登录 Windows 后自动启动", IsChecked = working.StartWithWindows };
            ntpBox = new TextBox { Text = working.NtpServer };

            int row = 0;
            AddSection(form, ref row, "倒计时");
            AddLabeledRow(form, ref row, "标题", titleBox);
            AddLabeledRow(form, ref row, "目标时间", targetBox);
            AddHint(form, ref row, "格式：yyyy-MM-dd HH:mm:ss，按当前 Windows 时区解释。可设置过去的时间。");

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

            AddSection(form, ref row, "窗口行为");
            AddWideRow(form, ref row, lockedBox);
            AddWideRow(form, ref row, topmostBox);
            AddWideRow(form, ref row, desktopModeBox);
            AddWideRow(form, ref row, startupBox);

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
            StackPanel previewStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
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
            Typography.SetNumeralAlignment(previewDigits, FontNumeralAlignment.Tabular);
            previewStack.Children.Add(previewTitle);
            previewStack.Children.Add(previewDigits);
            previewBorder.Child = previewStack;
            AddWideRow(form, ref row, previewBorder);

            scroll.Content = form;
            Grid.SetRow(scroll, 0);
            root.Children.Add(scroll);

            Border buttonBar = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 226, 230)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(18, 12, 18, 12)
            };
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Button cancel = new Button { Content = "取消", Width = 92, Height = 34, Margin = new Thickness(0, 0, 10, 0), IsCancel = true };
            Button save = new Button { Content = "保存", Width = 92, Height = 34, IsDefault = true };
            save.Click += SaveClicked;
            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            buttonBar.Child = buttons;
            Grid.SetRow(buttonBar, 1);
            root.Children.Add(buttonBar);
            Content = root;

            titleBox.TextChanged += delegate { UpdatePreview(); };
            titleFontBox.SelectionChanged += delegate { UpdatePreview(); };
            titleFontBox.LostKeyboardFocus += delegate { UpdatePreview(); };
            digitFontBox.SelectionChanged += delegate { UpdatePreview(); };
            digitFontBox.LostKeyboardFocus += delegate { UpdatePreview(); };
            titleSizeBox.TextChanged += delegate { UpdatePreview(); };
            digitSizeBox.TextChanged += delegate { UpdatePreview(); };
            themeBox.SelectionChanged += delegate { UpdatePreview(); };
            autoColorBox.Checked += delegate { manualColorBox.IsEnabled = false; };
            autoColorBox.Unchecked += delegate { manualColorBox.IsEnabled = true; };
            UpdatePreview();
        }

        private static ComboBox CreateFontBox(string selected)
        {
            ComboBox box = new ComboBox { IsEditable = true, MaxDropDownHeight = 300 };
            foreach (string font in Fonts.SystemFontFamilies.Select(f => f.Source).Distinct().OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
                box.Items.Add(font);
            box.Text = selected;
            return box;
        }

        private static void AddSection(Grid grid, ref int row, string text)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock title = new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(37, 42, 49)),
                Margin = new Thickness(0, row == 0 ? 0 : 18, 0, 10)
            };
            Grid.SetRow(title, row);
            Grid.SetColumnSpan(title, 2);
            grid.Children.Add(title);
            row++;
        }

        private static void AddLabeledRow(Grid grid, ref int row, string label, Control editor)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock labelBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(77, 82, 90)),
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

        private static void AddHint(Grid grid, ref int row, string text)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            TextBlock hint = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(112, 117, 126)),
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
            ThemeCatalog.ApplyPreview(previewBorder, previewTitle, previewDigits,
                theme == null ? ThemeCatalog.DefaultTheme : theme.Key);
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
            DialogResult = true;
        }
    }
}
