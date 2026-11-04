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

            // Before MainWindow exists, so a dark start never flashes the light palette first.
            var themeManager = new ThemeManager(new ThemeStore(ThemeStore.DefaultFilePath), new RegistryPersonalizeKey());
            try
            {
                themeManager.Start();
            }
            catch
            {
                // Theme is cosmetic: a failure leaves the light palette from App.xaml in place
                // and must never stop the app starting.
            }

            var mainWindow = new MainWindow(themeManager);

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
                onSettingsRequested: mainWindow.OpenSettingsFromTray,
                onExitRequested: () =>
                {
                    mainWindow.MarkExiting();
                    themeManager.Dispose();
                    tray.Dispose();
                    guard.Dispose();
                    Shutdown();
                },
                onLeftClick: mainWindow.EndPeekEarly);

            // Last, so the tray icon and second-launch listener already exist. The first Show()
            // runs Window_Loaded, which restores the saved size and position as usual.
            if (mainWindow.ShowWindowOnStartup)
            {
                mainWindow.ShowAndActivate();
            }
        }
    }
}
