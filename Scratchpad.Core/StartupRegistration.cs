using Microsoft.Win32;

namespace Scratchpad.Core
{
    public interface IRunKeyRegistry
    {
        void SetValue(string name, string value);
        string GetValue(string name);
        void DeleteValue(string name);
    }

    public class RegistryRunKey : IRunKeyRegistry
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public void SetValue(string name, string value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true))
            {
                key.SetValue(name, value);
            }
        }

        public string GetValue(string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key?.GetValue(name) as string;
            }
        }

        public void DeleteValue(string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
            {
                key?.DeleteValue(name, false);
            }
        }
    }

    public class StartupRegistration
    {
        public const string ValueName = "Scratchpad";

        private readonly IRunKeyRegistry _registry;
        private readonly string _executablePath;

        public StartupRegistration(IRunKeyRegistry registry, string executablePath)
        {
            _registry = registry;
            _executablePath = executablePath;
        }

        private string QuotedPath => "\"" + _executablePath + "\"";

        public void Enable()
        {
            _registry.SetValue(ValueName, QuotedPath);
        }

        public void Disable()
        {
            _registry.DeleteValue(ValueName);
        }

        public bool IsEnabledForCurrentPath()
        {
            return _registry.GetValue(ValueName) == QuotedPath;
        }
    }
}
