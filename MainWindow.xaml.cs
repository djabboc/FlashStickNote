using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using FlashStickNote.Models;
using FlashStickNote.Services;
using FlashStickNote.ViewModels;

namespace FlashStickNote;

public partial class MainWindow : Window
{
    private readonly Dictionary<int, HotkeyManager> _hotkeys = new();
    private readonly List<string> _failedHotkeys = new();
    private NoteStorage? _storage;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _isExiting;

    public MainWindow()
    {
        ApplyThemeResources();
        InitializeComponent();

        var conf = ConfigService.LoadConfig();
        _appConfig = conf;
        Logger.Init(ConfigService.BaseDir);
        Logger.Log($"程序启动，exe 目录: {ConfigService.BaseDir}");
        Logger.Log($"conf.json: notesDir={conf.NotesDir}, notesFormat={conf.NotesFormat}, fontFamily={conf.FontFamily}");

        try
        {
            _storage = new NoteStorage(ConfigService.BaseDir, conf.NotesDir, conf.NotesFormat);
            Logger.Log($"笔记目录: {_storage.Dir}");
        }
        catch (Exception ex)
        {
            Logger.Log($"笔记目录创建失败: {ex}");
            System.Windows.MessageBox.Show(
                $"笔记目录创建失败：{ex.Message}\n将改用默认目录（程序目录下的 notes 文件夹）。\n配置文件位置：{ConfigService.BaseDir}conf.json",
                "闪念笔记",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _storage = new NoteStorage(ConfigService.BaseDir, "notes", conf.NotesFormat);
            Logger.Log($"回退到默认目录: {_storage.Dir}");
        }

        DataContext = new MainViewModel(_storage);
        ApplyConfig(conf);
        ApplyWindowShortcut();
        InitLineNumberHooks();
        InitFontZoom();
        InitTrayIcon();
    }

    private System.Windows.Controls.ScrollViewer? _contentScrollViewer;
    private Controls.LineNumberRenderer? _lineNumbers;
    private bool _lineNumbersDeferred;
    private AppConfig _appConfig = new();
    private System.Windows.Input.ModifierKeys _zoomModifier;
    private bool _zoomEnabled;
    private readonly System.Windows.Threading.DispatcherTimer _zoomSaveTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(600),
    };

    private void ApplyThemeResources()
    {
        var conf = ConfigService.LoadConfig();
        var theme = ThemeService.Load(conf.Theme);

        SetResource("Theme.WindowBackground", theme.WindowBackground);
        SetResource("Theme.ListBackground", theme.ListBackground);
        SetResource("Theme.ListHeaderBackground", theme.ListHeaderBackground);
        SetResource("Theme.ListHeaderForeground", theme.ListHeaderForeground);
        SetResource("Theme.ListForeground", theme.ListForeground);
        SetResource("Theme.ListSecondaryForeground", theme.ListSecondaryForeground);
        SetResource("Theme.ListTertiaryForeground", theme.ListTertiaryForeground);
        SetResource("Theme.ListSelectedBackground", theme.ListSelectedBackground);
        SetResource("Theme.ListSelectedForeground", theme.ListSelectedForeground);
        SetResource("Theme.EditorBackground", theme.EditorBackground);
        SetResource("Theme.EditorForeground", theme.EditorForeground);
        SetResource("Theme.LineNumberBackground", theme.LineNumberBackground);
        SetResource("Theme.LineNumberForeground", theme.LineNumberForeground);
        SetResource("Theme.BorderColor", theme.BorderColor);
        SetResource("Theme.SplitterColor", theme.SplitterColor);
        Logger.Log($"已应用主题: {theme.Name}");
    }

