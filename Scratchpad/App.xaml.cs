using System.Diagnostics;
using System.Windows;
using Scratchpad.Core;

namespace Scratchpad
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var guard = new SingleInstanceGuard(SingleInstanceGuard.DefaultMutexName, SingleInstanceGuard.DefaultPipeName);

            if (!guard.TryAcquire())
            {
                guard.NotifyExistingInstanceToShow();
                Shutdown();
                return;
            }

            var mainWindow = new MainWindow();

            new StartupRegistration(new RegistryRunKey(), Process.GetCurrentProcess().MainModule.FileName).Enable();

            guard.ListenForShowRequests(() => Dispatcher.Invoke(mainWindow.ShowAndActivate));

            TrayIconManager tray = null;
            tray = new TrayIconManager(
                onShowRequested: mainWindow.ShowAndActivate,
                onExitRequested: () =>
                {
                    mainWindow.MarkExiting();
                    tray.Dispose();
                    guard.Dispose();
                    Shutdown();
                });
        }
    }
}
