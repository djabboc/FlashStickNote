using System.IO;

namespace FlashStickNote.Services;

public static class Logger
{
    private static readonly object Lock = new();
    private static string? _path;

    public static void Init(string baseDir)
    {
        _path = Path.Combine(baseDir, "log.txt");
    }

    public static void Log(string message)
    {
        try
        {
            lock (Lock)
            {
                if (_path == null)
                {
                    return;
                }

                File.AppendAllText(_path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n");
            }
        }
        catch
        {
        }
    }
}
