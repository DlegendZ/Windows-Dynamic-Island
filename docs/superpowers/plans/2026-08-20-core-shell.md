# Core Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Dynamic Island core shell — a borderless, transparent, always-on-top WPF pill window with Idle/Peek/Expanded states, an auto-peek event pipeline, a module-hosting contract, keyboard/screen-reader support, tray lifecycle, and optional autostart — per `docs/superpowers/specs/2026-08-20-core-shell-design.md`.

**Architecture:** Two projects. `DynamicIsland.Core` (net8.0, no WPF/Windows dependency) holds pure logic — state machine, peek event queue, settings persistence, autostart logic — all unit-testable with xunit. `DynamicIsland` (net8.0-windows, WPF) holds the window, Win32 interop, tray icon, and COM-based shortcut writer, referencing Core. No feature modules (media, stats, etc.) are built here — only the `IIslandModule`/`IPeekEventSource` contracts they'll implement later.

**Tech Stack:** C#, .NET 8, WPF, Win32 P/Invoke (window styles, hotkeys), `System.Windows.Forms.NotifyIcon` (tray), IWshRuntimeLibrary COM (Startup-folder shortcut), xunit (tests).

---

## Task 1: Solution and project scaffolding

**Files:**
- Create: `DynamicIsland.sln`
- Create: `src/DynamicIsland.Core/DynamicIsland.Core.csproj`
- Create: `src/DynamicIsland/DynamicIsland.csproj`
- Create: `src/DynamicIsland/App.xaml`
- Create: `src/DynamicIsland/App.xaml.cs`
- Create: `tests/DynamicIsland.Core.Tests/DynamicIsland.Core.Tests.csproj`

- [ ] **Step 1: Create directory layout and Core project**

```bash
mkdir -p src/DynamicIsland.Core src/DynamicIsland tests/DynamicIsland.Core.Tests
```

`src/DynamicIsland.Core/DynamicIsland.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create WPF app project**

`src/DynamicIsland/DynamicIsland.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\DynamicIsland.Core\DynamicIsland.Core.csproj" />
  </ItemGroup>
  <ItemGroup>
    <COMReference Include="IWshRuntimeLibrary">
      <Guid>{F935DC20-1CF0-11D0-ADB9-00C04FD58A0B}</Guid>
      <VersionMajor>1</VersionMajor>
      <VersionMinor>0</VersionMinor>
      <Lcid>0</Lcid>
      <WrapperTool>tlbimp</WrapperTool>
      <Isolated>false</Isolated>
      <EmbedInteropTypes>true</EmbedInteropTypes>
    </COMReference>
  </ItemGroup>
</Project>
```

`src/DynamicIsland/App.xaml`:

```xml
<Application x:Class="DynamicIsland.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="">
</Application>
```

`src/DynamicIsland/App.xaml.cs`:

```csharp
using System.Windows;

namespace DynamicIsland;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
```

- [ ] **Step 3: Create test project**

`tests/DynamicIsland.Core.Tests/DynamicIsland.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\DynamicIsland.Core\DynamicIsland.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create solution file and add all three projects**

```bash
dotnet new sln -n DynamicIsland
dotnet sln add src/DynamicIsland.Core/DynamicIsland.Core.csproj
dotnet sln add src/DynamicIsland/DynamicIsland.csproj
dotnet sln add tests/DynamicIsland.Core.Tests/DynamicIsland.Core.Tests.csproj
```

- [ ] **Step 5: Verify solution builds**

```bash
dotnet build
```

Expected: build succeeds (0 errors) for all three projects.

- [ ] **Step 6: Commit**

```bash
git add DynamicIsland.sln src/ tests/
git commit -m "chore: scaffold solution with Core, app, and test projects"
```

---

## Task 2: Island state machine (TDD)

**Files:**
- Create: `src/DynamicIsland.Core/IslandState.cs`
- Create: `src/DynamicIsland.Core/IslandTrigger.cs`
- Create: `src/DynamicIsland.Core/IslandStateMachine.cs`
- Test: `tests/DynamicIsland.Core.Tests/IslandStateMachineTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/DynamicIsland.Core.Tests/IslandStateMachineTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: FAIL with compile errors (`IslandState`, `IslandTrigger`, `IslandStateMachine` not found).

- [ ] **Step 3: Write the implementation**

`src/DynamicIsland.Core/IslandState.cs`:

```csharp
namespace DynamicIsland.Core;

