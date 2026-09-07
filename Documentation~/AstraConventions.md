# Astra conventions

Version: 1. Applies to the working versions of the framework and Astra packages.

## Product identity

| Product | UPM display name | Stable package ID |
|---|---|---|
| Aegis | Astra Aegis — Project Validation | `com.misterpxl.aegis` |
| Valkyrie | Astra Valkyrie — Inspector | `com.misterpxl.valkyrie` |
| Helios | Astra Helios — Debugger | `com.misterpxl.helios-debugger` |

Integrations use `Astra <Host> — <Target> Integration`, for example
`Astra Aegis — Valkyrie Integration`. Their existing package IDs remain stable.
The framework is presented as `Astra Framework — Unity Template`.

## Unity menus

- Commands: `Tools/Astra/<Product>/...`.
- Framework commands: `Tools/Astra/Framework/<Layer or Tool>/...`.
- Asset and component creation: `Astra/<Product>/<Category>/...`.
- Framework asset creation: `Astra/Framework/<Core|Game|Project>/...`.
- Write visible labels as words, such as `Scene Flow`, rather than C# identifiers.
- Keep vendor menus under their vendor's own identity; DOTween setup remains
  under `Tools/Demigiant`.

The menu moves change discovery paths, not script GUIDs or serialized assets.
Published tags keep their earlier labels until a new release includes these changes.
Scripts that call `EditorApplication.ExecuteMenuItem` should update their menu
paths. Public C# entry points keep their current names in this phase.

## Terminology and structure

| Term | Meaning |
|---|---|
| Package | Independently useful installable product |
| Integration | Optional package that connects products |
| Backend | Replaceable implementation of a capability |
| Module | Framework adapter for installation and lifecycle |
| Sample | Removable usage example |

Use `Runtime/`, `Editor/`, `Tests/EditMode/`, `Tests/PlayMode/`, `Samples~/` and
`Documentation~/` when relevant. Keep the historical `Addons~/` distribution
directory: published Git URLs address it directly. Existing assembly and test
directory names are migrated separately, not silently moved for presentation.

Each package documents installation, prerequisites, a minimal example,
integration choices, testing, removal and migration when needed. Its manifest
provides a display name, description, author, license and documentation/changelog/
license links. Samples retain descriptive names rather than a uniform placeholder.

## Independence and releases

Base packages do not depend on their optional integrations or on framework
services. Integrations declare their prerequisites. Each product has its own
SemVer; a catalog records the exact combinations validated together.

Git consumers explicitly list the Git sources of all required packages in their
project manifest. Local overrides are for development and validation, not
distribution. Existing Git tags are immutable; unreleased source changes must
not be described as already available through an older tag.

## API compatibility

Current namespaces, assemblies, symbols, settings paths, report schemas, rule IDs,
suppression keys and resource names retain their identities during this phase.
Use PascalCase for public members, `_camelCase` for private fields and `I...`
for interfaces. Prefer responsibility-based names such as `Settings`, `Profile`,
`Definition`, `Asset` and `Result` in new APIs.

The later namespace target is `Astra.<Product>` and
`Astra.Framework.Core.<Module>` / `Astra.Framework.Game.<Module>`.
Integration namespaces target `Astra.<Host>.Integrations.<Target>`.
This is a future compatibility migration requiring old-asset fixtures and a
consumer migration guide, not a naming rule already enforced on existing types.

Atlas is the retained name for persistence and migrations. Chronos is reserved
for timers, scheduling and updates.
