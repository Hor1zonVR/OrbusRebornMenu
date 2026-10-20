# OrbusRebornMenu

**One universal in-game VR mod configuration menu for OrbusVR Community Edition and Orbus Renewed.**

**Status: design and developer contract.** This repository now contains the architecture and integration foundation, but **there is no working VR plugin or installer release yet**. Don't install it as a BepInEx plugin until a tested compiled release is available.

## The idea

**RebornManager** will eventually install RebornMenu as a **core companion** whenever it prepares an OrbusVR instance with BepInEx. Players won't have to search the mod library for a separate menu plugin. **RebornManager and other mods must continue working without it.**

In VR, open a wrist-accessible **Mods** menu. Browse your loaded mods, select one, and edit its settings through a consistent interface.

- **Any BepInEx mod:** ordinary \`Config.Bind(...)\` entries automatically get basic settings controls (toggle, slider for a declared range, dropdown for a declared value list, text input, reset).
- **Mods with custom pages:** optional declarative config-page layout, with nicer grouping and explanatory text.
- **Mods with an advanced provider:** optionally show live counters, custom previews and a **grab / rotate / scale** editor for a known HUD target.
- **Unloaded/disabled mods:** conservative editor for recognised BepInEx \`.cfg\` files; unsupported files remain read-only.
- **Safe controls:** Reset to Defaults, Recenter Menu, Reset HUD Layout, and clear notices when a restart is needed.

### Intended player flow

\`\`\`text
RebornManager → Select Instance → Install BepInEx
                                  └─ RebornMenu automatically installed (when released)
                                       └─ Start OrbusVR → Open Mods on wrist
                                            ├─ CastMetrics
                                            ├─ QuestHelper
                                            ├─ RebornCam
                                            ├─ GoblinTimer
                                            └─ Any other mod with BepInEx configs
\`\`\`

A third-party author **does not have to know RebornMenu exists** for their normal BepInEx config to be discoverable.

## Documentation

- [Architecture and VR interaction specification](docs/ARCHITECTURE.md)
- [Wrist rune opener and native-feeling parchment UI design](docs/VR_UX.md)
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