public enum IslandState
{
    Idle,
    Peek,
    Expanded
}
```

`src/DynamicIsland.Core/IslandTrigger.cs`:

```csharp
namespace DynamicIsland.Core;

public enum IslandTrigger
{
    HoverEnter,
    HoverLeaveOrTimeout,
    Click,
    EscapeOrClickOutside,
    PeekEventRequested
}
```

`src/DynamicIsland.Core/IslandStateMachine.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: PASS, 9 tests.

- [ ] **Step 5: Commit**

```bash
git add src/DynamicIsland.Core/IslandState.cs src/DynamicIsland.Core/IslandTrigger.cs src/DynamicIsland.Core/IslandStateMachine.cs tests/DynamicIsland.Core.Tests/IslandStateMachineTests.cs
git commit -m "feat: add island state machine (Idle/Peek/Expanded)"
```

---

## Task 3: Peek event queue (TDD)

**Files:**
- Create: `src/DynamicIsland.Core/PeekEvent.cs`
- Create: `src/DynamicIsland.Core/PeekEventQueue.cs`
- Test: `tests/DynamicIsland.Core.Tests/PeekEventQueueTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/DynamicIsland.Core.Tests/PeekEventQueueTests.cs`:

```csharp
using System;
using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class PeekEventQueueTests
{
    [Fact]
    public void EmptyQueue_TryDequeueReturnsFalse()
    {
        var queue = new PeekEventQueue();
        var result = queue.TryDequeue(out var evt);
        Assert.False(result);
        Assert.Null(evt);
    }

    [Fact]
    public void EnqueueThenDequeue_ReturnsInFifoOrder()
    {
        var queue = new PeekEventQueue();
        var first = new PeekEvent("icon-a", "First", TimeSpan.FromSeconds(2));
        var second = new PeekEvent("icon-b", "Second", TimeSpan.FromSeconds(2));

        queue.Enqueue(first);
        queue.Enqueue(second);

        queue.TryDequeue(out var dequeuedFirst);
        queue.TryDequeue(out var dequeuedSecond);

        Assert.Equal(first, dequeuedFirst);
        Assert.Equal(second, dequeuedSecond);
    }

    [Fact]
    public void Count_ReflectsQueuedItems()
    {
        var queue = new PeekEventQueue();
        queue.Enqueue(new PeekEvent("icon", "Text", TimeSpan.FromSeconds(1)));
        Assert.Equal(1, queue.Count);

        queue.TryDequeue(out _);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void Clear_EmptiesQueue()
    {
        var queue = new PeekEventQueue();
        queue.Enqueue(new PeekEvent("icon", "Text", TimeSpan.FromSeconds(1)));
        queue.Clear();
        Assert.Equal(0, queue.Count);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: FAIL with compile errors (`PeekEvent`, `PeekEventQueue` not found).

- [ ] **Step 3: Write the implementation**

`src/DynamicIsland.Core/PeekEvent.cs`:

```csharp
using System;

namespace DynamicIsland.Core;

public sealed record PeekEvent(string IconKey, string Text, TimeSpan Duration);
```

`src/DynamicIsland.Core/PeekEventQueue.cs`:

```csharp
using System.Collections.Generic;

namespace DynamicIsland.Core;

public sealed class PeekEventQueue
{
    private readonly Queue<PeekEvent> _queue = new();

    public int Count => _queue.Count;

    public void Enqueue(PeekEvent peekEvent) => _queue.Enqueue(peekEvent);

    public bool TryDequeue(out PeekEvent? peekEvent) => _queue.TryDequeue(out peekEvent);

