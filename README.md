# ModCore (formerly RebornMenu)

> **ModCore transition (v0.2.14):** The user-facing mod is now **ModCore**. Its BepInEx GUID `com.horizon.orbus.rebornmenu`, DLL name `OrbusRebornMenu.dll`, existing config path, developer API assembly and GitHub repository intentionally stay the same, so saved settings and third-party integrations keep working. RebornManager's next update installs this companion automatically when it prepares BepInEx. Existing BepInEx instances can install or update it from Client Mods. A server-side ModCore, if built, is a separate server component and is not bundled into this client DLL.

**One universal in-game VR mod configuration menu for OrbusVR Community Edition and Orbus Renewed.**

**Status: native Mods tab and multi-mod configuration browser confirmed working in VR on Spades.** Players can select installed mods and modify live BepInEx settings using original Orbus styling. v0.2.13 fixes a bug that could freeze the entire Mods page partway down after scrolling in General Settings. This fix still requires live VR verification.

## The idea

**RebornManager** installs ModCore as a **core companion** whenever it prepares an OrbusVR instance with BepInEx (once the corresponding manager update is published). Players won't have to search the mod library for a separate menu plugin. **RebornManager and other mods must continue working without it.**

In VR, the **current design goal** is to open Mods from Orbus's existing in-game Settings/menu navigation, using its native appearance and controller pointer when technically possible. The earlier wrist-rune prototype has been retired. Browse your loaded mods, select one, and edit its settings through a consistent interface.

