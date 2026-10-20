# RebornMenu implementation order and quality gates

**Project priority:** Finish the universal in-VR configuration base before progressing new QuestHelper, CastMetrics, GoblinTimer or other HUD features.

## Phase 0 — Architecture and SDK contract (in progress)

- [x] Canonical product/architecture specification
- [x] Developer integration contract and draft \`rebornmenu.json\` schema
- [x] Example using the actual CastMetrics BepInEx section/key names
- [x] RebornManager companion installation rules
- [ ] Verify BepInEx plugin enumeration and config write APIs on actual installed IL2CPP runtime
- [ ] Compare Unity UI modules, input modules and XR hooks on Community and Renewed

## Phase 1 — Minimal VR runtime proof

- [ ] \`OrbusRebornMenu\` BepInEx 6 IL2CPP plugin project builds against \`GameDir\`
- [ ] Logs clean load on both game editions without modifying core Orbus UI
- [x] Choose default opening interaction and native parchment look ([VR UX spec](VR_UX.md))
- [ ] World-space panel can appear and disappear safely in real VR
- [ ] Independent wrist-open and desktop fallback input; no collisions with RebornCam keys
- [ ] VR laser/pointer focus + trigger, clear buttons and consistent scrolling
- [ ] Recovery/recenter method if menu leaves view

**Gate:** real screenshots/video showing the panel and working controls in both clients.

## Phase 2 — Universal config pages

- [ ] Enumerate loaded plugin GUIDs and typed \`ConfigEntry\`s
- [ ] Toggle, bounded slider, numeric field, select, string/VR keyboard, read-only fallback
- [ ] Config sections, descriptions, defaults and reset actions
- [ ] SettingChanged/save works; explicit restart warning for non-live third-party settings
- [ ] File-only conservative parser for unloaded plugins; atomic backups
- [ ] Unsupported/malformed \`.cfg\` never modified
- [ ] Error isolation if another plugin or provider throws

**Gate:** CastMetrics' existing entries appear without a special SDK reference, and persist after restart.

## Phase 3 — Optional enhanced configuration and placement

- [ ] Manifest validation and tab ordering
- [ ] Freeze a *versioned* optional runtime provider bridge after IL2CPP testing
- [ ] Generic HUD placement adapter with grab, rotate, size, preview, confirm/cancel/reset
- [ ] Wand/wrist/head/world anchor adapters only as proven safe
- [ ] Recenter/failsafe reset even when HUD is moved out of sight
- [ ] No unnecessary XR rig modifications

**Gate:** CastMetrics wand HUD can be moved, scaled and reset live without affecting spell casting; works even when menu later removed.

## Phase 4 — RebornManager integration

- [ ] Publish tested DLL release and SHA-256
- [ ] BepInEx preparation automatically installs compatible RebornMenu after loader success
- [ ] Install/Repair/Disable/Update status per instance
- [ ] Companion download failure leaves loader fully functional
- [ ] In-place game update and rollback preserve plugin and settings
- [ ] First-time onboarding explains how to open/recenter menu in VR

**Gate:** clean managed Community and Renewed installations work, including failure/repair tests.

## Phase 5 — Adopt across mods

- [ ] QuestHelper custom sections, helpers/alerts
- [ ] GoblinTimer countdown/settings/placement
- [ ] RebornCam camera-specific settings and safe previews
- [ ] Document simple third-party mod integration with zero SDK requirement
- [ ] Public examples and compatibility matrix

**Gate:** no project needs to create its own generic VR config UI. Mod remains usable when RebornMenu is disabled.

## Non-negotiable regression checks

- Both Community and Renewed, scene/world transitions, controller disconnect, menu closed during combat, high/low framerate, repeated show/hide.
- Mod toggles/editing do not bypass normal server gameplay rules.
- A broken config value or custom page never crashes the whole game.
- Config secret fields masked and logs redacted.
- Recovery always possible without headset interaction from Windows (edit config/remove RebornMenu DLL in RM).
