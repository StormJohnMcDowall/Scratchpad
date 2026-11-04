using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Scratchpad.Core;

namespace Scratchpad
{
    /// <summary>
    /// The system-wide key that brings the note forward. Windows delivers WM_HOTKEY to a window,
    /// so this owns a message-only one: the app usually starts hidden (the main window may never
    /// have been shown), and this handle has to outlive every Hide/Show of the note.
    /// Every member swallows failures — a hotkey is a convenience and must never crash the app.
    /// </summary>
    public sealed class GlobalHotkey : IDisposable
    {
        private const int WmHotkey = 0x0312;
        private const uint ModNoRepeat = 0x4000;
        private static readonly IntPtr HwndMessage = new IntPtr(-3);

        // Two IDs, alternated, so a change can hold the new key before releasing the old one.
        private const int FirstId = 1;
        private const int SecondId = 2;

        private readonly HwndSource _source;
        private int _registeredId;

        public event Action Pressed;

        // The user's chosen key, whether or not Windows currently has it registered for us.
        public HotkeySettings Current { get; private set; }

        public bool IsRegistered => _registeredId != 0;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        public GlobalHotkey()
        {
            try
            {
                _source = new HwndSource(new HwndSourceParameters("ScratchpadHotkey")
                {
                    ParentWindow = HwndMessage,
                    Width = 0,
                    Height = 0,
                    WindowStyle = 0
                });
                _source.AddHook(WndProc);
            }
            catch
            {
                // Without the window every registration simply fails, and the user is told so.
                _source = null;
            }
        }

        /// <summary>Registers the saved key. True when it's working, or when it's None.</summary>
        public bool Start(HotkeySettings settings)
        {
            Unregister();
            Current = settings;
            if (!settings.IsNone && TryRegister(settings, FirstId))
            {
                _registeredId = FirstId;
            }
            return IsRegistered || Current.IsNone;
        }

        /// <summary>
        /// Switches to the next key. The new key is held before the old one is released, so a
        /// refusal (another program owns it) changes nothing and the old key keeps working.
        /// </summary>
        public bool TryChange(HotkeySettings next)
        {
            if (next.IsNone)
            {
                Unregister();
                Current = HotkeySettings.None;
                return true;
            }
            if (next == Current && IsRegistered)
            {
                return true;
            }

            var spareId = _registeredId == FirstId ? SecondId : FirstId;
            if (!TryRegister(next, spareId))
            {
                return false;
            }

            Unregister();
            _registeredId = spareId;
            Current = next;
            return true;
        }

        // While Settings records a key, so pressing the current hotkey reaches the box instead.
        public void Suspend()
        {
            Unregister();
        }

        /// <summary>
        /// Registers Current again (after Suspend, or retrying a key that was taken at startup).
        /// True when it's working, or when it's None.
        /// </summary>
        public bool Resume()
        {
            if (!Current.IsNone && !IsRegistered && TryRegister(Current, FirstId))
            {
                _registeredId = FirstId;
            }
            return IsRegistered || Current.IsNone;
        }

        public void Dispose()
        {
            Unregister();
            try
            {
                if (_source != null)
                {
                    _source.RemoveHook(WndProc);
                    _source.Dispose();
                }
            }
            catch
            {
                // Shutting down anyway; Windows releases the hotkey with the process.
            }
        }

        private bool TryRegister(HotkeySettings hotkey, int id)
        {
            if (_source == null || !Hotkey.IsValid(hotkey))
            {
                return false;
            }

            try
            {
                return RegisterHotKey(_source.Handle, id, (uint)hotkey.Modifiers | ModNoRepeat, (uint)hotkey.VirtualKey);
            }
            catch
            {
                return false;
            }
        }

        private void Unregister()
        {
            if (_registeredId == 0)
            {
                return;
            }

            try
            {
                UnregisterHotKey(_source.Handle, _registeredId);
            }
            catch
            {
                // The ID is forgotten either way; a stale registration dies with the process.
            }
            _registeredId = 0;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotkey)
            {
                var id = wParam.ToInt32();
                if (id == FirstId || id == SecondId)
                {
                    handled = true;
                    try
                    {
                        Pressed?.Invoke();
                    }
                    catch
                    {
                        // Showing the note is best-effort; an exception here would end the app.
                    }
                }
            }
            return IntPtr.Zero;
        }
    }
}
