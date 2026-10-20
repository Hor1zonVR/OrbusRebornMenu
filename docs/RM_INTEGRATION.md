# RebornManager ↔ RebornMenu installation contract

**Design requirements. Not shipped yet.** This document defines how the Windows RebornManager will install, repair and version RebornMenu **once a tested compiled release exists**.

## Experience

- On **New instance → Prepare for mods**, RM installs the official BepInEx IL2CPP loader first, then attempts to install the RebornMenu core companion into that **same** managed game instance.
- On **existing instance → Install BepInEx**, the same follow-up applies.
- If RebornMenu fails to download, BepInEx remains usable. The UI reports **RebornMenu not installed — Retry** rather than pretending full success.
- If BepInEx is already present, RM does **not** need to reinstall it; **Install/Repair RebornMenu** is a separate action.
- RebornMenu is intentionally bundled as a managed companion **not** a dependency of RM itself. A player may disable it without losing other mods or game functionality.
- No silent changes to original/unmanaged game folders; RM should explicitly ask before touching manually imported installations.
- RM must not insert RebornMenu into the curated mod list as a normal discoverable plugin; its lifecycle belongs to per-instance loader setup and maintenance.

## Verified release delivery

Once the real binary is tested, publish a stable release from **Hor1zonVR/OrbusRebornMenu**, with:

1. A single unambiguous compiled DLL (plus optional files if required).
2. SHA-256 digest and release version.
3. Compatibility declaration for BepInEx version, Community Edition and Orbus Renewed build.
4. An exact install destination, e.g. \`BepInEx/plugins/OrbusRebornMenu/OrbusRebornMenu.dll\`.

RM must download the approved release from the official repo, verify its digest, stage before replacing, and not overwrite unrelated files in \`BepInEx/plugins\`. A failed plugin install should leave both the existing RebornMenu version and BepInEx intact.

**Safety:** Avoid pinning a mutable \`latest\` release without checking compatibility. Installing BepInEx must not mean blindly installing untested builds of RebornMenu. Pin to a known-good release and expose updates separately.

## Lifecycle and recovery

A per-instance status panel should distinguish:

| Status | Action |
| --- | --- |
| No BepInEx | Install BepInEx + offer automatic RebornMenu |
| BepInEx installed / no menu | Install RebornMenu |
| Ready | Open quick help, check release, repair |
| Menu disabled | Re-enable |
| Menu outdated | Update to compatible release |
| Incompatible with game build | Do not load automatically; show warning and previous compatible version |
| Menu crash / missing DLL | View logs, repair or roll back menu |

RM's existing in-place **game version updater** preserves the BepInEx tree; menu files/settings should be preserved too. Regenerate game-dependent IL2CPP interop as needed. Never automatically delete a player's menu config during a game update or rollback.

The client-side runtime must have a separate failure boundary: an exception in a menu page or third-party provider should not crash the game or lock VR controls.

## Explicit limits

- Automatic install does not mean required-for-other-mods. **No dependency enforcement**.
- The RebornMenu plugin must never need elevated Windows permissions or write outside the chosen game instance.
- No user telemetry or remote config execution.
- No server gameplay changes.
- Standalone Quest uses different Android modding technology and is out of scope for the initial PC release.

## Prerequisites to implement

1. A real, tagged, tested RebornMenu runtime release from the repo.
2. Verified BepInEx 6 IL2CPP compatibility and Unity UI/input behaviour on both supported PC game editions.
3. Fail-safe installation and uninstall tests on a clean managed instance.
4. Recovery/disable procedure that works if the plugin causes boot or VR input problems.
5. Mod settings data retained during per-instance game updates.

Do **not** add placeholder auto-install logic that fetches source-only releases or suggests a compiled mod exists when it doesn't.
