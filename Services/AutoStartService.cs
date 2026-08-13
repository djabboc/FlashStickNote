using Microsoft.Win32;

namespace FlashStickNote.Services;

public static class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FlashStickNote";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(ValueName, $"\"{exePath}\"");
                    Logger.Log($"已设置开机启动: {exePath}");
                }
            }
            else
            {
                key.DeleteValue(ValueName, false);
                Logger.Log("已取消开机启动");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"设置开机启动失败: {ex.Message}");
        }
    }
}
