using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace Scratchpad.Core
{
    public class SingleInstanceGuard : IDisposable
    {
        public const string DefaultMutexName = "Scratchpad-SingleInstance-Mutex";
        public const string DefaultPipeName = "Scratchpad-SingleInstance-Pipe";

        private readonly string _mutexName;
        private readonly string _pipeName;
        private readonly object _serverLock = new object();

        private Mutex _mutex;
        private bool _acquired;
        private volatile bool _disposed;
        private Thread _listenerThread;
        private NamedPipeServerStream _currentServer;

        public SingleInstanceGuard(string mutexName, string pipeName)
        {
            _mutexName = mutexName;
            _pipeName = pipeName;
        }

        public bool TryAcquire()
        {
            _mutex = new Mutex(true, _mutexName, out var createdNew);
            _acquired = createdNew;
            return createdNew;
        }

        public void ListenForShowRequests(Action onShowRequested)
        {
            _listenerThread = new Thread(() => ListenLoop(onShowRequested))
            {
                IsBackground = true
            };
            _listenerThread.Start();
        }

        private void ListenLoop(Action onShowRequested)
        {
            while (!_disposed)
            {
                NamedPipeServerStream server;
                try
                {
                    server = new NamedPipeServerStream(_pipeName, PipeDirection.In);
                    lock (_serverLock)
                    {
                        _currentServer = server;
                    }
                }
                catch
                {
                    return;
                }

                try
                {
                    server.WaitForConnection();
                    using (var reader = new StreamReader(server))
                    {
                        var line = reader.ReadLine();
                        if (InstanceMessage.IsShowCommand(line))
                        {
                            onShowRequested();
                        }
                    }
                }
                catch
                {
                    // Disposed while waiting for a connection; loop will exit on the next check.
                }
                finally
                {
                    server.Dispose();
                }
            }
        }

        public void NotifyExistingInstanceToShow()
        {
            // Best-effort: if the existing instance hasn't started listening yet, or has
            // already gone away, there is nothing the caller can do but continue regardless
            // (e.g. shut this second instance down). This must never throw.
            try
            {
                using (var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out))
                {
                    client.Connect(2000);
                    using (var writer = new StreamWriter(client) { AutoFlush = true })
                    {
                        writer.WriteLine(InstanceMessage.Show);
                    }
                }
            }
            catch
            {
                // See comment above.
            }
        }

        public void Dispose()
        {
            _disposed = true;

            lock (_serverLock)
            {
                _currentServer?.Dispose();
            }

            if (_acquired)
            {
                _mutex?.ReleaseMutex();
            }

            _mutex?.Dispose();
        }
    }
}
