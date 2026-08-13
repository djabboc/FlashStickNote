using System.IO;
using System.Text.Json;

namespace FlashStickNote.Services;

public class ThemeColors
{
    public string Name { get; set; } = "浅色";
    public string WindowBackground { get; set; } = "#F5F5F5";
    public string ListBackground { get; set; } = "#F5F5F5";
    public string ListHeaderBackground { get; set; } = "#ECECEC";
    public string ListHeaderForeground { get; set; } = "#333333";
    public string ListForeground { get; set; } = "#1E1E1E";
    public string ListSecondaryForeground { get; set; } = "#999999";
    public string ListTertiaryForeground { get; set; } = "#BBBBBB";
    public string ListSelectedBackground { get; set; } = "#D6E4F0";
    public string ListSelectedForeground { get; set; } = "#1E1E1E";
    public string EditorBackground { get; set; } = "#FFFFFF";
    public string EditorForeground { get; set; } = "#1E1E1E";
    public string LineNumberBackground { get; set; } = "#FAFAFA";
    public string LineNumberForeground { get; set; } = "#999999";
    public string BorderColor { get; set; } = "#DDDDDD";
    public string SplitterColor { get; set; } = "#E0E0E0";
}

public static class ThemeService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static readonly Dictionary<string, ThemeColors> BuiltIn = new()
    {
        ["light"] = new ThemeColors(),
        ["green"] = new ThemeColors
        {
            Name = "护眼绿",
            WindowBackground = "#C7EDCC",
            ListBackground = "#BFE3C5",
            ListHeaderBackground = "#B5DDBB",
            ListHeaderForeground = "#2B3B2B",
            ListForeground = "#243324",
            ListSecondaryForeground = "#6B8A6B",
            ListTertiaryForeground = "#7E9C7E",
            ListSelectedBackground = "#A3D4AC",
            ListSelectedForeground = "#1F2E1F",
            EditorBackground = "#C7EDCC",
            EditorForeground = "#263626",
            LineNumberBackground = "#BFE3C5",
            LineNumberForeground = "#6B8A6B",
            BorderColor = "#9CC9A3",
            SplitterColor = "#9CC9A3",
        },
        ["paper"] = new ThemeColors
        {
            Name = "纸张黄",
            WindowBackground = "#F6F0DC",
            ListBackground = "#F0E9D2",
            ListHeaderBackground = "#E9E0C4",
            ListHeaderForeground = "#5B4F33",
            ListForeground = "#4A3F28",
            ListSecondaryForeground = "#9A8B64",
            ListTertiaryForeground = "#A89A75",
            ListSelectedBackground = "#E3D7AE",
            ListSelectedForeground = "#4A3F28",
            EditorBackground = "#FBF6E3",
            EditorForeground = "#4A3F28",
            LineNumberBackground = "#F0E9D2",
            LineNumberForeground = "#9A8B64",
            BorderColor = "#E2D5A8",
            SplitterColor = "#E2D5A8",
        },
        ["dark"] = new ThemeColors
        {
            Name = "深色",
            WindowBackground = "#1E1E1E",
            ListBackground = "#252526",
            ListHeaderBackground = "#2D2D30",
            ListHeaderForeground = "#DDDDDD",
            ListForeground = "#D4D4D4",
            ListSecondaryForeground = "#9E9E9E",
            ListTertiaryForeground = "#7A7A7A",
            ListSelectedBackground = "#37373D",
            ListSelectedForeground = "#FFFFFF",
            EditorBackground = "#1E1E1E",
            EditorForeground = "#D4D4D4",
            LineNumberBackground = "#252526",
            LineNumberForeground = "#6A6A6A",
            BorderColor = "#3F3F46",
            SplitterColor = "#3F3F46",
        },
    };

    public static string ThemeDir => Path.Combine(ConfigService.BaseDir, "theme");

    public static void EnsureDefaultThemes()
    {
        Directory.CreateDirectory(ThemeDir);
        foreach (var (key, theme) in BuiltIn)
        {
            var path = Path.Combine(ThemeDir, key + ".json");
            if (!File.Exists(path))
            {
                try
                {
                    File.WriteAllText(path, JsonSerializer.Serialize(theme, Options));
                }
                catch
                {
                }
            }
        }
    }

    public static ThemeColors Load(string? name)
    {
        EnsureDefaultThemes();
        var key = (name ?? "light").Trim().ToLowerInvariant();

        var path = Path.Combine(ThemeDir, key + ".json");
        if (File.Exists(path))
        {
            try
            {
                var theme = JsonSerializer.Deserialize<ThemeColors>(File.ReadAllText(path), Options);
                if (theme != null)
                {
                    return theme;
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"主题文件读取失败: {path} ({ex.Message})");
            }
        }

        return BuiltIn.TryGetValue(key, out var fallback) ? fallback : BuiltIn["light"];
    }
}
