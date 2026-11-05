# OrbusRebornMenu

**One universal in-game VR mod configuration menu for OrbusVR Community Edition and Orbus Renewed.**

**Status: experimental native Settings integration.** The source now includes a guarded Mods tab that appears after Graphics when the original PowerUI Settings header is verified, plus Smaller/Larger controls for Ability Labels' live BepInEx scale configuration. **The new integration still needs compilation and in-headset verification.** Keep a backup test instance and disable the feature if Settings behaves unexpectedly.

## The idea

**RebornManager** will eventually install RebornMenu as a **core companion** whenever it prepares an OrbusVR instance with BepInEx. Players won't have to search the mod library for a separate menu plugin. **RebornManager and other mods must continue working without it.**

In VR, the **current design goal** is to open Mods from Orbus's existing in-game Settings/menu navigation, using its native appearance and controller pointer when technically possible. The earlier wrist-rune prototype has been retired. Browse your loaded mods, select one, and edit its settings through a consistent interface.

- **Any BepInEx mod:** ordinary \`Config.Bind(...)\` entries automatically get basic settings controls (toggle, slider for a declared range, dropdown for a declared value list, text input, reset).
- **Mods with custom pages:** optional declarative config-page layout, with nicer grouping and explanatory text.
- **Mods with an advanced provider:** optionally show live counters, custom previews and a **grab / rotate / scale** editor for a known HUD target.
- **Unloaded/disabled mods:** conservative editor for recognised BepInEx \`.cfg\` files; unsupported files remain read-only.
- **Safe controls:** Reset to Defaults, Recenter Menu, Reset HUD Layout, and clear notices when a restart is needed.

### Intended player flow

\`\`\`text
RebornManager → Select Instance → Install BepInEx
                                  └─ RebornMenu automatically installed (when released)
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

**Not yet proven in VR or released as a compiled v0.2.0 DLL.** Until we identify the precise native Settings tab container, it isn't guaranteed to appear as a top-level tab. The first page is an interaction test, not yet a full settings editor.

See [v0.2 native Mods tab testing and rollback](docs/NATIVE_MODS_V020_TEST.md).

## New test: Mods after Graphics (Community and Spades)

The two FourOfSpades extension tabs sit above the original Orbus Settings tabs. RebornMenu's first native UI test instead adds Mods as a **fifth original Settings tab**, after Graphics. On both clients this is the same insertion point and does not depend on Spades' custom upper row.

- Uses existing Orbus `settings-tab-control`, `settings-tab`, `compact option`, `menu-list`, headings and CSS assets.
- Appends to the verified native PowerUI DOM, without replacing existing tab elements or their click handlers.
- Narrowing the original four tabs to 104px, plus 8px spacing, lets all five fit the existing 570px tab header. If the runtime APIs cannot set those widths, Mods stays hidden to prevent overlap.
- Initially exposes **Ability Labels > Smaller / Larger**, changing the loaded `OrbusAbilityLabels.Plugin.LabelScale` (`[Labels] Scale`, 0.50x–2.00x) via its existing BepInEx `ConfigEntry<float>`. No Ability Labels source changes required.
- No full automatic mod settings browser or slider yet. Those come after the basic native tab/VR pointer test succeeds.
- Safely leaves original UI alone when the recognised Settings document or tab identifiers are absent; emits one diagnostic warning in `BepInEx/LogOutput.log`.

From any PowerShell folder, run `& "$env:USERPROFILE\OneDrive\Documents\OrbusRebornMenu\release.ps1"`, update RebornMenu in RebornManager, and open Settings in VR. Look for Mods immediately to the right of Graphics and test Larger/Smaller with Bard equipped. Send the `[RebornMenu]` lines from the log if missing or unresponsive.

To roll back without uninstalling, close the game and set `[NativeMods] EnableLiveModsTab = false` in `BepInEx/config/com.horizon.orbus.rebornmenu.cfg` then relaunch.

## Standalone mod first, automatic companion later

During VR testing, RebornMenu is a **normal optional client mod in RebornManager**. We'll publish a compiled DLL through [GitHub Releases](https://github.com/Hor1zonVR/OrbusRebornMenu/releases), and RM will install and update it like CastMetrics or QuestHelper.

Maintainers build/publish with `./release.ps1`: this automatically finds local BepInEx compilation references and **never installs into a particular instance**. Players simply open an instance in RM and choose **Client Mods → RebornMenu → Install**. Its built-in normal mod updater handles later stable releases.

Once the headset UI and generic configuration editor are reliable, we'll remove RebornMenu from the ordinary catalogue and integrate it with RM's BepInEx preparation and companion repair flow. **That automatic installation is not yet implemented.**

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
