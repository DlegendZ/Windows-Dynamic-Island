using System;

namespace DynamicIsland.Core;

public sealed record PeekEvent(string IconKey, string Text, TimeSpan Duration);
