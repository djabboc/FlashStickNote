using System.IO;
using System.Text.Json;

namespace FlashStickNote.Services;

public class ShortcutConfig
{
    public string ToggleWindow { get; set; } = "Ctrl+Shift+N";
    public string NewNote { get; set; } = "Ctrl+N";
    public string HideWindow { get; set; } = "Ctrl+W";
    public string FontZoom { get; set; } = "Ctrl+Wheel";
    public string ToggleWordWrap { get; set; } = "Alt+Z";
}

public class AppConfig
{
    public string FontFamily { get; set; } = "Microsoft YaHei UI";
    public double FontSize { get; set; } = 14.0;
    public double TitleFontSize { get; set; } = 18.0;
    public double NoteListFontSize { get; set; } = 13.0;
    public string NotesDir { get; set; } = "notes";
    public string NotesFormat { get; set; } = "json";
    public bool AllowMultiInstance { get; set; } = false;
    public string NewNoteFocus { get; set; } = "title";
    public int ListContentLines { get; set; } = 0;
    public bool ShowLineNumbers { get; set; } = false;
    public bool WordWrap { get; set; } = true;
    public string Theme { get; set; } = "light";
    public bool ConfirmDelete { get; set; } = true;
    public string ListTitleFontFamily { get; set; } = "";
    public double ListTitleFontSize { get; set; } = 13.0;
    public string ListPreviewFontFamily { get; set; } = "";
    public double ListPreviewFontSize { get; set; } = 11.0;
    public string TitleFontFamily { get; set; } = "";
    public string TitleColor { get; set; } = "";
    public string ContentFontFamily { get; set; } = "";
    public string ContentColor { get; set; } = "";
    public string LinkColor { get; set; } = "";
    public string Icon { get; set; } = "";
    public bool StartWithWindows { get; set; } = false;
    public bool SilentStart { get; set; } = false;
}

public static class ConfigService
{
    private const string ShortcutFileName = "shortcut.json";
    private const string ConfigFileName = "conf.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string BaseDir => AppContext.BaseDirectory;

    public static ShortcutConfig LoadShortcut()
        => Load(ShortcutFileName, () => new ShortcutConfig());

    public static AppConfig LoadConfig()
        => Load(ConfigFileName, () => new AppConfig());

    public static void SaveConfig(AppConfig conf)
        => Save(Path.Combine(BaseDir, ConfigFileName), conf);

    private static T Load<T>(string fileName, Func<T> factory) where T : class, new()
    {
        var path = Path.Combine(BaseDir, fileName);
        if (!File.Exists(path))
        {
            var def = factory();
            Save(path, def);
            return def;
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
            if (value != null)
            {
                return value;
            }

            Logger.Log($"读取 {fileName} 失败（反序列化结果为空），使用默认值");
        }
        catch (Exception ex)
        {
            Logger.Log($"读取 {fileName} 失败: {ex.Message}，使用默认值");
        }

        return factory();
    }

    private static void Save<T>(string path, T value)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
        }
        catch
        {
        }
    }
}
