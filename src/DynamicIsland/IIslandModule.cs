namespace DynamicIsland;

public interface IIslandModule
{
    string Id { get; }
    string Header { get; }
    string IconKey { get; }
    System.Windows.Controls.UserControl CreateContent();
}
