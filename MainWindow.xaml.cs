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
    private System.Drawing.Icon? _trayIcon;
    private string? _iconPath;
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
                "FlashStickNote",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            _storage = new NoteStorage(ConfigService.BaseDir, "notes", conf.NotesFormat);
            Logger.Log($"回退到默认目录: {_storage.Dir}");
        }

        DataContext = new MainViewModel(_storage);
        ApplyAppIcon();
        ApplyConfig(conf);
        ApplyWindowShortcuts();
        InitEditor();
        InitEditorBinding();
        InitFontZoom();
        InitTrayIcon();
    }

    private void ApplyAppIcon()
    {
        var conf = ConfigService.LoadConfig();
        var iconPath = IconService.Resolve(conf.Icon);
        if (iconPath == null)
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(iconPath);
            var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            var frame = decoder.Frames
                .OrderBy(f => Math.Abs(f.Width - 32))
                .FirstOrDefault();
            if (frame != null)
            {
                Icon = frame;
            }

            _iconPath = iconPath;
            Logger.Log($"已应用图标: {iconPath}");
        }
        catch (Exception ex)
        {
            Logger.Log($"加载图标失败: {iconPath} ({ex.Message})");
        }
    }

    private AppConfig _appConfig = new();
    private System.Windows.Input.ModifierKeys _zoomModifier;
    private bool _zoomEnabled;
    private bool _editorSyncing;
    private Note? _hookedEditorNote;
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

        ContentBox.WordWrap = conf.WordWrap;
        ContentBox.ShowLineNumbers = conf.ShowLineNumbers;
        ContentBox.HorizontalScrollBarVisibility = conf.WordWrap
            ? System.Windows.Controls.ScrollBarVisibility.Disabled
            : System.Windows.Controls.ScrollBarVisibility.Auto;

        var editorForeground = Resources["Theme.EditorForeground"] as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Black;
        ContentBox.LineNumbersForeground = Resources["Theme.LineNumberForeground"] as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Gray;

        ContentBox.TextArea.TextView.LinkTextForegroundBrush = ParseBrush(conf.LinkColor)
            ?? System.Windows.Media.Brushes.DodgerBlue;
        ContentBox.TextArea.TextView.LinkTextUnderline = true;

        if (ParseBrush(conf.ContentColor) is { } contentBrush)
        {
            ContentBox.Foreground = contentBrush;
            ContentBox.TextArea.Caret.CaretBrush = contentBrush;
        }
        else
        {
            ContentBox.TextArea.Caret.CaretBrush = editorForeground;
        }
    }

    private void InitEditor()
    {
        var options = ContentBox.TextArea.Options;
        options.EnableHyperlinks = true;
        options.EnableEmailHyperlinks = false;
        options.RequireControlModifierForHyperlinkClick = true;

        var generators = ContentBox.TextArea.TextView.ElementGenerators;
        foreach (var generator in generators.OfType<ICSharpCode.AvalonEdit.Rendering.LinkElementGenerator>().ToList())
        {
            generators.Remove(generator);
        }

        generators.Add(new Controls.HttpLinkElementGenerator());
        Logger.Log("超链接识别已启用（仅 http/https，Ctrl+单击打开）");
    }

    private void InitEditorBinding()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        vm.PropertyChanged += OnVmPropertyChanged;
        ContentBox.TextChanged += OnEditorTextChanged;
        HookEditorNote(vm.SelectedNote);
        SyncEditorFromSelectedNote();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel vm)
        {
            return;
        }

        if (e.PropertyName == nameof(MainViewModel.SelectedNote))
        {
            HookEditorNote(vm.SelectedNote);
            SyncEditorFromSelectedNote();
        }
    }

    private void HookEditorNote(Note? note)
    {
        if (ReferenceEquals(_hookedEditorNote, note))
        {
            return;
        }

        if (_hookedEditorNote != null)
        {
            _hookedEditorNote.PropertyChanged -= OnNotePropertyChangedForEditor;
        }

        _hookedEditorNote = note;
        if (_hookedEditorNote != null)
        {
            _hookedEditorNote.PropertyChanged += OnNotePropertyChangedForEditor;
        }
    }

    private void OnNotePropertyChangedForEditor(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Note.Title) or nameof(Note.Content))
        {
            SyncEditorFromSelectedNote();
        }
    }

    private void SyncEditorFromSelectedNote()
    {
        var vm = DataContext as MainViewModel;
        var text = vm?.SelectedNote?.Content ?? "";
        if (ContentBox.Text == text)
        {
            return;
        }

        _editorSyncing = true;
        ContentBox.Text = text;
        _editorSyncing = false;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_editorSyncing)
        {
            return;
        }

        if (DataContext is MainViewModel vm && vm.SelectedNote != null && vm.SelectedNote.Content != ContentBox.Text)
        {
            vm.SelectedNote.Content = ContentBox.Text;
        }
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

        _zoomSaveTimer.Stop();
        _zoomSaveTimer.Start();
        e.Handled = true;
    }

    private void ApplyWindowShortcuts()
    {
        var shortcut = ConfigService.LoadShortcut();
        AddWindowShortcut(shortcut.NewNote, CreateNewNote, "新建笔记");
        AddWindowShortcut(shortcut.HideWindow, HideToTray, "隐藏到托盘");
        AddWindowShortcut(shortcut.ToggleWordWrap, ToggleWordWrap, "切换自动换行");
    }

    private void ToggleWordWrap()
    {
        var wrap = !ContentBox.WordWrap;
        ContentBox.WordWrap = wrap;
        ContentBox.HorizontalScrollBarVisibility = wrap
            ? System.Windows.Controls.ScrollBarVisibility.Disabled
            : System.Windows.Controls.ScrollBarVisibility.Auto;
        _appConfig.WordWrap = wrap;
        ConfigService.SaveConfig(_appConfig);
        Logger.Log($"已切换自动换行: {(wrap ? "开启" : "关闭")}");
    }

    private void AddWindowShortcut(string combo, Action action, string name)
    {
        try
        {
            var converter = new System.Windows.Input.KeyGestureConverter();
            if (converter.ConvertFromString(combo) is System.Windows.Input.KeyGesture gesture)
            {
                InputBindings.Add(new System.Windows.Input.KeyBinding(new RelayCommand(action), gesture));
                Logger.Log($"窗口内快捷键已注册: {name}={combo}");
            }
            else
            {
                Logger.Log($"窗口内快捷键解析失败: {name}={combo}");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"窗口内快捷键解析失败: {name}={combo} ({ex.Message})");
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
                "FlashStickNote",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        vm.DeleteSelected();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);

        var shortcut = ConfigService.LoadShortcut();
        RegisterHotkey(handle, 0xBEEF, shortcut.ToggleWindow, ToggleWindow);
        Logger.Log($"全局快捷键注册: toggleWindow={shortcut.ToggleWindow}({(_failedHotkeys.Contains(shortcut.ToggleWindow) ? "失败" : "成功")})（newNote/hideWindow 为窗口内快捷键）");

        if (_failedHotkeys.Count > 0)
        {
            System.Windows.MessageBox.Show(
                "以下全局快捷键注册失败（可能已被其他程序占用），请修改 shortcut.json 后重启：\n" +
                string.Join("\n", _failedHotkeys),
                "FlashStickNote",
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
        if (_iconPath != null)
        {
            try
            {
                _trayIcon = new System.Drawing.Icon(_iconPath);
            }
            catch
            {
            }
        }

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _trayIcon ?? System.Drawing.SystemIcons.Application,
            Text = "FlashStickNote",
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
        _trayIcon?.Dispose();
        base.OnClosing(e);
    }
}