    public void Clear() => _queue.Clear();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/DynamicIsland.Core/PeekEvent.cs src/DynamicIsland.Core/PeekEventQueue.cs tests/DynamicIsland.Core.Tests/PeekEventQueueTests.cs
git commit -m "feat: add peek event and FIFO peek event queue"
```

---

## Task 4: Peek event source contract

**Files:**
- Create: `src/DynamicIsland.Core/IPeekEventSource.cs`

- [ ] **Step 1: Write the interface**

`src/DynamicIsland.Core/IPeekEventSource.cs`:

```csharp
using System;

namespace DynamicIsland.Core;

public interface IPeekEventSource
{
    event Action<PeekEvent> PeekRequested;
}
```

- [ ] **Step 2: Verify solution still builds**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/DynamicIsland.Core/IPeekEventSource.cs
git commit -m "feat: add IPeekEventSource contract for future modules"
```

---

## Task 5: Settings persistence (TDD)

**Files:**
- Create: `src/DynamicIsland.Core/IslandSettings.cs`
- Create: `src/DynamicIsland.Core/SettingsService.cs`
- Test: `tests/DynamicIsland.Core.Tests/SettingsServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/DynamicIsland.Core.Tests/SettingsServiceTests.cs`:

```csharp
using System;
using System.IO;
using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DynamicIslandTests_" + Guid.NewGuid());
        _filePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var service = new SettingsService(_filePath);
        var settings = service.Load();
        Assert.False(settings.LaunchAtStartup);
        Assert.Null(settings.HotkeyBinding);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var service = new SettingsService(_filePath);
        var settings = new IslandSettings { LaunchAtStartup = true, HotkeyBinding = "Ctrl+Alt+D" };

        service.Save(settings);
        var loaded = service.Load();

        Assert.True(loaded.LaunchAtStartup);
        Assert.Equal("Ctrl+Alt+D", loaded.HotkeyBinding);
    }

    [Fact]
    public void Save_CreatesDirectoryIfMissing()
    {
        Assert.False(Directory.Exists(_tempDir));
        var service = new SettingsService(_filePath);
        service.Save(new IslandSettings());
        Assert.True(File.Exists(_filePath));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: FAIL with compile errors (`IslandSettings`, `SettingsService` not found).

- [ ] **Step 3: Write the implementation**

`src/DynamicIsland.Core/IslandSettings.cs`:

```csharp
namespace DynamicIsland.Core;

public sealed class IslandSettings
{
    public bool LaunchAtStartup { get; set; } = false;
    public string? HotkeyBinding { get; set; } = null;
}
```

`src/DynamicIsland.Core/SettingsService.cs`:

```csharp
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
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: PASS, 3 tests.

- [ ] **Step 5: Commit**

```bash
git add src/DynamicIsland.Core/IslandSettings.cs src/DynamicIsland.Core/SettingsService.cs tests/DynamicIsland.Core.Tests/SettingsServiceTests.cs
git commit -m "feat: add JSON settings persistence"
```

---

## Task 6: Autostart logic (TDD)

**Files:**
- Create: `src/DynamicIsland.Core/IShortcutWriter.cs`
- Create: `src/DynamicIsland.Core/AutostartService.cs`
- Test: `tests/DynamicIsland.Core.Tests/AutostartServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/DynamicIsland.Core.Tests/AutostartServiceTests.cs`:

```csharp
using System;
using System.IO;
using DynamicIsland.Core;
using Xunit;

namespace DynamicIsland.Core.Tests;

public class AutostartServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FakeShortcutWriter _writer = new();

    public AutostartServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DynamicIslandAutostartTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void IsEnabled_WhenNoShortcut_ReturnsFalse()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Enable_CreatesShortcutAndReflectsInIsEnabled()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        service.Enable();
        Assert.True(service.IsEnabled());
        Assert.Equal(1, _writer.CreateCalls);
    }

    [Fact]
    public void Enable_WhenAlreadyEnabled_DoesNotCallWriterAgain()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        service.Enable();
        service.Enable();
        Assert.Equal(1, _writer.CreateCalls);
    }

    [Fact]
    public void Disable_RemovesShortcut()
    {
        var service = new AutostartService(_writer, _tempDir, @"C:\fake\DynamicIsland.exe");
        service.Enable();
        service.Disable();
        Assert.False(service.IsEnabled());
        Assert.Equal(1, _writer.DeleteCalls);
    }

    private sealed class FakeShortcutWriter : IShortcutWriter
    {
        public int CreateCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public void CreateShortcut(string shortcutPath, string targetPath)
        {
            CreateCalls++;
            File.WriteAllText(shortcutPath, targetPath);
        }

        public void DeleteShortcut(string shortcutPath)
        {
            DeleteCalls++;
            File.Delete(shortcutPath);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: FAIL with compile errors (`IShortcutWriter`, `AutostartService` not found).

- [ ] **Step 3: Write the implementation**

`src/DynamicIsland.Core/IShortcutWriter.cs`:

```csharp
namespace DynamicIsland.Core;

public interface IShortcutWriter
{
    void CreateShortcut(string shortcutPath, string targetPath);
    void DeleteShortcut(string shortcutPath);
}
```

`src/DynamicIsland.Core/AutostartService.cs`:

```csharp
using System;
using System.IO;

namespace DynamicIsland.Core;

public sealed class AutostartService
{
    private const string ShortcutName = "DynamicIsland.lnk";

