using System;

namespace Scratchpad.Core
{
    public struct StartupWindowSettings : IEquatable<StartupWindowSettings>
    {
        public bool ShowWindowOnStartup { get; set; }

        public bool Equals(StartupWindowSettings other)
        {
            return ShowWindowOnStartup == other.ShowWindowOnStartup;
        }

        public override bool Equals(object obj)
        {
            return obj is StartupWindowSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ShowWindowOnStartup.GetHashCode();
        }

        public static bool operator ==(StartupWindowSettings a, StartupWindowSettings b) => a.Equals(b);
        public static bool operator !=(StartupWindowSettings a, StartupWindowSettings b) => !a.Equals(b);
    }
}
