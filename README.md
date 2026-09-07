# Astra Helios — Debugger

Part of the **Astra** family. This package works independently of the Astra framework.

The Astra menu labels described here are unreleased. Existing published tags keep
their previous labels until the next release; package IDs and C# APIs are unchanged.

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
"com.misterpxl.helios-debugger": "https://github.com/misterpxl/HeliosDebugger.git#v2.3.1"
```

By default it bootstraps itself in the Editor and Development Builds only.

Helios uses TextMeshPro for the runtime UI. If TMP Essential Resources are not
present, the editor imports them automatically from Unity's built-in `com.unity.ugui`
package. You can also run the import manually with:

`Tools > Astra > Helios > Import TMP Essential Resources`

Create settings with:

`Tools > Astra > Helios > Create Settings Asset`

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
Compilation menu under `Tools > Astra > Helios` manages the symbol for the
selected target.

## Runtime Presentation

The settings asset controls the default tab, remembered tab, trigger corner,
gesture, panel opacity, diagnostic overlays, Escape-to-close behavior, and
screen-space/world-space canvas mode. A project-owned theme can be created with:

`Tools > Astra > Helios > Create Theme Asset`

The default runtime presentation is a dark dev-tool style UI using the bundled
Inter font and Lucide icon set. Both assets live under
`Runtime/Resources/HeliosDebugger`; Inter is licensed under OFL 1.1 and Lucide
under ISC.

`HeliosThemeProfile` exposes surface colors, content colors, button states,
border/elevation colors, corner radii, spacing, and typography sizes. Existing
theme assets keep their previous serialized colors and can opt into the newer
tokens incrementally.

Custom tabs can provide an icon without central registry edits:

```csharp
public sealed class MyRuntimeTab : HeliosTabBase, IHeliosTabIcon
{
    public override string Title => "Gameplay";
    public override int Order => 25;
    public Sprite Icon => HeliosIcons.Get("sliders");

    protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
    {
        widgets.CreateEmptyState("Empty", parent, "No gameplay diagnostics yet.");
    }
}
```

Addons that need their own sprites can create a `HeliosIconSet` asset and call
`HeliosIcons.Register(iconSet)` during their bootstrap.

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
salted PBKDF2 hash with `Tools > Astra > Helios > Configure Access PIN`; the
plaintext PIN is never serialized.

Locking access or expiring the session closes an open debugger. Custom option
controls should check `HeliosOptionControlContext.CanEdit` immediately before
changing a value; custom tab callbacks can check
`HeliosService.CanInteractWithDebugger`.

Reflected options whose getters throw display `<unavailable>` and are retried on
subsequent reads. `HeliosOptionMember.GetValue()` returns `null` for an unavailable
getter; use `TryGetValue(out object value)` when a legitimate `null` must be
distinguished from a failed read.

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

## Astra conventions

See [Astra conventions](Documentation~/AstraConventions.md) for product identity,
menu paths, terminology and the staged API migration policy.

Menu migration: `Tools > HeliosDebugger` is now `Tools > Astra > Helios`.

## Tests

Add `com.misterpxl.helios-debugger` to the consumer manifest’s `testables` and run
its EditMode and PlayMode suites. Include optional integrations when testing them.

## Removal

Remove dependent integrations and project references to Helios APIs/annotations
before removing the base package. Review project-owned settings and generated
catalogs separately. To keep the annotations while excluding the runtime from
Release builds, use `HELIOS_DEBUGGER_DISABLE` as described above.

## Service lifecycle (2.4.0 candidate)

The working source version is 2.4.0; these APIs are not present in the older
installation tags above. Use the validated Astra Git revision until a new release
is tagged. Integrations requiring these APIs must declare Helios 2.4.0 or later.

Subscribe to `Helios.Initialized` and `Helios.ShuttingDown` to follow service
generations without starting the debugger. Use `Helios.TryGetService(out var service)`
to attach immediately when a generation already exists. Reading `Helios.Service`
still initializes it lazily, preserving its existing standalone behavior.

Notifications are synchronous on the calling thread; call lifecycle and registration
APIs on Unity’s main thread. `ShuttingDown` runs before service disposal, after
`TryGetService` starts returning false. Detach from the supplied service rather
than reading `Helios.Service`. Reentrant `Initialize`/`Shutdown` is rejected;
subscriber exceptions are logged without skipping the other subscribers.

Each integration owns and removes its exact registrations, then unsubscribes when
it is disposed. `service.UnregisterAction(action)` preserves a replacement action
with the same ID. `Helios.Shutdown(expectedService)` cannot stop a newer generation.
Lifecycle subscriptions and tab providers reset at SubsystemRegistration; static
integrations should register again at BeforeSceneLoad. Scene reload disabled is
not qualified by the current Astra session tests.