    private readonly IShortcutWriter _writer;
    private readonly string _startupFolder;
    private readonly string _exePath;

    public AutostartService(IShortcutWriter writer, string startupFolder, string exePath)
    {
        _writer = writer;
        _startupFolder = startupFolder;
        _exePath = exePath;
    }

    public static string DefaultStartupFolder() =>
        Environment.GetFolderPath(Environment.SpecialFolder.Startup);

    private string ShortcutPath => Path.Combine(_startupFolder, ShortcutName);

    public bool IsEnabled() => File.Exists(ShortcutPath);

    public void Enable()
    {
        if (!IsEnabled())
            _writer.CreateShortcut(ShortcutPath, _exePath);
    }

    public void Disable()
    {
        if (IsEnabled())
            _writer.DeleteShortcut(ShortcutPath);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/DynamicIsland.Core.Tests
```

Expected: PASS, 4 tests.

- [ ] **Step 5: Commit**

```bash
git add src/DynamicIsland.Core/IShortcutWriter.cs src/DynamicIsland.Core/AutostartService.cs tests/DynamicIsland.Core.Tests/AutostartServiceTests.cs
git commit -m "feat: add autostart service with injectable shortcut writer"
```

---

## Task 7: Win32 window-style interop + click-through spike

**Files:**
- Create: `src/DynamicIsland/Interop/NativeMethods.cs`
- Create: `src/DynamicIsland/SpikeWindow.xaml` (temporary, deleted at end of task)
- Create: `src/DynamicIsland/SpikeWindow.xaml.cs` (temporary, deleted at end of task)

This task de-risks spec §14 risk 1 (`WS_EX_TRANSPARENT` click-through quirks) before the real window is built.

- [ ] **Step 1: Write the interop layer**

`src/DynamicIsland/Interop/NativeMethods.cs`:

```csharp
using System;
using System.Runtime.InteropServices;

namespace DynamicIsland.Interop;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public static void ApplyToolWindowStyle(IntPtr hwnd)
    {
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_LAYERED);
    }

    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, clickThrough ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT);
    }
}
```

- [ ] **Step 2: Build a throwaway spike window to prove click-through works**

`src/DynamicIsland/SpikeWindow.xaml`:

```xml
<Window x:Class="DynamicIsland.SpikeWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Spike" WindowStyle="None" AllowsTransparency="True"
        Background="Transparent" Topmost="True" ShowInTaskbar="False"
        Width="400" Height="200" Left="100" Top="100">
    <Grid>
        <Border Background="#801C1C1E" Width="100" Height="40"
                HorizontalAlignment="Center" VerticalAlignment="Top"
                MouseLeftButtonUp="Border_Click"/>
    </Grid>
</Window>
```

`src/DynamicIsland/SpikeWindow.xaml.cs`:

```csharp
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DynamicIsland.Interop;

namespace DynamicIsland;

