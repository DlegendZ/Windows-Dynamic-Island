using System;
using System.IO;

namespace DynamicIsland.Core;

public sealed class AutostartService
{
    private const string ShortcutName = "DynamicIsland.lnk";

    private readonly IShortcutWriter _writer;
    private readonly string _startupFolder;
    private readonly string _exePath;

    public AutostartService(IShortcutWriter writer, string startupFolder, string exePath)
    {
        _writer = writer;
        _startupFolder = startupFolder;
        _exePath = exePath;
    }

    public static string DefaultStartupFolder() =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private string ShortcutPath => Path.Combine(_startupFolder, ShortcutName);

    public bool IsEnabled() => File.Exists(ShortcutPath);

    public void Enable()
    {
        if (!IsEnabled())
            _writer.CreateShortcut(ShortcutPath, _exePath);
    }

    public void Disable()
    {
        if (IsEnabled())
            _writer.DeleteShortcut(ShortcutPath);
    }
}
