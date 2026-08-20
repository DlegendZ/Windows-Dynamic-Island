using System;
using System.IO;
using System.Text.Json;

namespace DynamicIsland.Core;

public sealed class SettingsService
{
    private readonly string _filePath;

    public SettingsService(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DynamicIsland",
            "settings.json");

    public IslandSettings Load()
    {
        if (!File.Exists(_filePath))
            return new IslandSettings();

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<IslandSettings>(json) ?? new IslandSettings();
    }

    public void Save(IslandSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
