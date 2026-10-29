using System;
using System.Windows.Forms;

namespace Scratchpad
{
    public class TrayIconManager : IDisposable
    {
        private readonly NotifyIcon _notifyIcon;

        public TrayIconManager(Action onShowRequested, Action onExitRequested)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (s, e) => onShowRequested());
            menu.Items.Add("Exit", null, (s, e) => onExitRequested());

            _notifyIcon = new NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = menu,
                Text = "Scratchpad"
            };
            _notifyIcon.DoubleClick += (s, e) => onShowRequested();
        }

        public void Dispose()
        {
            _notifyIcon.Dispose();
        }
    }
}
