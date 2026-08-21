using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Interop;
using MutePilot.SingleInstance;
using MutePilot.Startup;

namespace MutePilot;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private SingleInstanceService? _singleInstanceService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var options = ApplicationLaunchOptions.Parse(e.Args);

        if (options.StartupTaskCommand is StartupTaskCommand startupTaskCommand)
        {
            var exitCode = new StartupService().RunElevatedTaskCommand(startupTaskCommand);
            Shutdown(exitCode);
            return;
        }

        _singleInstanceService = new SingleInstanceService();
        var handoffWait = options.IsElevationHandoff
            ? TimeSpan.FromSeconds(20)
            : TimeSpan.Zero;

        if (!_singleInstanceService.TryAcquire(handoffWait))
        {
            if (!options.StartInBackground)
            {
                MessageBox.Show(
                    "MutePilot이 이미 실행 중입니다. 시스템 트레이를 확인하세요.",
                    "MutePilot",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        _ = new WindowInteropHelper(mainWindow).EnsureHandle();
        mainWindow.StartServices();

        if (!options.StartInBackground)
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceService?.Dispose();
        _singleInstanceService = null;
        base.OnExit(e);
    }
}
