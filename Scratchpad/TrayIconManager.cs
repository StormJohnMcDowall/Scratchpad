using System;
using System.Drawing;
using System.Windows.Forms;

namespace Scratchpad
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _appIcon;

        public TrayIconManager(Action onShowRequested, Action onExitRequested)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => onShowRequested());
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

        public void Dispose()
        {
            _notifyIcon.Dispose();
            _appIcon?.Dispose();
        }
    }
}
