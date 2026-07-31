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
"com.misterpxl.helios-debugger": "https://github.com/misterpxl/HeliosDebugger.git#v2.0.0"
```

By default it bootstraps itself in the Editor and Development Builds only.

Create settings with:

`Tools > HeliosDebugger > Create Settings Asset`

The project-owned settings asset is stored at
`Assets/Resources/HeliosDebuggerSettings.asset`.

## Sample

Import `Usage Examples` from the Package Manager, then open
`HeliosDebuggerSample.unity`. Enter Play Mode and use the floating trigger or
the backquote key to inspect the static and dynamic sample options.

`HeliosDebuggerSample.prefab` can also be dropped into another test scene.

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

The default policy is Development Build only. In production builds the runtime bootstrap exits early. Strict release validation fails the build while Helios is compiled unless release usage is explicitly allowed.

For complete stripping, add `HELIOS_DEBUGGER_DISABLE` to the release build
profile. The runtime and runtime-dependent editor assemblies use this as an
assembly constraint, so their code is not compiled into that target. The
Compilation menu under `Tools > HeliosDebugger` manages the symbol for the
selected target.

## Runtime Presentation

The settings asset controls the default tab, remembered tab, trigger corner,
gesture, panel opacity, diagnostic overlays, Escape-to-close behavior, and
screen-space/world-space canvas mode. A project-owned theme can be created with:

`Tools > HeliosDebugger > Create Theme Asset`

World-space anchors and screen-space cameras are scene objects and must be
provided at runtime through `HeliosDebuggerRoot.SetWorldSpaceAnchor` or
`SetCanvasCamera`.

Pinned values and parameterless actions remain available in the game view when
the main panel is closed. The console supports severity filters, duplicate
collapse, full entry details, clipboard copy, and filtered file export.

## Access Policy

Projects can install their own access policy:

```csharp
Helios.SetAccessPolicy(myAccessPolicy);
```

The built-in PIN policy is enabled from the settings asset. Configure its
salted PBKDF2 hash with `Tools > HeliosDebugger > Configure Access PIN`; the
plaintext PIN is never serialized.

## Version 2 Migration

Version 2 removes `HeliosReportTransportKind` and the path-based
`HeliosBugReport`. Report transports now own a local `HeliosTransportId` and
receive a canonical `HeliosReportBundle`, allowing new transports without
editing a shared enum. Update custom transports to the new interface and use
the artifact lookup API instead of `LogsPath`, `ScreenshotPath`, and related
properties.

## Opt Out

Add these scripting defines when needed:

- `HELIOS_DEBUGGER_DISABLE` disables the runtime code path.
- `HELIOS_DEBUGGER_DISABLE_AUTO_BOOT` keeps the API available but prevents automatic bootstrap.
