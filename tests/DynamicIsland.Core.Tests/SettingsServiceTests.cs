using System;
using System.IO;
using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DynamicIslandTests_" + Guid.NewGuid());
        _filePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var service = new SettingsService(_filePath);
        var settings = service.Load();
        Assert.False(settings.LaunchAtStartup);
        Assert.Null(settings.HotkeyBinding);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var service = new SettingsService(_filePath);
        var settings = new IslandSettings { LaunchAtStartup = true, HotkeyBinding = "Ctrl+Alt+D" };

        service.Save(settings);
        var loaded = service.Load();

        Assert.True(loaded.LaunchAtStartup);
        Assert.Equal("Ctrl+Alt+D", loaded.HotkeyBinding);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        Assert.False(Directory.Exists(_tempDir));
        var service = new SettingsService(_filePath);
        service.Save(new IslandSettings());
        Assert.True(File.Exists(_filePath));
    }
}