- **Any BepInEx mod:** ordinary \`Config.Bind(...)\` entries automatically get basic settings controls (toggle, slider for a declared range, dropdown for a declared value list, text input, reset).
- **Mods with custom pages:** optional declarative config-page layout, with nicer grouping and explanatory text.
- **Mods with an advanced provider:** optionally show live counters, custom previews and a **grab / rotate / scale** editor for a known HUD target.
- **Unloaded/disabled mods:** conservative editor for recognised BepInEx \`.cfg\` files; unsupported files remain read-only.
- **Safe controls:** Reset to Defaults, Recenter Menu, Reset HUD Layout, and clear notices when a restart is needed.

### Intended player flow

\`\`\`text
RebornManager → Select Instance → Install BepInEx
                                  └─ ModCore automatically installed (on updated RM)
                                       └─ Start OrbusVR → Settings → Mods (planned)
                                            ├─ CastMetrics
                                            ├─ QuestHelper
                                            ├─ RebornCam
                                            ├─ GoblinTimer
                                            └─ Any other mod with BepInEx configs
\`\`\`

A third-party author **does not have to know RebornMenu exists** for their normal BepInEx config to be discoverable.

## Current experiment: native Orbus Settings Mods entry (v0.2.0)

We're abandoning the unsuccessful v0.1 wrist-rune/floating-text experiment. The current code attempts a small **Mods entry inside the existing Orbus Settings PowerUI document**, using the game's own renderer and VR pointer. This is a guarded first test: it changes *only* recognised Settings HTML, otherwise leaves Orbus untouched. It does **not** depend on RebornCam.

**Historical note:** The first diagnostic-only v0.2.0 experiments could not locate the insertion API. Later versions use the verified Dom.Element HTML insertion method; the native tab and interactive multi-mod settings have since been confirmed working in VR.

See [v0.2 native Mods tab testing and rollback](docs/NATIVE_MODS_V020_TEST.md).

## New test: Mods after Graphics (Community and Spades)

The two FourOfSpades extension tabs sit above the original Orbus Settings tabs. RebornMenu's first native UI test instead adds Mods as a **fifth original Settings tab**, after Graphics. On both clients this is the same insertion point and does not depend on Spades' custom upper row.

- Uses existing Orbus `settings-tab-control`, `settings-tab`, `compact option`, `menu-list`, headings and CSS assets.
- Appends to the verified native PowerUI DOM, without replacing existing tab elements or their click handlers.
- Narrowing the original four tabs to 104px, plus 8px spacing, lets all five fit the existing 570px tab header. If the runtime APIs cannot set those widths, Mods stays hidden to prevent overlap.
- **Confirmed in-game:** Mods appears after Graphics and the first Ability Labels Smaller/Larger controls changed the live label size.
- **Confirmed in VR:** left-hand mod selector, right-hand per-mod settings and native Ability Labels controls. Latest update removes the extra outer page scrollbar, leaving independent list/settings scroll regions (not yet VR-tested).
- Generic BepInEx config discovery works in-game: supported bool, enum, and bounded int/float/double entries appear without hardcoded mod pages. Free-form strings are deliberately omitted. Developer-only entries are now hidden in the new build.
- See [Automatic mod settings for authors](docs/AUTOMATIC_MOD_SETTINGS.md) for the built-in contract and supported config types.
- [Independent scroll and obsolete .cfg cleanup](docs/MODS_SCROLL_AND_CONFIG_CLEANUP.md) documents the new scroll layout, retired Ability Labels/QuestHelper entries, and optional backup-first cleanup script.
- Safely leaves original UI alone when the recognised Settings document or tab identifiers are absent; emits one diagnostic warning in `BepInEx/LogOutput.log`.

From any PowerShell folder, run `& "$env:USERPROFILE\OneDrive\Documents\OrbusRebornMenu\release.ps1"`, update RebornMenu in RebornManager, and open Settings in VR. Look for Mods immediately to the right of Graphics and test the new Ability Labels Scale slider with Bard equipped. Send the `[RebornMenu]` lines from the log if missing or unresponsive.

To roll back without uninstalling, close the game and set `[NativeMods] EnableLiveModsTab = false` in `BepInEx/config/com.horizon.orbus.rebornmenu.cfg` then relaunch.

## Automatic core companion and existing installations

ModCore remains a normal, managed client plugin with its existing ID and published `OrbusRebornMenu.dll` asset. On new BepInEx installations, an updated RebornManager attempts to install the latest stable ModCore release automatically. When network or release lookup fails, **BepInEx still installs successfully** and players can retry through **Client Mods → ModCore → Install**. No client is silently changed when already prepared, and intentionally disabled or manually installed copies are respected.

Maintainers build/publish with `./release.ps1`; this uses local game interop **only as compilation references**, never installs into a particular instance. Existing installations use **Client Mods → ModCore → Update** through the normal stable-release mechanism. The server-side half of ModCore will be a separate JavaScript server component.

Current test: [v0.2 native Settings installation, testing and rollback](docs/NATIVE_MODS_V020_TEST.md). The [v0.1 wrist prototype notes](docs/PROTOTYPE_V01_TEST.md) are archived.

## Documentation

- [Architecture and VR interaction specification](docs/ARCHITECTURE.md)
- [Current: native Orbus Settings integration plan](docs/NATIVE_MENU_PLAN.md)
- [Retired: early wrist-rune prototype design](docs/VR_UX.md)
- [Developer guide and optional API/manifest contract](docs/DEVELOPER_API.md)
- [Implementation milestones and acceptance tests](docs/ROADMAP.md)
- [How RebornManager will install, repair and update RebornMenu](docs/RM_INTEGRATION.md)
- [CastMetrics proposed config-page example](examples/CastMetrics/rebornmenu.json)
- [Declarative page JSON Schema — proposed v1](schemas/rebornmenu.schema.json)

## Priorities

The initial target is **Windows PCVR Unity 2018 / BepInEx 6 IL2CPP**, tested on both [Community Edition](https://github.com/OrbusVR-Community/reborn-server) and [FourOfSpades' Renewed client](https://github.com/FourOfSpades4/reborn-server).

Standalone Quest Android mod loading is a separate future project and cannot simply reuse Windows BepInEx DLLs.

**We are deliberately putting further QuestHelper, CastMetrics, GoblinTimer and other mod feature work on hold** until a tested RebornMenu runtime, generic BepInEx config editing, and shared HUD placement are ready. This will give all of them a common foundation rather than a different settings UI inside every mod.

## Maintained alongside

- [OrbusRebornManager](https://github.com/Hor1zonVR/OrbusRebornManager)
- [OrbusRebornCamera](https://github.com/Hor1zonVR/OrbusRebornCamera)
- [OrbusQuestHelper](https://github.com/Hor1zonVR/OrbusQuestHelper)
- [CastMetrics](https://github.com/Hor1zonVR/CastMetrics)

**Not affiliated with the original OrbusVR developers.** Respect community server rules and the original game/client licences when building plugins.

## v0.2.13 — Recoverable Mods scrolling

On some Renewed/Spades sessions the Mods tab was partially off-screen:
the first installed mods and the top of the selected page disappeared,
and scrolling up had no effect. RebornMenu was setting `overflowY:hidden`
on the **game's outer Settings scroller** while opening Mods. If the
player had previously scrolled down in General, this trapped the entire
page at that offset. Resetting only the Mods sidebar didn't fix it.

v0.2.13 keeps the original `.largesettingsmenu-inner` scroll behaviour
enabled (`overflowY:auto`) even on Mods. It attempts to reset the
outer Settings, mod navigation and selected content to the top on
opening Mods, and resets the right content when changing selected mods.
The reset supports multiple reflected `scrollTop` or `scrollTo` APIs;
if a PowerUI client exposes none of them, the native outer scrollbar
remains usable rather than getting locked. This avoids depending on
the internal implementation of PowerUI's per-client scrollTop setter.

**No saved mod configs, custom page providers, original game Settings
markup, or WeaponAdjustments presets are modified.** The generic
custom-page API introduced in v0.2.12 remains unchanged. Test this
with General scrolled to the bottom first, then open Mods and verify
that Ability Labels and the WeaponAdjustments page header are visible
and that all columns can still scroll up/down.