public partial class SpikeWindow : Window
{
    public SpikeWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.ApplyToolWindowStyle(hwnd);
            NativeMethods.SetClickThrough(hwnd, true);
        };
    }

    private void Border_Click(object sender, MouseButtonEventArgs e) =>
        MessageBox.Show("Click landed on the pill");
}
```

Temporarily point `App.xaml` `StartupUri` at `SpikeWindow.xaml` (or launch it from `OnStartup`) to run it.

- [ ] **Step 3: Manually verify click-through**

```bash
dotnet run --project src/DynamicIsland
```

Manual check: with the spike window transparent-clickthrough applied, clicking on the transparent (non-border) area should pass through to whatever's underneath (e.g. desktop icons respond), while clicking the visible border shows the message box. If click-through does not work as expected, note the deviation in this task before proceeding — do not silently change approach.

Expected: transparent areas are click-through, the border area is not.

- [ ] **Step 4: Delete the spike, keep the interop layer**

```bash
rm src/DynamicIsland/SpikeWindow.xaml src/DynamicIsland/SpikeWindow.xaml.cs
```

Revert any temporary `App.xaml`/`App.xaml.cs` changes made to launch the spike.

- [ ] **Step 5: Verify build still succeeds**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/DynamicIsland/Interop/NativeMethods.cs
git commit -m "feat: add Win32 window-style interop (spiked click-through manually)"
```

---

## Task 8: Module hosting contract

**Files:**
- Create: `src/DynamicIsland/IIslandModule.cs`

- [ ] **Step 1: Write the interface**

`src/DynamicIsland/IIslandModule.cs`:

```csharp
using System.Windows.Controls;

namespace DynamicIsland;

public interface IIslandModule
{
    string Id { get; }
    string Header { get; }
    string IconKey { get; }
    UserControl CreateContent();
}
```

- [ ] **Step 2: Verify build succeeds**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/DynamicIsland/IIslandModule.cs
git commit -m "feat: add IIslandModule contract for future feature modules"
```

---

## Task 9: Island window — states, animation, positioning

**Files:**
- Create: `src/DynamicIsland/IslandWindow.xaml`
- Create: `src/DynamicIsland/IslandWindow.xaml.cs`

- [ ] **Step 1: Write the window XAML**

`src/DynamicIsland/IslandWindow.xaml`:

```xml
<Window x:Class="DynamicIsland.IslandWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Dynamic Island"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        Width="900" Height="220"
        Left="0" Top="0">
    <Grid>
        <Border x:Name="PillBorder"
                Background="#F01C1C1E"
                CornerRadius="20"
                Width="36" Height="10"
                HorizontalAlignment="Center"
                VerticalAlignment="Top"
                Margin="0,8,0,0"
                Focusable="True"
                MouseEnter="PillBorder_MouseEnter"
                MouseLeave="PillBorder_MouseLeave"
                MouseLeftButtonUp="PillBorder_Click"
                KeyDown="PillBorder_KeyDown"
                AutomationProperties.Name="Dynamic Island">
            <Grid x:Name="PeekContent" Visibility="Collapsed">
                <TextBlock x:Name="PeekText"
                           Foreground="White"
                           VerticalAlignment="Center"
                           HorizontalAlignment="Center"
                           FontSize="12"/>
            </Grid>
        </Border>

        <Border x:Name="ExpandedPanel"
                Background="#F01C1C1E"
                CornerRadius="24"
                Width="500" Height="180"
                HorizontalAlignment="Center"
                VerticalAlignment="Top"
                Margin="0,8,0,0"
                Visibility="Collapsed"
                KeyDown="ExpandedPanel_KeyDown"
                AutomationProperties.Name="Dynamic Island expanded panel">
            <DockPanel>
                <ListBox x:Name="ModuleTabStrip"
                         DockPanel.Dock="Top"
                         Height="40"
                         Background="Transparent"
                         BorderThickness="0"
                         SelectionChanged="ModuleTabStrip_SelectionChanged"
                         KeyboardNavigation.DirectionalNavigation="Cycle"
                         AutomationProperties.Name="Module tabs">
                    <ListBox.ItemsPanel>
                        <ItemsPanelTemplate>
                            <StackPanel Orientation="Horizontal"/>
                        </ItemsPanelTemplate>
                    </ListBox.ItemsPanel>
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Header}"
                                       Margin="8,0"
                                       Foreground="White"
                                       AutomationProperties.Name="{Binding Header}"/>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
                <ContentControl x:Name="ModuleContentHost" Margin="8"/>
            </DockPanel>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 2: Write the code-behind**

`src/DynamicIsland/IslandWindow.xaml.cs`:

