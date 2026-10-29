using System;
using System.Threading;

namespace Scratchpad.Core
{
    public class DebouncedAutosaveCoordinator : IDisposable
    {
        private readonly Action<string> _save;
        private readonly TimeSpan _delay;
        private readonly Timer _timer;
        private readonly object _lock = new object();
        private string _latestContent;

        public DebouncedAutosaveCoordinator(Action<string> save, TimeSpan delay)
        {
            _save = save;
            _delay = delay;
            _timer = new Timer(OnTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void NotifyChanged(string currentContent)
        {
            lock (_lock)
            {
                _latestContent = currentContent;
                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }

        private void OnTimerElapsed(object state)
        {
            string content;
            lock (_lock)
            {
                content = _latestContent;
            }

            _save(content);
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
