using System;
using System.IO;
using DynamicIsland.Core;

namespace DynamicIsland;

public sealed class WshShortcutWriter : IShortcutWriter
{
    public void CreateShortcut(string shortcutPath, string targetPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM component is not available on this system.");

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Save();
    }

    public void DeleteShortcut(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);
    }
}