```csharp
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
        _peekQueue.Enqueue(peekEvent);
        Dispatcher.Invoke(() => _stateMachine.Fire(IslandTrigger.PeekEventRequested));
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

    private void PillBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        _peekTimer.Stop();
        _stateMachine.Fire(IslandTrigger.HoverEnter);
    }

    private void PillBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        _peekTimer.Interval = TimeSpan.FromSeconds(1.5);
        _peekTimer.Start();
    }

    private void PillBorder_Click(object sender, MouseButtonEventArgs e) =>
        _stateMachine.Fire(IslandTrigger.Click);

    private void PillBorder_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space)
            _stateMachine.Fire(IslandTrigger.Click);
    }

    private void ExpandedPanel_KeyDown(object sender, KeyEventArgs e)
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
```

- [ ] **Step 3: Wire it up as the app's window**

Edit `src/DynamicIsland/App.xaml.cs`:

```csharp
using System.Windows;

namespace DynamicIsland;

public partial class App : Application
{
    private IslandWindow? _islandWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _islandWindow = new IslandWindow();
        _islandWindow.Show();
    }
}
```

- [ ] **Step 4: Build and manually verify**

```bash
dotnet build
dotnet run --project src/DynamicIsland
```

Manual checks:
- Window is borderless, transparent, has no taskbar entry
- Hovering the top-center sliver grows it into a compact pill
- Moving the mouse away shrinks it back after ~1.5s
- Clicking the pill expands it into the panel with an empty tab strip (no modules registered yet — this is expected, module content is out of scope)
- Pressing Escape while the panel is focused collapses it back to idle
- Tab/Arrow keys move focus within the panel

- [ ] **Step 5: Commit**

```bash
git add src/DynamicIsland/IslandWindow.xaml src/DynamicIsland/IslandWindow.xaml.cs src/DynamicIsland/App.xaml.cs
git commit -m "feat: add island window with Idle/Peek/Expanded states and animations"
```

---

## Task 10: Autostart shortcut writer (COM)

**Files:**
- Create: `src/DynamicIsland/WshShortcutWriter.cs`

- [ ] **Step 1: Write the COM-backed shortcut writer**

`src/DynamicIsland/WshShortcutWriter.cs`:

```csharp
using System.IO;
using DynamicIsland.Core;
using IWshRuntimeLibrary;

namespace DynamicIsland;

public sealed class WshShortcutWriter : IShortcutWriter
{
    public void CreateShortcut(string shortcutPath, string targetPath)
    {
        var shell = new WshShell();
        var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.Save();
    }

    public void DeleteShortcut(string shortcutPath)
    {
        if (File.Exists(shortcutPath))
            File.Delete(shortcutPath);
    }
}
```

- [ ] **Step 2: Build to verify the COM reference resolves**

```bash
dotnet build src/DynamicIsland
```

Expected: 0 errors. If the COM reference fails to resolve, verify Windows Script Host is registered on the build machine (`regsvr32` is not required — the type library ships with Windows) and that the `COMReference` GUID in `DynamicIsland.csproj` matches Task 1 exactly.

- [ ] **Step 3: Commit**

```bash
git add src/DynamicIsland/WshShortcutWriter.cs
git commit -m "feat: add Windows Script Host shortcut writer for autostart"
```

---

## Task 11: Tray icon and app lifecycle wiring

**Files:**
- Create: `src/DynamicIsland/TrayIconService.cs`
- Modify: `src/DynamicIsland/App.xaml.cs`

- [ ] **Step 1: Write the tray icon service**

`src/DynamicIsland/TrayIconService.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicIsland;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _autostartItem;

    public event Action? ShowRequested;
    public event Action? QuitRequested;
    public event Action<bool>? AutostartToggled;

    public TrayIconService(bool autostartEnabled)
    {
        var menu = new ContextMenuStrip();

        var showItem = menu.Items.Add("Show/Hide Island");
        showItem.Click += (_, _) => ShowRequested?.Invoke();

        _autostartItem = new ToolStripMenuItem("Launch at startup")
        {
            CheckOnClick = true,
            Checked = autostartEnabled
        };
        _autostartItem.CheckedChanged += (_, _) => AutostartToggled?.Invoke(_autostartItem.Checked);
        menu.Items.Add(_autostartItem);

        menu.Items.Add(new ToolStripSeparator());

        var quitItem = menu.Items.Add("Quit");
        quitItem.Click += (_, _) => QuitRequested?.Invoke();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = "Dynamic Island",
            ContextMenuStrip = menu
        };
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
```

