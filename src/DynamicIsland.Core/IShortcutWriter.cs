namespace DynamicIsland.Core;

public interface IShortcutWriter
{
    void CreateShortcut(string shortcutPath, string targetPath);
    void DeleteShortcut(string shortcutPath);
}
