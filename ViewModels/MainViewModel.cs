using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using FlashStickNote.Models;
using FlashStickNote.Services;

namespace FlashStickNote.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly NoteStorage _storage;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _reloadTimer;
    private readonly HashSet<string> _reloadQueue = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private Note? _pendingSave;
    private Note? _selectedNote;
    private readonly int _listContentLines;
    private System.ComponentModel.ICollectionView? _notesView;
    private string _searchText = "";
    private bool _useRegex;
    private bool _matchWholeWord;
    private bool _matchCase;
    private string _searchError = "";
    private readonly Dictionary<string, int> _loadFailures = new(StringComparer.OrdinalIgnoreCase);

    public ObservableCollection<Note> Notes { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value ?? "";
            OnPropertyChanged();
            RefreshFilter();
        }
    }

    public Note? SelectedNote
    {
        get => _selectedNote;
        set
        {
            if (ReferenceEquals(_selectedNote, value))
            {
                return;
            }

            Flush();
            if (_selectedNote != null)
            {
                _selectedNote.PropertyChanged -= OnNotePropertyChanged;
                RemoveIfEmpty(_selectedNote);
            }

            _selectedNote = value;
            if (_selectedNote != null)
            {
                _selectedNote.PropertyChanged += OnNotePropertyChanged;
            }

            OnPropertyChanged();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool UseRegex
    {
        get => _useRegex;
        set => SetSearchOption(ref _useRegex, value);
    }

    public bool MatchWholeWord
    {
        get => _matchWholeWord;
        set => SetSearchOption(ref _matchWholeWord, value);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => SetSearchOption(ref _matchCase, value);
    }

    public string SearchError
    {
        get => _searchError;
        private set
        {
            if (_searchError != value)
            {
                _searchError = value;
                OnPropertyChanged();
            }
        }
    }

    public MainViewModel(NoteStorage storage)
    {
        _storage = storage;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Flush();
        };

        _reloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _reloadTimer.Tick += (_, _) =>
        {
            _reloadTimer.Stop();
            ProcessReloadQueue();
        };

        _listContentLines = ConfigService.LoadConfig().ListContentLines;

        foreach (var note in storage.LoadAll())
        {
            note.PreviewLineCount = _listContentLines;
            Notes.Add(note);
        }

        SelectedNote = Notes.FirstOrDefault();
        _notesView = CollectionViewSource.GetDefaultView(Notes);
        InitWatcher();
    }

    private void RefreshFilter()
    {
        if (_notesView == null)
        {
            return;
        }

        var matcher = new NoteSearchMatcher(_searchText, _useRegex, _matchWholeWord, _matchCase);
        SearchError = matcher.Error ?? "";
        var term = _searchText.Trim();
        if (term.Length == 0)
        {
            _notesView.Filter = null;
        }
        else
        {
            _notesView.Filter = item => item is Note note &&
                (matcher.IsMatch(note.Title) || matcher.IsMatch(note.Content));
        }

        _notesView.Refresh();
    }

    private void SetSearchOption(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
        RefreshFilter();
    }

    private void InitWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(_storage.Dir)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                InternalBufferSize = 64 * 1024,
            };
            _watcher.Changed += OnWatcherChanged;
            _watcher.Created += OnWatcherCreated;
            _watcher.Deleted += OnWatcherDeleted;
            _watcher.Renamed += OnWatcherRenamed;
            _watcher.Error += (_, _) => Logger.Log("笔记目录监控出错");
            _watcher.EnableRaisingEvents = true;
            Logger.Log($"已开启笔记目录监控: {_storage.Dir}");
        }
        catch (Exception ex)
        {
            Logger.Log($"笔记目录监控启动失败: {ex.Message}");
        }
    }

    private static bool IsSupportedFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".md", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsInRecycle(string path)
        => path.StartsWith(
            _storage.RecycleDir + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedFile(e.FullPath) || IsInRecycle(e.FullPath))
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _reloadQueue.Add(e.FullPath);
            _reloadTimer.Stop();
            _reloadTimer.Start();
        });
    }

    private void OnWatcherCreated(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedFile(e.FullPath) || IsInRecycle(e.FullPath))
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _reloadQueue.Add(e.FullPath);
            _reloadTimer.Stop();
            _reloadTimer.Start();
        });
    }

    private void OnWatcherDeleted(object sender, FileSystemEventArgs e)
    {
        if (!IsSupportedFile(e.FullPath) || IsInRecycle(e.FullPath))
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() => HandleDeleted(e.FullPath));
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsSupportedFile(e.FullPath) || IsInRecycle(e.FullPath))
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var note = Notes.FirstOrDefault(n =>
                    string.Equals(n.StoredFileName, e.OldFullPath, StringComparison.OrdinalIgnoreCase));
                if (note != null)
                {
                    note.StoredFileName = e.FullPath;
                    ReloadFromDisk(e.FullPath, note);
                    Logger.Log($"检测到文件重命名: {Path.GetFileName(e.OldFullPath)} -> {Path.GetFileName(e.FullPath)}");
                }
                else
                {
                    HandleCreated(e.FullPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"处理文件重命名失败: {ex.Message}");
            }
        });
    }

    private void ProcessReloadQueue()
    {
        var paths = _reloadQueue.ToList();
        _reloadQueue.Clear();
        foreach (var path in paths)
        {
            try
            {
                if (_storage.WasRecentlyWritten(path))
                {
                    continue;
                }

                if (!File.Exists(path))
                {
                    continue;
                }

                var note = Notes.FirstOrDefault(n =>
                    string.Equals(n.StoredFileName, path, StringComparison.OrdinalIgnoreCase));
                if (note == null)
                {
                    if (!HandleCreated(path))
                    {
                        RequeueLater(path);
                    }

                    continue;
                }

                if (ReferenceEquals(note, _pendingSave))
                {
                    continue;
                }

                ReloadFromDisk(path, note);
            }
            catch (Exception ex)
            {
                Logger.Log($"同步外部修改失败: {Path.GetFileName(path)} ({ex.Message})");
            }
        }
    }

    private void RequeueLater(string path)
    {
        if (_loadFailures.TryGetValue(path, out var count) && count >= 2)
        {
            return;
        }

        _loadFailures[path] = count + 1;
        _ = Task.Delay(1500).ContinueWith(_ =>
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                _reloadQueue.Add(path);
                _reloadTimer.Stop();
                _reloadTimer.Start();
            });
        });
    }

    private bool HandleCreated(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return true;
            }

            if (Notes.Any(n => string.Equals(n.StoredFileName, path, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var note = _storage.LoadFile(path);
            if (note == null)
            {
                return false;
            }

            if (note.IsEmpty)
            {
                return true;
            }

            note.PreviewLineCount = _listContentLines;
            Notes.Insert(0, note);
            _loadFailures.Remove(path);
            Logger.Log($"检测到外部新增笔记: {Path.GetFileName(path)}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"处理外部新增笔记失败: {Path.GetFileName(path)} ({ex.Message})");
            return false;
        }
    }

    private void HandleDeleted(string path)
    {
        try
        {
            var note = Notes.FirstOrDefault(n =>
                string.Equals(n.StoredFileName, path, StringComparison.OrdinalIgnoreCase));
            if (note == null)
            {
                return;
            }

            var index = Notes.IndexOf(note);
            var wasSelected = ReferenceEquals(note, _selectedNote);

            if (ReferenceEquals(note, _pendingSave))
            {
                _pendingSave = null;
                _saveTimer.Stop();
            }

            if (wasSelected)
            {
                if (_selectedNote != null)
                {
                    _selectedNote.PropertyChanged -= OnNotePropertyChanged;
                }

                _selectedNote = null;
                OnPropertyChanged(nameof(SelectedNote));
                CommandManager.InvalidateRequerySuggested();
            }

            Notes.Remove(note);
            Logger.Log($"检测到外部删除笔记: {Path.GetFileName(path)}");

            if (wasSelected && Notes.Count > 0)
            {
                SelectedNote = Notes[Math.Min(Math.Max(index, 0), Notes.Count - 1)];
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"处理外部删除笔记失败: {Path.GetFileName(path)} ({ex.Message})");
        }
    }

    private void ReloadFromDisk(string path, Note note)
    {
        var loaded = _storage.LoadFile(path);
        if (loaded == null || loaded.IsEmpty)
        {
            return;
        }

        note.PropertyChanged -= OnNotePropertyChanged;
        note.Title = loaded.Title;
        note.Content = loaded.Content;
        note.UpdatedAt = loaded.UpdatedAt;
        note.CreatedAt = loaded.CreatedAt;
        note.StoredFileName = path;
        note.PropertyChanged += OnNotePropertyChanged;
        Logger.Log($"已同步外部修改: {Path.GetFileName(path)}");
    }

    public void NewNote()
    {
        var note = _storage.Create();
        note.PreviewLineCount = _listContentLines;
        Notes.Insert(0, note);
        SelectedNote = note;
    }

    public void DeleteSelected()
    {
        if (SelectedNote == null)
        {
            return;
        }

        var note = SelectedNote;
        var index = Notes.IndexOf(note);
        SelectedNote = null;
        Notes.Remove(note);
        _storage.Delete(note);

        if (Notes.Count > 0)
        {
            SelectedNote = Notes[Math.Min(Math.Max(index, 0), Notes.Count - 1)];
        }
    }

    private void RemoveIfEmpty(Note note)
    {
        if (!note.IsEmpty)
        {
            return;
        }

        Notes.Remove(note);
        _storage.Delete(note);
    }

    private void OnNotePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Note.Title) or nameof(Note.Content))
        {
            _pendingSave = sender as Note;
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    public void Flush()
    {
        _saveTimer.Stop();
        if (_pendingSave != null)
        {
            _storage.Save(_pendingSave);
            _pendingSave = null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
