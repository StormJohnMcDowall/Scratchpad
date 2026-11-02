using System;
using Scratchpad.Core;
using Xunit;

class FakePersonalizeRegistry : IPersonalizeRegistry
{
    public object Value;
    public bool Throws;

    public object GetAppsUseLightTheme()
    {
        if (Throws)
        {
            throw new UnauthorizedAccessException();
        }
        return Value;
    }
}

public class ThemeTests
{
    [Fact]
    public void WindowsAppsUseLightTheme_FalseWhenValueIsZero()
    {
        Assert.False(ThemeResolver.WindowsAppsUseLightTheme(new FakePersonalizeRegistry { Value = 0 }));
    }

    [Fact]
    public void WindowsAppsUseLightTheme_TrueWhenValueIsOne()
    {
        Assert.True(ThemeResolver.WindowsAppsUseLightTheme(new FakePersonalizeRegistry { Value = 1 }));
    }

    [Fact]
    public void WindowsAppsUseLightTheme_TrueWhenValueMissing()
    {
        Assert.True(ThemeResolver.WindowsAppsUseLightTheme(new FakePersonalizeRegistry { Value = null }));
    }

    [Fact]
    public void WindowsAppsUseLightTheme_TrueWhenValueIsNotAnInt()
    {
        Assert.True(ThemeResolver.WindowsAppsUseLightTheme(new FakePersonalizeRegistry { Value = "0" }));
    }

    [Fact]
    public void WindowsAppsUseLightTheme_TrueWhenRegistryThrows()
    {
        Assert.True(ThemeResolver.WindowsAppsUseLightTheme(new FakePersonalizeRegistry { Throws = true }));
    }

    [Theory]
    [InlineData(ThemePreference.FollowWindows, true, AppTheme.Light)]
    [InlineData(ThemePreference.FollowWindows, false, AppTheme.Dark)]
    [InlineData(ThemePreference.Light, false, AppTheme.Light)]
    [InlineData(ThemePreference.Light, true, AppTheme.Light)]
    [InlineData(ThemePreference.Dark, true, AppTheme.Dark)]
    [InlineData(ThemePreference.Dark, false, AppTheme.Dark)]
    public void Resolve_CombinesPreferenceWithWindowsSetting(ThemePreference preference, bool windowsAppsUseLightTheme, AppTheme expected)
    {
        Assert.Equal(expected, ThemeResolver.Resolve(preference, windowsAppsUseLightTheme));
    }
}
