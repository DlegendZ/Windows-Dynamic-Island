using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class IslandStateMachineTests
{
    [Fact]
    public void StartsInIdle()
    {
        var sm = new IslandStateMachine();
        Assert.Equal(IslandState.Idle, sm.Current);
    }

    [Fact]
    public void HoverEnterFromIdle_MovesToPeek()
    {
        var sm = new IslandStateMachine();
        var result = sm.Fire(IslandTrigger.HoverEnter);
        Assert.True(result);
        Assert.Equal(IslandState.Peek, sm.Current);
    }

    [Fact]
    public void ClickFromIdle_IsIgnored()
    {
        var sm = new IslandStateMachine();
        var result = sm.Fire(IslandTrigger.Click);
        Assert.False(result);
        Assert.Equal(IslandState.Idle, sm.Current);
    }

    [Fact]
    public void ClickFromPeek_MovesToExpanded()
    {
        var sm = new IslandStateMachine();
        sm.Fire(IslandTrigger.HoverEnter);
        var result = sm.Fire(IslandTrigger.Click);
        Assert.True(result);
        Assert.Equal(IslandState.Expanded, sm.Current);
    }

    [Fact]
    public void HoverLeaveFromPeek_MovesToIdle()
    {
        var sm = new IslandStateMachine();
        sm.Fire(IslandTrigger.HoverEnter);
        var result = sm.Fire(IslandTrigger.HoverLeaveOrTimeout);
        Assert.True(result);
        Assert.Equal(IslandState.Idle, sm.Current);
    }

    [Fact]
    public void EscapeFromExpanded_MovesToIdle()
    {
        var sm = new IslandStateMachine();
        sm.Fire(IslandTrigger.HoverEnter);
        sm.Fire(IslandTrigger.Click);
        var result = sm.Fire(IslandTrigger.EscapeOrClickOutside);
        Assert.True(result);
        Assert.Equal(IslandState.Idle, sm.Current);
    }

    [Fact]
    public void PeekEventFromExpanded_DoesNotInterrupt()
    {
        var sm = new IslandStateMachine();
        sm.Fire(IslandTrigger.HoverEnter);
        sm.Fire(IslandTrigger.Click);
        var result = sm.Fire(IslandTrigger.PeekEventRequested);
        Assert.False(result);
        Assert.Equal(IslandState.Expanded, sm.Current);
    }

    [Fact]
    public void PeekEventFromIdle_MovesToPeek()
    {
        var sm = new IslandStateMachine();
        var result = sm.Fire(IslandTrigger.PeekEventRequested);
        Assert.True(result);
        Assert.Equal(IslandState.Peek, sm.Current);
    }

    [Fact]
    public void StateChanged_FiresWithPreviousAndCurrent()
    {
        var sm = new IslandStateMachine();
        IslandState? seenPrevious = null;
        IslandState? seenCurrent = null;
        sm.StateChanged += (prev, curr) => { seenPrevious = prev; seenCurrent = curr; };

        sm.Fire(IslandTrigger.HoverEnter);

        Assert.Equal(IslandState.Idle, seenPrevious);
        Assert.Equal(IslandState.Peek, seenCurrent);
    }
}