    private System.Windows.Media.Brush? ParseBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            var brush = new System.Windows.Media.BrushConverter().ConvertFromString(hex)
                as System.Windows.Media.Brush;
            brush?.Freeze();
            return brush;
        }
        catch
        {
            return null;
        }
    }

    private void SetResource(string key, string hex)
    {
        var brush = ParseBrush(hex);
        if (brush != null)
        {
            Resources[key] = brush;
        }
    }

    private void ApplyConfig(AppConfig conf)
    {
        var baseFamily = conf.FontFamily;
        var titleFamily = string.IsNullOrWhiteSpace(conf.TitleFontFamily) ? baseFamily : conf.TitleFontFamily;
        var contentFamily = string.IsNullOrWhiteSpace(conf.ContentFontFamily) ? baseFamily : conf.ContentFontFamily;
        var listTitleFamily = string.IsNullOrWhiteSpace(conf.ListTitleFontFamily) ? baseFamily : conf.ListTitleFontFamily;
        var listPreviewFamily = string.IsNullOrWhiteSpace(conf.ListPreviewFontFamily) ? baseFamily : conf.ListPreviewFontFamily;

        try
        {
            TitleBox.FontFamily = new System.Windows.Media.FontFamily(titleFamily);
            ContentBox.FontFamily = new System.Windows.Media.FontFamily(contentFamily);
            NoteList.FontFamily = new System.Windows.Media.FontFamily(baseFamily);
            Resources["Theme.ListTitleFontFamily"] = new System.Windows.Media.FontFamily(listTitleFamily);
            Resources["Theme.ListPreviewFontFamily"] = new System.Windows.Media.FontFamily(listPreviewFamily);
        }
        catch
        {
        }

        TitleBox.FontSize = conf.TitleFontSize;
        ContentBox.FontSize = conf.FontSize;
        NoteList.FontSize = conf.NoteListFontSize;
        Resources["Theme.ListTitleFontSize"] = conf.ListTitleFontSize;
        Resources["Theme.ListPreviewFontSize"] = conf.ListPreviewFontSize;

        if (ParseBrush(conf.TitleColor) is { } titleBrush)
        {
            TitleBox.Foreground = titleBrush;
        }

        if (ParseBrush(conf.ContentColor) is { } contentBrush)
        {
            ContentBox.Foreground = contentBrush;
            ContentBox.CaretBrush = contentBrush;
        }

        ContentBox.TextWrapping = conf.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        ContentBox.HorizontalScrollBarVisibility = conf.WordWrap
            ? System.Windows.Controls.ScrollBarVisibility.Disabled
            : System.Windows.Controls.ScrollBarVisibility.Auto;
        LineNumberPanel.Visibility = conf.ShowLineNumbers ? Visibility.Visible : Visibility.Collapsed;
        ScheduleLineNumbers();
    }

    private void InitFontZoom()
    {
        _zoomSaveTimer.Tick += (_, _) =>
        {
            _zoomSaveTimer.Stop();
            ConfigService.SaveConfig(_appConfig);
            Logger.Log($"已保存字体大小: fontSize={_appConfig.FontSize}, titleFontSize={_appConfig.TitleFontSize}");
        };

        var combo = ConfigService.LoadShortcut().FontZoom;
        try
        {
            var parts = combo.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var mods = System.Windows.Input.ModifierKeys.None;
            var hasWheel = false;
            foreach (var part in parts)
            {
                if (part.Equals("Wheel", StringComparison.OrdinalIgnoreCase))
                {
                    hasWheel = true;
                    continue;
                }

                switch (part.ToLowerInvariant())
                {
                    case "ctrl":
                        mods |= System.Windows.Input.ModifierKeys.Control;
                        break;
                    case "alt":
                        mods |= System.Windows.Input.ModifierKeys.Alt;
                        break;
                    case "shift":
                        mods |= System.Windows.Input.ModifierKeys.Shift;
                        break;
                    default:
                        return;
                }
            }

            if (!hasWheel || parts.Length < 2)
            {
                Logger.Log($"字体缩放快捷键配置无效（需包含修饰键+Wheel）: {combo}");
                return;
            }

            _zoomModifier = mods;
            _zoomEnabled = true;
            ContentBox.PreviewMouseWheel += OnEditorMouseWheel;
            TitleBox.PreviewMouseWheel += OnEditorMouseWheel;
            Logger.Log($"字体缩放快捷键已启用: {combo}");
        }
        catch (Exception ex)
        {
            Logger.Log($"字体缩放快捷键解析失败: {combo} ({ex.Message})");
        }
    }

    private void OnEditorMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (!_zoomEnabled || System.Windows.Input.Keyboard.Modifiers != _zoomModifier)
        {
            return;
        }

        var step = e.Delta > 0 ? 1.0 : -1.0;
        var newSize = Math.Clamp(_appConfig.FontSize + step, 8.0, 72.0);
        var newTitleSize = Math.Clamp(_appConfig.TitleFontSize + step, 8.0, 72.0);
        if (Math.Abs(newSize - _appConfig.FontSize) < 0.01 &&
            Math.Abs(newTitleSize - _appConfig.TitleFontSize) < 0.01)
        {
            e.Handled = true;
            return;
        }

        _appConfig.FontSize = newSize;
        _appConfig.TitleFontSize = newTitleSize;
        ContentBox.FontSize = newSize;
        TitleBox.FontSize = newTitleSize;
        ScheduleLineNumbers();

        _zoomSaveTimer.Stop();
        _zoomSaveTimer.Start();
        e.Handled = true;
    }

    private void ApplyWindowShortcut()
    {
        var combo = ConfigService.LoadShortcut().HideWindow;
        try
        {
            var converter = new System.Windows.Input.KeyGestureConverter();
            if (converter.ConvertFromString(combo) is System.Windows.Input.KeyGesture gesture)
            {
                InputBindings.Add(new System.Windows.Input.KeyBinding(new RelayCommand(HideToTray), gesture));
                Logger.Log($"窗口内快捷键已注册: 隐藏到托盘={combo}");
            }
            else
            {
                Logger.Log($"隐藏窗口快捷键解析失败: {combo}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"隐藏窗口快捷键解析失败: {combo} ({ex.Message})");
        }
    }

    private void HideToTray()
    {
        if (IsVisible)
        {
            Hide();
        }
    }

    private void NoteList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Delete)
        {
            return;
        }

        DeleteSelectedWithConfirm();
        e.Handled = true;
    }

    private void NoteList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && NoteList.SelectedItem is Note note)
        {
            vm.SelectedNote = note;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e) => DeleteSelectedWithConfirm();

    private void DeleteSelectedWithConfirm()
    {
        if (DataContext is not MainViewModel vm || vm.SelectedNote == null)
        {
            return;
        }

        if (ConfigService.LoadConfig().ConfirmDelete)
        {
            var result = System.Windows.MessageBox.Show(
                $"确定删除笔记「{vm.SelectedNote.DisplayTitle}」吗？\n删除的笔记会移入回收站。",
                "闪念笔记",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        vm.DeleteSelected();
    }

    private void InitLineNumberHooks()
    {
        _lineNumbers = new Controls.LineNumberRenderer(ContentBox, LineNumbersHost, LineNumberPanel);
        _lineNumbers.SetForeground(
            Resources["Theme.LineNumberForeground"] as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Gray);
        ContentBox.Loaded += (_, _) => InitLineNumbers();
        ContentBox.TextChanged += (_, _) => ScheduleLineNumbers();
        ContentBox.SizeChanged += (_, _) => ScheduleLineNumbers();
    }

    private void InitLineNumbers()
    {
        if (_contentScrollViewer == null)
        {
            _contentScrollViewer = FindDescendant<System.Windows.Controls.ScrollViewer>(ContentBox);
            if (_contentScrollViewer != null)
            {
                _contentScrollViewer.ScrollChanged += (_, _) => _lineNumbers?.Update();
                _lineNumbers?.SetScrollViewer(_contentScrollViewer);
            }
        }

        _lineNumbers?.Update();
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                return typed;
            }

            var found = FindDescendant<T>(child);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void ScheduleLineNumbers()
    {
        _lineNumbers?.Invalidate();
        if (_lineNumbersDeferred)
        {
            return;
        }

        _lineNumbersDeferred = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(() =>
            {
                _lineNumbersDeferred = false;
                _lineNumbers?.Update();
            }));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);

        var shortcut = ConfigService.LoadShortcut();
        RegisterHotkey(handle, 0xBEEF, shortcut.ToggleWindow, ToggleWindow);
        RegisterHotkey(handle, 0xBEFF, shortcut.NewNote, CreateNewNote);
        Logger.Log($"快捷键注册: toggle={shortcut.ToggleWindow}({(_failedHotkeys.Contains(shortcut.ToggleWindow) ? "失败" : "成功")}), newNote={shortcut.NewNote}({(_failedHotkeys.Contains(shortcut.NewNote) ? "失败" : "成功")})");

        if (_failedHotkeys.Count > 0)
        {
            System.Windows.MessageBox.Show(
                "以下全局快捷键注册失败（可能已被其他程序占用），请修改 shortcut.json 后重启：\n" +
                string.Join("\n", _failedHotkeys),
                "闪念笔记",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RegisterHotkey(IntPtr handle, int id, string combo, Action action)
    {
        var hotkey = new HotkeyManager(handle, id);
        hotkey.Pressed += action;
        if (!hotkey.Register(combo))
        {
            _failedHotkeys.Add(combo);
        }

        _hotkeys[id] = hotkey;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyManager.WmHotkey && _hotkeys.TryGetValue(wParam.ToInt32(), out var hotkey))
        {
            hotkey.OnHotkeyMessage();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void ToggleWindow()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            ShowWindow();
        }
    }

    private void CreateNewNote()
    {
        ShowWindow();
        if (DataContext is MainViewModel vm)
        {
            vm.NewNote();
        }

        var conf = ConfigService.LoadConfig();
        if (string.Equals(conf.NewNoteFocus, "content", StringComparison.OrdinalIgnoreCase))
        {
            ContentBox.Focus();
        }
        else
        {
            TitleBox.Focus();
        }
    }

    private void NewNoteButton_Click(object sender, RoutedEventArgs e) => CreateNewNote();

    public void ShowWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void InitTrayIcon()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "闪念笔记",
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("显示/隐藏", null, (_, _) => ToggleWindow());
        menu.Items.Add("新建笔记", null, (_, _) => CreateNewNote());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("打开笔记目录", null, (_, _) => OpenDir(_storage?.Dir));
        menu.Items.Add("打开配置目录", null, (_, _) => OpenDir(ConfigService.BaseDir));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        _notifyIcon.ContextMenuStrip = menu;
    }

    private static void OpenDir(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true,
        });
    }

    private void ExitApp()
    {
        _isExiting = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.Flush();
        }

        foreach (var hotkey in _hotkeys.Values)
        {
            hotkey.Dispose();
        }

        _notifyIcon?.Dispose();
        base.OnClosing(e);
    }
}
