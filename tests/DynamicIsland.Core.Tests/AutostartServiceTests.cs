using System;
using System.IO;
using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class AutostartServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeShortcutWriter _writer = new();

    public AutostartServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DynamicIslandAutostartTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void IsEnabled_WhenNoShortcut_ReturnsFalse()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Enable_CreatesShortcutAndReflectsInIsEnabled()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        service.Enable();
        Assert.True(service.IsEnabled());
        Assert.Equal(1, _writer.CreateCalls);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_DoesNotCallWriterAgain()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        service.Enable();
        service.Enable();
        Assert.Equal(1, _writer.CreateCalls);
    }

    [Fact]
    public void Disable_RemovesShortcut()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        service.Enable();
        service.Disable();
        Assert.False(service.IsEnabled());
        Assert.Equal(1, _writer.DeleteCalls);
    }

    private sealed class FakeShortcutWriter : IShortcutWriter
    {
        public int CreateCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public void CreateShortcut(string shortcutPath, string targetPath)
        {
            CreateCalls++;
            File.WriteAllText(shortcutPath, targetPath);
        }

        public void DeleteShortcut(string shortcutPath)
        {
            DeleteCalls++;
            File.Delete(shortcutPath);
        }
    }
}
