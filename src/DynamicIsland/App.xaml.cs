using System.Windows;

namespace DynamicIsland;

public partial class App : System.Windows.Application
{
    private IslandWindow? _islandWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _islandWindow = new IslandWindow();
        _islandWindow.Show();
    }
}
