using System;
using System.Drawing;
using System.Windows.Forms;

namespace Scratchpad
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _appIcon;

        public TrayIconManager(Action onShowRequested, Action onSettingsRequested, Action onExitRequested, Action onLeftClick)
        {
            var menu = new ContextMenuStrip();
            // Bold marks Show as what double-clicking the icon does, as Windows' own tray menus do.
            var showItem = menu.Items.Add("Show", null, (s, e) => onShowRequested());
            showItem.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add("Settings…", null, (s, e) => onSettingsRequested());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (s, e) => onExitRequested());

            _appIcon = LoadAppIcon();
            _notifyIcon = new NotifyIcon
            {
                Icon = _appIcon ?? SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = menu,
                Text = "Scratchpad"
            };
            _notifyIcon.DoubleClick += (s, e) => onShowRequested();
            // MouseClick rather than Click: Click also fires for the right click that opens the menu.
            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    onLeftClick();
                }
            };
        }

        // On Windows 10/11 a balloon shows as a toast. Best-effort: a notification that fails
        // must never take the app down with it.
        public void ShowWarning(string text)
        {
            try
            {
                _notifyIcon.ShowBalloonTip(5000, "Scratchpad", text, ToolTipIcon.Warning);
            }
            catch (Exception)
            {
            }
        }

        // The icon is cosmetic: if the embedded resource can't be read, the generic icon still
        // gives the user a tray entry, which the app can't do without.
        private static Icon LoadAppIcon()
        {
            try
            {
                var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/Scratchpad.ico"));
                if (resource == null)
                {
                    return null;
                }
                using (var stream = resource.Stream)
                {
                    // Picks the frame closest to the tray's size instead of scaling the first one.
                    return new Icon(stream, SystemInformation.SmallIconSize);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The menu and its bold font are deliberately left to process exit: this runs from inside
        // the Exit item's own Click, and disposing the menu (or a font it may still paint with)
        // while it is mid-click risks an ObjectDisposedException on the way out.
        public void Dispose()
        {
            _notifyIcon.Dispose();
            _appIcon?.Dispose();
        }
    }
}
