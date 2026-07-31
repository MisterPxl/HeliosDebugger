# Helios Debugger - DOTween

Optional runtime `Tweens` tab for DOTween Free. The base
`com.misterpxl.helios-debugger` package has no DOTween dependency and continues
to compile without this addon.

## Requirements

1. Install DOTween Free separately.
2. Open `Tools > Demigiant > DOTween Utility Panel`.
3. Run setup. The standard DOTween Free import exposes `DOTween.dll`; generating
   asmdefs may additionally create `DOTween.Modules`.
4. Verify setup supplied the `DOTWEEN` scripting define used to keep the addon
   assembly excluded when DOTween is absent.
5. Install Helios Debugger and this addon.

Git URL:

```text
https://github.com/misterpxl/HeliosDebugger.git?path=/Addons/DOTween#dotween-v1.1.0
```

Manifest entry:

```json
"com.misterpxl.helios-debugger.dotween": "https://github.com/misterpxl/HeliosDebugger.git?path=/Addons/DOTween#dotween-v1.1.0"
```

DOTween is intentionally not declared as a UPM dependency because DOTween Free
is distributed and configured separately.

## Runtime behavior

The addon registers a deferred `IHeliosTabProvider` before the first scene. It
does not initialize Helios. The tab appears only if Helios itself passes its
bootstrap policy and creates a root, or if the application initializes Helios
explicitly.

Helios defaults to Editor and Development Builds. `HELIOS_DEBUGGER_DISABLE`
removes the runtime assemblies; `HELIOS_DEBUGGER_DISABLE_AUTO_BOOT` keeps the
API available but prevents automatic UI creation. The addon does not bypass
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
