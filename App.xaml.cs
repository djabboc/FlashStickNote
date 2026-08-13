using System.Threading;
using System.Windows;
using System.Windows.Threading;
using FlashStickNote.Services;

namespace FlashStickNote;

public partial class App : System.Windows.Application
{
    private const string MutexName = "FlashStickNote_SingleInstance";
    private const string ShowSignalName = "FlashStickNote_ShowSignal";

    private static Mutex? _mutex;
    private static EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Logger.Init(ConfigService.BaseDir);
        Logger.Log("程序启动");

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Log($"未处理异常（已阻止崩溃）: {args.Exception}");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Log($"AppDomain 未处理异常: {args.ExceptionObject}");
        };

        var conf = ConfigService.LoadConfig();
        if (!conf.AllowMultiInstance)
        {
            _mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                Logger.Log("已有实例在运行，通知其显示窗口后退出");
                try
                {
                    using var signal = EventWaitHandle.OpenExisting(ShowSignalName);
                    signal.Set();
                }
                catch
                {
                }

                Shutdown();
                return;
            }

            Logger.Log("单实例模式：已持有互斥锁");
            _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignalName);
            var listener = new Thread(ListenForShowSignal) { IsBackground = true };
            listener.Start();
        }
        else
        {
            Logger.Log("允许多实例模式");
        }

        AutoStartService.Apply(conf.StartWithWindows);

        var window = new MainWindow();
        MainWindow = window;
        if (conf.SilentStart)
        {
            window.Opacity = 0;
            window.Show();
            window.Hide();
            window.Opacity = 1;
            Logger.Log("静默启动：已最小化到托盘");
        }
        else
        {
            window.Show();
        }
    }

    private void ListenForShowSignal()
    {
        while (true)
        {
            try
            {
                if (_showSignal == null || !_showSignal.WaitOne())
                {
                    return;
                }

                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    if (MainWindow is MainWindow win)
                    {
                        win.ShowWindow();
                    }
                }));
            }
            catch
            {
                return;
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showSignal?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
