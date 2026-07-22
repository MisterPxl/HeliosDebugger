# HeliosDebugger

HeliosDebugger is a standalone runtime debugger for Unity projects.

It provides:

- Runtime console backed by `Application.logMessageReceivedThreaded`
- Lightweight profiler overview
- Options and cheats tab powered by `[HeliosOptions]`, `[HeliosOption]`, and `[HeliosAction]`
- System information tab
- Bug report builder with local export, webhook, and native-share fallback transports
- Floating trigger, keyboard toggle, gamepad combo, and optional Konami sequence

## Quick Start

Add the package to the project's `Packages/manifest.json`:

```json
"com.misterpxl.helios-debugger": "https://github.com/misterpxl/HeliosDebugger.git#v1.0.0"
```

By default it bootstraps itself in the Editor and Development Builds only.

Create settings with:

`Tools > HeliosDebugger > Create Settings Asset`

The project-owned settings asset is stored at
`Assets/Resources/HeliosDebuggerSettings.asset`.

## Options

```csharp
using HeliosDebugger;

[HeliosOptions("Gameplay")]
public static class GameplayDebugOptions
{
    [HeliosOption("God Mode")]
    public static bool GodMode;

    [HeliosOption("Spawn Count")]
    [HeliosRange(0, 50, 1)]
    public static int SpawnCount = 3;

    [HeliosAction("Give Coins", Pin = true)]
    public static void GiveCoins()
    {
        UnityEngine.Debug.Log("Coins granted.");
    }
}
```

For instance-owned options, call:

```csharp
Helios.RegisterOptions(myOptionsInstance);
```

For options that appear and disappear at runtime, use a dynamic container and
remove it when the owning object is disabled or destroyed:

```csharp
public sealed class EnemyDebugOptions : MonoBehaviour
{
    private readonly HeliosDynamicOptionContainer _options = new HeliosDynamicOptionContainer();
    private float _speed = 1f;

    private void OnEnable()
    {
        _options.AddOption(HeliosOptionDefinition.Create(
            "Enemy Speed",
            () => _speed,
            value => _speed = value,
            "Enemies"));
        _options.AddAction(HeliosOptionDefinition.FromMethod(
            "Log Enemy Speed",
            () => Debug.Log(_speed),
            "Enemies"));

        Helios.AddOptionContainer(_options);
    }

    private void OnDisable()
    {
        Helios.RemoveOptionContainer(_options);
    }
}
```

You can also register individual dynamic entries directly:

```csharp
IHeliosValueOption option = HeliosOptionDefinition.Create(
    "Wave Count",
    () => waveCount,
    value => waveCount = value,
    "Spawning");

Helios.AddOption(option);
Helios.RemoveOption(option);
```

## Core.Debug Bridge

`Core.Debug` remains the template command registry. The optional `Core.Debug.HeliosBridge` module adapts Core debug commands into Helios actions while Helios captures Core logs through Unity's global log stream.

To avoid two overlays, disable `Create Runtime Panel` on `DebugServiceConfiguration` when Helios owns the UI.

## Build Policy

The default policy is Development Build only. In production builds the runtime bootstrap exits early. The Editor validator also warns when Helios is present in a non-development build.

## Opt Out

Add these scripting defines when needed:

- `HELIOS_DEBUGGER_DISABLE` disables the runtime code path.
- `HELIOS_DEBUGGER_DISABLE_AUTO_BOOT` keeps the API available but prevents automatic bootstrap.