- [ ] **Step 2: Wire tray, settings, and autostart into App**

Replace `src/DynamicIsland/App.xaml.cs`:

```csharp
using System.Diagnostics;
using System.Windows;
using DynamicIsland.Core;

namespace DynamicIsland;

public partial class App : Application
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
```

- [ ] **Step 3: Build and manually verify**

```bash
dotnet build
dotnet run --project src/DynamicIsland
```

Manual checks:
- Tray icon appears on launch (system tray, "Dynamic Island" tooltip)
- Right-click menu shows Show/Hide Island, Launch at startup (unchecked by default), Quit
- Toggling "Launch at startup" creates a `.lnk` in `shell:startup`; toggling off removes it
- Quit closes the app and removes the tray icon
- Restart the app: "Launch at startup" checkbox reflects prior state correctly

- [ ] **Step 4: Commit**

```bash
git add src/DynamicIsland/TrayIconService.cs src/DynamicIsland/App.xaml.cs
git commit -m "feat: wire tray icon, settings, and autostart into app lifecycle"
```

---

## Task 12: Full verification pass and README

**Files:**
- Create: `README.md`

- [ ] **Step 1: Run the full automated test suite**

```bash
dotnet test
```

Expected: all Core tests pass (20 tests across Tasks 2, 3, 5, 6), 0 failures.

- [ ] **Step 2: Manual verification checklist (Windows 11)**

Run `dotnet run --project src/DynamicIsland` on a Windows 11 machine and confirm each item from spec §13:
- Borderless/transparent/topmost/no-taskbar-entry
- Click-through in Idle, interactive in Peek/Expanded
- Keyboard-only pass reaches every control (Tab, Arrows, Enter, Esc)
- Narrator announces a sensible name for every focusable element
- Idle CPU/RAM sampled over 10+ minutes stays near zero CPU, flat RAM (Task Manager, "Details" tab, filter to the app's process)

- [ ] **Step 3: Manual verification checklist (Windows 10)**

Repeat Step 2 on a Windows 10 machine. Note any behavioral differences (if a Windows 10 machine isn't available, document this as a known gap rather than skipping silently).

- [ ] **Step 4: Write README**

`README.md`:

```markdown
# Dynamic Island for Windows

A borderless, transparent, always-on-top pill widget for Windows 10/11, inspired by Apple's Dynamic Island.

## Status

Core shell only (window states, animations, module hosting contract, tray, autostart). Feature modules — media controls, focus timer, notifications, system stats, brightness/battery/volume, AI assistant, tasks/notes/agenda — are specced and built separately; see `docs/superpowers/specs/`.

## Build and run

\`\`\`bash
dotnet build
dotnet run --project src/DynamicIsland
\`\`\`

## Test

\`\`\`bash
dotnet test
\`\`\`

## Project layout

- `src/DynamicIsland.Core` — state machine, peek event queue, settings, autostart logic (no WPF dependency, fully unit tested)
- `src/DynamicIsland` — WPF window, Win32 interop, tray icon, COM shortcut writer
- `tests/DynamicIsland.Core.Tests` — xunit tests for Core
```

- [ ] **Step 5: Commit**

```bash
git add README.md
git commit -m "docs: add README covering build, test, and project layout"
```

---

## Plan Self-Review Notes

- **Spec coverage:** §5 window architecture → Task 9/7; §6 states → Task 2/9; §7 auto-peek pipeline → Task 3/4/9; §8 module contract → Task 8; §9 input/accessibility → Task 9 (keyboard) + Task 12 (Narrator manual pass); §10 idle resource use → Task 9 (timers only run in Peek/Expanded) + Task 12 (manual measurement); §11 lifecycle → Task 5/6/10/11; §14 risks → Task 7 (click-through spike).
- **Deferred to future module specs:** actual feature modules (media, stats, notifications, timer, tasks, AI) — intentionally out of scope per this plan's goal.
