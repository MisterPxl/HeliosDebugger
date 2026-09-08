# Helios 3.0 migration candidate

Published on `codex/astra-foundation`, without a release tag. Install the exact
revision in the framework Astra catalog. Pair Helios 3.x with Helios–DOTween 2.x
and Aegis–Helios 2.x when those integrations are used. Base packages remain independent.

## Upgrade

1. Back up source, assets and `.meta` files, manifest/lockfile, generated catalogs
   and project preferences. Keep the old package revisions for rollback.
2. Update the base and installed integrations together. Git consumers must list
   each prerequisite explicitly; version-only dependencies do not fetch Git packages.
3. Change `HeliosDebugger` imports to `Astra.Helios`, and `HeliosDebugger.DOTween`
   to `Astra.Helios.Integrations.DOTween`. Apply the assembly mappings in
   [identities.json](identities.json) to asmdefs, reflection strings and custom
   tooling; recompile dependent DLLs. Update imported sample scripts in place,
   retaining their metadata and configured assets. Do not replace strings in YAML.
4. Open Unity and check assets and references. Run `Tools > Astra > Helios >
   Regenerate Options Catalog`; wait for the following compilation before building.
   For batch imports, explicitly execute
   `Astra.Helios.Editor.HeliosOptionsCatalogGenerator.Generate`, then start a
   separate process for verification/build. `-quit` can precede delayed callbacks.
5. Inspect existing settings, themes, saved tab selection and persisted options,
   then run your consumer tests and target builds before resaving project assets.

## Serialization and stable identities

The inventory records nine assembly mappings and 136 Unity public type mappings.
All 206 existing metadata files are preserved, including renamed asmdef GUIDs.
The four historical engine-free annotation types receive no UnityEngine dependency.
`MovedFrom` supports Unity serialization; this is not a general source or binary
compatibility layer. Settings resource paths, serialized fields, access data,
menu preference keys and generated file paths keep their historical identities.

Seven built-in/sample type IDs retain their old full names through the engine-free
`HeliosTypeIdentityAttribute`. Custom tab/options types that move namespace can
use `[HeliosTypeIdentity("Old.Namespace.TypeName")]` to keep existing tab IDs and
`HeliosOption.<type identity>.<member name>` preference keys. The attribute is not
inherited. Keep the original member name when preserving its option key.

An obsolete `HeliosDebugger.HeliosGeneratedOptions.Register` relay lets the previous
generated catalog compile during the first import. It does not translate old
reflection strings. Regeneration rewrites those strings and `link.xml`; the build
validator rejects a catalog still calling the legacy API. No other old API aliases
are promised.

## Evidence and reproduction

`Legacy~` contains immutable fixtures captured with Helios
`7201ceb8d671c3b050d027ecc2692aa243ae26c1`, including the DOTween integration;
Aegis and its Helios integration used `d1c4a94992213d8a442d9056a22b2b68f313abcd`.
Verify all 14 file hashes in `legacy-sha256.json` before use. Fixtures include
settings, theme, shared/cyclic managed references, original generated C#/linker
files and synthetic preference values. PIN fields are synthetic serialization
data, not an access-policy authentication test.

The framework `Tools/Astra/Migration` probes implement Capture, Verify, Resave,
Generate, VerifyCatalog and BuildPlayer in disposable consumers. Preference checks
inject the captured historical key/value pairs for each run, exercise the real
service/options registry and restore the prior preference store afterward.
They do not claim unchanged on-disk preferences across processes. The initial
import compiles the byte-identical old catalog through the relay before generation.

Qualification uses Unity 6000.4.0f1/macOS: old-to-old import, new import, resave,
reopen, catalog/linker registration, EditMode/PlayMode, optional package compilation
and a macOS Mono Development Player. The Player is built, not launched. Unity
6000.0, IL2CPP, Release stripping, other targets and external custom classes are
not qualified by these fixtures. See the framework HeliosMigrationValidation report
for exact counts and package revisions.

## Rollback

Restore the saved package revisions together with source, generated catalogs,
manifest/lockfile and original assets/metas. Restore backed-up preferences if
application code changed them. Do not downgrade only the package after resaving
managed-reference assets or generating catalog strings with the new identities.
