using System.IO;

namespace FlashStickNote.Services;

public static class IconService
{
    public const string DefaultIconFileName = "FlashStickNote.ico";

    public static string? Resolve(string? configured)
    {
        var baseDir = ConfigService.BaseDir;

        if (!string.IsNullOrWhiteSpace(configured))
        {
            var custom = Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(baseDir, configured);
            if (File.Exists(custom))
            {
                return custom;
            }

            Logger.Log($"配置的图标文件不存在: {custom}，改用默认图标");
        }

        var defaultPath = Path.Combine(baseDir, DefaultIconFileName);
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        return TryExtractDefault(defaultPath) ? defaultPath : null;
    }

    private static bool TryExtractDefault(string path)
    {
        try
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(DefaultIconFileName);
            if (stream == null)
            {
                return false;
            }

            using var fs = File.Create(path);
            stream.CopyTo(fs);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
