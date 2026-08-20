using System;

namespace DynamicIsland.Core;

public interface IPeekEventSource
{
    event Action<PeekEvent> PeekRequested;
}
