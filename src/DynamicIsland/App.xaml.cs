using System.Diagnostics;
using System.Windows;
using DynamicIsland.Core;

namespace DynamicIsland;

public partial class App : System.Windows.Application
{
    private IslandWindow? _islandWindow;
    private TrayIconService? _trayIconService;
    private AutostartService? _autostartService;
    private SettingsService? _settingsService;
    private IslandSettings? _settings;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _settingsService = new SettingsService(SettingsService.DefaultFilePath());
        _settings = _settingsService.Load();

        var exePath = Process.GetCurrentProcess().MainModule!.FileName;
        _autostartService = new AutostartService(new WshShortcutWriter(), AutostartService.DefaultStartupFolder(), exePath);

        _islandWindow = new IslandWindow();
        _islandWindow.Show();

        _trayIconService = new TrayIconService(_autostartService.IsEnabled());
        _trayIconService.ShowRequested += () => _islandWindow.Activate();
        _trayIconService.QuitRequested += () => Shutdown();
        _trayIconService.AutostartToggled += OnAutostartToggled;
    }

    private void OnAutostartToggled(bool enabled)
    {
        if (enabled)
            _autostartService!.Enable();
        else
            _autostartService!.Disable();

        _settings!.LaunchAtStartup = enabled;
        _settingsService!.Save(_settings);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIconService?.Dispose();
        base.OnExit(e);
    }
}
