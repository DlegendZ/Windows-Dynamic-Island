using System;

namespace DynamicIsland.Core;

public sealed class IslandStateMachine
{
    public IslandState Current { get; private set; } = IslandState.Idle;

    public event Action<IslandState, IslandState>? StateChanged;

    public bool Fire(IslandTrigger trigger)
    {
        var next = GetNext(Current, trigger);
        if (next is null || next == Current)
            return false;

        var previous = Current;
        Current = next.Value;
        StateChanged?.Invoke(previous, Current);
        return true;
    }

    private static IslandState? GetNext(IslandState current, IslandTrigger trigger) => (current, trigger) switch
    {
        (IslandState.Idle, IslandTrigger.HoverEnter) => IslandState.Peek,
        (IslandState.Idle, IslandTrigger.PeekEventRequested) => IslandState.Peek,
        (IslandState.Peek, IslandTrigger.HoverLeaveOrTimeout) => IslandState.Idle,
        (IslandState.Peek, IslandTrigger.Click) => IslandState.Expanded,
        (IslandState.Expanded, IslandTrigger.EscapeOrClickOutside) => IslandState.Idle,
        _ => null
    };
}
