using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FlashStickNote.Models;

public class Note : INotifyPropertyChanged
{
    private string _title = "";
    private string _content = "";

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
                OnPropertyChanged(nameof(ContentPreview));
            }
        }
    }

    [JsonIgnore]
    public string? StoredFileName { get; set; }

    [JsonIgnore]
    public int PreviewLineCount { get; set; }

    [JsonIgnore]
    public string ContentPreview
    {
        get
        {
            if (PreviewLineCount <= 0 || string.IsNullOrWhiteSpace(_content))
            {
                return "";
            }

            var lines = _content.Replace("\r\n", "\n").Split('\n');
            return string.Join("\n", lines.Take(PreviewLineCount)).TrimEnd('\n', '\r');
        }
    }

    [JsonIgnore]
    public string EffectiveTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_title))
            {
                return _title;
            }

            var line = GetFirstLine(_content);
            return string.IsNullOrWhiteSpace(line) ? "" : line;
        }
    }

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(EffectiveTitle) ? "无标题" : EffectiveTitle;

    [JsonIgnore]
    public bool IsEmpty => string.IsNullOrWhiteSpace(_title) && string.IsNullOrWhiteSpace(_content);

    private static string GetFirstLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var idx = text.IndexOf('\n');
        var line = idx >= 0 ? text[..idx] : text;
        return line.TrimEnd('\r').Trim();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
