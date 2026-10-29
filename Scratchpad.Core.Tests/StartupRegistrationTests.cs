using System.Collections.Generic;
using Scratchpad.Core;
using Xunit;

class FakeRunKeyRegistry : IRunKeyRegistry
{
    public Dictionary<string, string> Values = new Dictionary<string, string>();
    public void SetValue(string name, string value) => Values[name] = value;
    public string GetValue(string name) => Values.TryGetValue(name, out var v) ? v : null;
    public void DeleteValue(string name) => Values.Remove(name);
}

public class StartupRegistrationTests
{
    [Fact]
    public void Enable_SetsValueToQuotedExecutablePath()
    {
        var registry = new FakeRunKeyRegistry();
        new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe").Enable();

        Assert.Equal("\"C:\\Apps\\Scratchpad.exe\"", registry.Values[StartupRegistration.ValueName]);
    }

    [Fact]
    public void IsEnabledForCurrentPath_FalseWhenStoredPathDiffers()
    {
        var registry = new FakeRunKeyRegistry();
        registry.Values[StartupRegistration.ValueName] = "\"C:\\Old\\Scratchpad.exe\"";

        var result = new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe").IsEnabledForCurrentPath();

        Assert.False(result);
    }

    [Fact]
    public void IsEnabledForCurrentPath_TrueAfterEnable()
    {
        var registry = new FakeRunKeyRegistry();
        var registration = new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe");

        registration.Enable();

        Assert.True(registration.IsEnabledForCurrentPath());
    }

    [Fact]
    public void Disable_RemovesValue()
    {
        var registry = new FakeRunKeyRegistry();
        var registration = new StartupRegistration(registry, @"C:\Apps\Scratchpad.exe");
        registration.Enable();

        registration.Disable();

        Assert.Null(registry.GetValue(StartupRegistration.ValueName));
    }
}
