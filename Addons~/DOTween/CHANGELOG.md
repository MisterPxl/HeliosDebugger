# Changelog

All notable changes to the Helios Debugger DOTween addon are documented here.

## [2.1.0] - 2026-09-09

- Add `IDOTweenSourceProvider` / `DOTweenSourceProviders`: optional providers describe the
  authoring source of an observed tween (owner, asset, step, diagnostics). Snapshots expose
  `Source`, the tab shows it and the search matches it. No behaviour change without a provider.

## [2.0.0] - 2026-09-09

- Target Helios 3.x and rename namespaces/assemblies to Astra.Helios.Integrations.DOTween.
- Preserve the Tweens tab identity and script/assembly metadata.
- Keep DOTween and Helios compile constraints aligned across runtime and tests.

### Changed

- Adopt Astra display names, menus, integration terminology and shared documentation conventions. Package IDs remain unchanged; the migration candidate above changes C# APIs.

## [1.1.3] - 2026-08-10

### Changed

- Tweens tab snapshots refresh at most four times per second, matching the
  documented runtime behavior.

## [1.1.2] - 2026-08-10

### Added

- `LICENSE.md` (MIT) and this changelog.

## [1.1.1] - 2026-08-10

### Changed

- The addon moved to `Addons~/DOTween` in the repository; install it with
  `?path=/Addons~/DOTween`. It is no longer imported as content of the base
  package.

## [1.1.0] - 2026-08-01

### Added

- Tab icon through `IHeliosTabIcon`.

## [1.0.0] - 2026-07-31

### Added

- Initial release: runtime `Tweens` tab for DOTween Free with search,
  per-tween and bulk controls, and generation-safe command handling.
