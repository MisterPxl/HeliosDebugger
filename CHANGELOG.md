# Changelog

All notable changes to the Helios Debugger package are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the package adheres to [Semantic Versioning](https://semver.org/).

## [2.3.0] - 2026-08-10

### Added

- `HeliosPinAccessPolicy` exposes `IsThrottled` and `RetryNotBeforeUtc`; failed
  unlock attempts beyond three now back off exponentially (1s doubling up to
  60s) before the next attempt is evaluated.
- `HeliosWebhookReportTransport.IsValidEndpoint` is public and shared with the
  editor build validator.
- `LICENSE.md` (MIT) and this changelog.

### Changed

- Webhook endpoints must use HTTPS at runtime as well as at build time; plain
  HTTP is only tolerated for loopback addresses used in local testing.
- Saving a PIN from the editor window now enables `Require Pin` on the
  settings asset instead of leaving the challenge silently disabled.
- `Tools > HeliosDebugger > Compilation` menu items apply the
  `HELIOS_DEBUGGER_DISABLE` define to every valid build target, including the
  dedicated server, instead of only the selected one.
- `HeliosShortcutContext` is a readonly struct; polling shortcuts no longer
  allocates every frame. Docked profiler overlay refreshes on the profiler
  interval instead of every frame.

### Fixed

- PIN salt and hash lengths are validated when the access policy is created;
  malformed credentials fail closed to deny-all.
- Plaintext PIN fields in the editor window are never serialized and are
  cleared when the window closes.
- The webhook `UnityWebRequest` is disposed on every code path and no longer
  buffers the unused HTTP response body.
- Native share deletes its temporary materialized report files after the
  share completes, fails, or is cancelled.
- Profiler history trimming removes surplus samples in one pass.

## [2.2.0] - 2026-08-10

### Added

- `HeliosLogStore.Revision` and `HeliosLogStore.Count` for allocation-free
  change detection.

### Changed

- The DOTween addon moved to `Addons~/DOTween` so it is no longer imported as
  content of the base package; install it through its dedicated `?path=` URL.
- Console tab, docked console overlay, and the virtualized log list rebuild
  only when the log store revision or the visible window changes.

### Fixed

- Data race on the threaded log sequence counter.

## [2.1.0] - 2026-08-01

### Added

- Theme tokens for button states, borders, elevation, radii, spacing, and
  typography on `HeliosThemeProfile`.
- `IHeliosTabIcon` and `HeliosIcons.Register` for icon extensibility without
  central registry edits.

## [2.0.0] - 2026-07-31

### Changed

- **Breaking:** report transports own a local `HeliosTransportId` and receive
  a canonical `HeliosReportBundle`; `HeliosReportTransportKind` and the
  path-based `HeliosBugReport` were removed.

## [1.0.0] - 2026-07-22

### Added

- Initial release: runtime console, profiler overview, options and cheats
  tabs, system information, bug report builder, and access policies.
