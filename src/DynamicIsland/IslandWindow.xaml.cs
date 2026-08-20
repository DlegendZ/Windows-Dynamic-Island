using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Interop;

namespace DynamicIsland;

public partial class IslandWindow : Window
{
    private readonly IslandStateMachine _stateMachine = new();
    private readonly PeekEventQueue _peekQueue = new();
    private readonly DispatcherTimer _peekTimer = new();
    private IntPtr _hwnd;

    public ObservableCollection<IIslandModule> Modules { get; } = new();

    public IslandWindow()
    {
        InitializeComponent();
        DataContext = this;

        _stateMachine.StateChanged += OnStateChanged;
        _peekTimer.Tick += (_, _) =>
        {
            _peekTimer.Stop();
            _stateMachine.Fire(IslandTrigger.HoverLeaveOrTimeout);
        };

        SourceInitialized += IslandWindow_SourceInitialized;
        Loaded += IslandWindow_Loaded;
    }

    public void AddModule(IIslandModule module)
    {
        Modules.Add(module);
        if (module is IPeekEventSource peekSource)
            peekSource.PeekRequested += EnqueuePeekEvent;
    }

    public void EnqueuePeekEvent(PeekEvent peekEvent)
    {
        Dispatcher.Invoke(() =>
        {
            _peekQueue.Enqueue(peekEvent);
            _stateMachine.Fire(IslandTrigger.PeekEventRequested);
        });
    }

    private void IslandWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.ApplyToolWindowStyle(_hwnd);
        NativeMethods.SetClickThrough(_hwnd, true);

        var source = HwndSource.FromHwnd(_hwnd);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_DISPLAYCHANGE = 0x007E;
        if (msg == WM_DISPLAYCHANGE)
            PositionTopCenter();

        return IntPtr.Zero;
    }

    private void IslandWindow_Loaded(object? sender, RoutedEventArgs e) => PositionTopCenter();

    private void PositionTopCenter()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top;
    }

    private void PillBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _peekTimer.Stop();
        _stateMachine.Fire(IslandTrigger.HoverEnter);
    }

    private void PillBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _peekTimer.Interval = TimeSpan.FromSeconds(1.5);
        _peekTimer.Start();
    }

    private void PillBorder_Click(object sender, MouseButtonEventArgs e) =>
        _stateMachine.Fire(IslandTrigger.Click);

    private void PillBorder_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space)
            _stateMachine.Fire(IslandTrigger.Click);
    }

    private void ExpandedPanel_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            _stateMachine.Fire(IslandTrigger.EscapeOrClickOutside);
    }

    private void ModuleTabStrip_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModuleTabStrip.SelectedItem is IIslandModule module)
            ModuleContentHost.Content = module.CreateContent();
    }

    private void OnStateChanged(IslandState previous, IslandState current)
    {
        NativeMethods.SetClickThrough(_hwnd, current == IslandState.Idle);

        switch (current)
        {
            case IslandState.Idle:
                AnimateTo(PillBorder, 36, 10);
                ExpandedPanel.Visibility = Visibility.Collapsed;
                PeekContent.Visibility = Visibility.Collapsed;
                break;

            case IslandState.Peek:
                if (_peekQueue.TryDequeue(out var peekEvent) && peekEvent is not null)
                    PeekText.Text = peekEvent.Text;
                else
                    PeekText.Text = string.Empty;
                PeekContent.Visibility = Visibility.Visible;
                ExpandedPanel.Visibility = Visibility.Collapsed;
                AnimateTo(PillBorder, 160, 36);
                _peekTimer.Interval = TimeSpan.FromSeconds(3);
                _peekTimer.Start();
                break;

            case IslandState.Expanded:
                _peekTimer.Stop();
                PeekContent.Visibility = Visibility.Collapsed;
                ExpandedPanel.Visibility = Visibility.Visible;
                ExpandedPanel.Focus();
                break;
        }
    }

    private static void AnimateTo(FrameworkElement element, double width, double height)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(250));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        element.BeginAnimation(WidthProperty, new DoubleAnimation(width, duration) { EasingFunction = ease });
        element.BeginAnimation(HeightProperty, new DoubleAnimation(height, duration) { EasingFunction = ease });
    }
}
