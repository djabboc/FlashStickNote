using System.IO;
using System.Text.Json;
using System.Threading;
using FlashStickNote.Models;

namespace FlashStickNote.Services;

public class NoteStorage
{
    private const string MarkerFileName = ".flashsticknote";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string[] SupportedExtensions = { ".json", ".txt", ".md" };

    private readonly string _dir;
    private readonly string _recycleDir;
    private readonly string _defaultFormat;
    private readonly Dictionary<string, DateTime> _selfWrites = new(StringComparer.OrdinalIgnoreCase);

    public NoteStorage(string baseDir, string notesDir, string format)
    {
        _dir = ResolveDir(baseDir, notesDir);
        _defaultFormat = NormalizeFormat(format);
        _recycleDir = Path.Combine(_dir, "recycle");
        Directory.CreateDirectory(_dir);
        Logger.Log($"NoteStorage 初始化: 目录={_dir}, 新笔记格式={_defaultFormat}");
        ImportFromDefaultDir(baseDir);
    }

    public string Dir => _dir;

    public string RecycleDir => _recycleDir;

    public bool WasRecentlyWritten(string path)
        => _selfWrites.TryGetValue(path, out var t) && (DateTime.Now - t).TotalMilliseconds < 2000;

    public Note? LoadFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        return LoadFileAs(path, ext);
    }

    private void MarkSelfWrite(string path)
    {
        _selfWrites[path] = DateTime.Now;
        if (_selfWrites.Count > 500)
        {
            var cutoff = DateTime.Now.AddMinutes(-5);
            foreach (var key in _selfWrites.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList())
            {
                _selfWrites.Remove(key);
            }
        }
    }

    private static string NormalizeFormat(string? format)
    {
        var value = (format ?? "json").Trim().ToLowerInvariant();
        return value is "json" or "txt" or "md" ? value : "json";
    }

    private static string? FormatOfPath(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext is "json" or "txt" or "md" ? ext : null;
    }

    private static string ResolveDir(string baseDir, string? notesDir)
    {
        var path = string.IsNullOrWhiteSpace(notesDir) ? "notes" : notesDir.Trim();
        path = Environment.ExpandEnvironmentVariables(path);
        if (path.StartsWith("~/") || path.StartsWith("~\\"))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                path[2..]);
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
    }

    private void ImportFromDefaultDir(string baseDir)
    {
        var defaultDir = Path.Combine(baseDir, "notes");
        if (string.Equals(Path.GetFullPath(defaultDir), Path.GetFullPath(_dir), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(defaultDir) || File.Exists(Path.Combine(_dir, MarkerFileName)))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(defaultDir))
        {
            try
            {
                var dest = Path.Combine(_dir, Path.GetFileName(file));
                if (!File.Exists(dest))
                {
                    File.Move(file, dest);
                    Logger.Log($"笔记导入: {file} -> {dest}");
                }
            }
            catch
            {
            }
        }

        try
        {
            File.WriteAllText(Path.Combine(_dir, MarkerFileName), "FlashStickNote");
        }
        catch
        {
        }
    }

    public IEnumerable<Note> LoadAll()
    {
        var notes = new List<Note>();
        foreach (var ext in SupportedExtensions)
        {
            foreach (var file in Directory.GetFiles(_dir, "*" + ext))
            {
                try
                {
                    var note = LoadFileAs(file, ext);
                    if (note != null && !note.IsEmpty)
                    {
                        notes.Add(note);
                    }
                }
                catch
                {
                }
            }
        }

        return notes.OrderByDescending(n => n.UpdatedAt);
    }

    private Note? LoadFileAs(string file, string ext)
    {
        var text = ReadAllTextWithRetry(file);
        if (text == null)
        {
            return null;
        }

        if (string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
        {
            var note = JsonSerializer.Deserialize<Note>(text, Options);
            if (note != null)
            {
                note.StoredFileName = file;
            }

            return note;
        }

        var markdown = string.Equals(ext, ".md", StringComparison.OrdinalIgnoreCase);
        var title = Path.GetFileNameWithoutExtension(file);
        var content = StripTitleLine(text, title, markdown);
        return new Note
        {
            Title = title,
            Content = content,
            CreatedAt = File.GetCreationTime(file),
            UpdatedAt = File.GetLastWriteTime(file),
            StoredFileName = file,
        };
    }

    private static string StripTitleLine(string text, string title, bool markdown)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var lines = text.Split('\n');
        var first = lines[0].TrimEnd('\r').Trim();
        var expected = markdown ? "# " + title : title;
        if (!string.Equals(first, expected, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        if (lines.Length >= 2 && !string.IsNullOrWhiteSpace(lines[1].TrimEnd('\r')))
        {
            return text;
        }

        return string.Join("\n", lines.Skip(1)).TrimStart('\r', '\n');
    }

    private static string? ReadAllTextWithRetry(string file)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                return File.ReadAllText(file);
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(120);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(120);
            }
        }

        return null;
    }

    public Note Create() => new Note();

    public bool Save(Note note)
    {
        if (note.IsEmpty)
        {
            var emptyPath = note.StoredFileName;
            if (string.IsNullOrEmpty(emptyPath) || !File.Exists(emptyPath))
            {
                note.StoredFileName = null;
                return true;
            }

            try
            {
                File.Delete(emptyPath);
                MarkSelfWrite(emptyPath);
                note.StoredFileName = null;
                Logger.Log($"Empty note file deleted: {emptyPath}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to delete empty note file: {emptyPath} ({ex.Message})");
                return false;
            }
        }

        var originalPath = note.StoredFileName;
        var format = originalPath != null ? FormatOfPath(originalPath) : null;
        format = string.IsNullOrEmpty(format) ? _defaultFormat : format;
        note.UpdatedAt = DateTime.Now;
        var newPath = format == "json" ? GetJsonPath(note) : BuildPlainPath(note, format);
        var text = format == "json"
            ? JsonSerializer.Serialize(note, Options)
            : FormatPlain(note);
        var tempPath = Path.Combine(
            _dir,
            $".{Path.GetFileName(newPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(tempPath, text);
            File.Move(tempPath, newPath, true);
            note.StoredFileName = newPath;
            MarkSelfWrite(newPath);

            if (!string.IsNullOrEmpty(originalPath) &&
                !string.Equals(originalPath, newPath, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(originalPath))
            {
                try
                {
                    File.Delete(originalPath);
                    MarkSelfWrite(originalPath);
                }
                catch (Exception ex)
                {
                    Logger.Log($"New note saved but old file remains: {originalPath} ({ex.Message})");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Failed to save note: {Path.GetFileName(newPath)} ({ex.Message})");
            return false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch
            {
            }
        }
    }

    public bool Delete(Note note)
    {
        var path = note.StoredFileName;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            note.StoredFileName = null;
            return true;
        }

        Directory.CreateDirectory(_recycleDir);
        var name = Path.GetFileName(path);
        var dest = Path.Combine(_recycleDir, name);
        var index = 2;
        while (File.Exists(dest))
        {
            dest = Path.Combine(
                _recycleDir,
                $"{Path.GetFileNameWithoutExtension(name)} ({index}){Path.GetExtension(name)}");
            index++;
        }

        try
        {
            File.Move(path, dest);
            note.StoredFileName = null;
            MarkSelfWrite(path);
            MarkSelfWrite(dest);
            Logger.Log($"Note moved to recycle: {name}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Log($"Recycle move failed, attempting copy/delete: {ex.Message}");
            try
            {
                File.Copy(path, dest);
                File.Delete(path);
                note.StoredFileName = null;
                MarkSelfWrite(path);
                MarkSelfWrite(dest);
                Logger.Log($"Note copied to recycle: {name}");
                return true;
            }
            catch (Exception fallbackEx)
            {
                Logger.Log($"Failed to recycle note: {name} ({fallbackEx.Message})");
                return false;
            }
        }
    }

    private string GetJsonPath(Note note) => Path.Combine(_dir, note.Id + ".json");

    private string BuildPlainPath(Note note, string format)
    {
        var ext = format == "md" ? ".md" : ".txt";
        var baseName = SanitizeFileName(note.EffectiveTitle);
        var path = Path.Combine(_dir, baseName + ext);
        var index = 2;
        while (File.Exists(path) &&
               !string.Equals(note.StoredFileName, path, StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(_dir, $"{baseName} ({index}){ext}");
            index++;
        }

        return path;
    }

    private static string SanitizeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = title
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Where(c => !invalid.Contains(c))
            .ToArray();
        var name = new string(chars).Trim();
        if (name.Length > 60)
        {
            name = name[..60].TrimEnd();
        }

        return string.IsNullOrWhiteSpace(name) ? "无标题" : name;
    }

    private static string FormatPlain(Note note) => note.Content ?? "";
}
