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

            // Start listening before any slower startup work (registry I/O below) so a second
            // launch arriving in that window still reaches this instance instead of timing out.
            guard.ListenForShowRequests(() => Dispatcher.Invoke(mainWindow.ShowAndActivate));

            try
            {
                var startupRegistration = new StartupRegistration(new RegistryRunKey(), Process.GetCurrentProcess().MainModule.FileName);
                if (!startupRegistration.IsEnabledForCurrentPath())
                {
                    startupRegistration.Enable();
                }
            }
            catch
            {
                // Launching at login is a convenience, not something the app depends on to
                // function — e.g. HKCU\...\Run may be locked down by policy on this machine.
                // Losing it must never prevent the app itself from starting.
            }

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
