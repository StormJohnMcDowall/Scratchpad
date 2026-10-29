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
        private bool _hasPendingChange;
        private bool _disposed;

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
                if (_disposed)
                {
                    return;
                }

                _latestContent = currentContent;
                _hasPendingChange = true;
                _timer.Change(_delay, Timeout.InfiniteTimeSpan);
            }
        }

        public void Flush()
        {
            string content;
            bool hasPendingChange;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                hasPendingChange = _hasPendingChange;
                content = _latestContent;
                if (hasPendingChange)
                {
                    _hasPendingChange = false;
                    _timer.Change(Timeout.Infinite, Timeout.Infinite);
                }
            }

            if (hasPendingChange)
            {
                TrySave(content);
            }
        }

        private void OnTimerElapsed(object state)
        {
            string content;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                content = _latestContent;
                _hasPendingChange = false;
            }

            TrySave(content);
        }

        private void TrySave(string content)
        {
            try
            {
                _save(content);
            }
            catch
            {
                // A transient failure (e.g. the file is momentarily locked by an antivirus
                // scan) must not take the process down. Retry on the next tick instead.
                lock (_lock)
                {
                    if (!_disposed)
                    {
                        _hasPendingChange = true;
                        _timer.Change(_delay, Timeout.InfiniteTimeSpan);
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _disposed = true;
            }

            _timer.Dispose();
        }
    }
}
