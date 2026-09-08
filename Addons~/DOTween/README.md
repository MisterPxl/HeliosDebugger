# Astra Helios — DOTween Integration

An optional **Astra integration**. Install its prerequisites explicitly; the base
packages remain usable independently. This **2.0.0 migration candidate** requires
Helios 3.x and uses `Astra.Helios.Integrations.DOTween`. Existing tags keep the old
API. See the [Helios migration guide](../../Documentation~/Migration-3.0/README.md).

Optional runtime `Tweens` tab for DOTween Free. The base
`com.misterpxl.helios-debugger` package has no DOTween dependency and continues
to compile without this integration.

## Requirements

1. Install DOTween Free separately.
2. Open `Tools > Demigiant > DOTween Utility Panel`.
3. Run setup. The standard DOTween Free import exposes `DOTween.dll`; generating
   asmdefs may additionally create `DOTween.Modules`.
4. Verify setup supplied the `DOTWEEN` scripting define used to keep the integration
   assembly excluded when DOTween is absent.
5. Install Helios Debugger and this integration.

Git URL:

```text
https://github.com/misterpxl/HeliosDebugger.git?path=/Addons~/DOTween#codex/astra-foundation
```

Manifest entry:

```json
"com.misterpxl.helios-debugger.dotween": "https://github.com/misterpxl/HeliosDebugger.git?path=/Addons~/DOTween#codex/astra-foundation"
```

The integration lives under `Addons~` so it is never imported as content of the base
package; it is only available through this dedicated package URL.

DOTween is intentionally not declared as a UPM dependency because DOTween Free
is distributed and configured separately.

## Runtime behavior

The integration registers a deferred `IHeliosTabProvider` before the first scene. It
does not initialize Helios. The tab appears only if Helios itself passes its
bootstrap policy and creates a root, or if the application initializes Helios
explicitly.

Helios defaults to Editor and Development Builds. `HELIOS_DEBUGGER_DISABLE`
removes the runtime assemblies; `HELIOS_DEBUGGER_DISABLE_AUTO_BOOT` keeps the
API available but prevents automatic UI creation. The integration does not bypass
either policy.

The tab provides:

- Active, Playing and Paused totals
- Search by tween ID, target or concrete tween type
- Per-tween Pause/Play, Complete and Kill
- Pause All and Play All
- Kill All with a three-second, two-click confirmation

Snapshots refresh at most four times per second while the tab is visible.
Commands resolve weak tween handles and recheck `Tween.IsActive()` immediately
before mutation. The monitor appends a no-op `onKill` generation sentinel while
a tween is listed, preserving existing callbacks; this prevents a pooled Tween
instance from receiving a command intended for its previous generation.

## Data limits

- Auto-killed completed tweens are already gone and cannot be displayed.
- Tween IDs are optional.
- DOTween Free does not expose the original creation callsite.
- The visible rows are the union of DOTween's public `PlayingTweens()` and
  `PausedTweens()` lists. `TotalActiveTweens()` can therefore exceed the number
  of enumerable rows for transient internal states.

## Sample

Import `DOTween Monitoring` from Package Manager. Add
`HeliosDOTweenSample` to a GameObject to create looping playing and paused
tweens with IDs and targets.

## Prerequisites and tests

The manifest declares these package versions:

- `com.misterpxl.helios-debugger`: `3.0.0`.
- `com.unity.ugui`: `2.0.0`.

Install the Astra base packages explicitly in the consumer manifest, using the
Git URLs from their READMEs. Git packages are not fetched transitively from
version-only dependencies. Unity registry dependencies resolve normally.

Add `com.misterpxl.helios-debugger.dotween` to the consumer manifest’s `testables` and run
its suites in Unity Test Runner. Keep DOTween installed while running these tests.

## Removal

Remove project components, assets or code that reference this integration before
removing it through Package Manager. The base packages can remain installed.
