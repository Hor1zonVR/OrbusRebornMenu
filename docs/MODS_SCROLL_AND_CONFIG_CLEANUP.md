# Native Mods settings: independent scrolling and config cleanup

## What changed

The working native Mods tab is unchanged (fifth original Settings tab,
after Graphics). Within the Mods page:

- **Left panel** (`rm-mod-list`): 148px wide, 390px high; `overflow-y:auto`.
- **Right panel** (`rm-mod-content`): 350px wide, 394px high;
  `overflow-y:auto`.
- **Mods page**: fixed 410px height, `overflow:hidden`.
- **Vanilla outer scroll parent** (`.largesettingsmenu-inner`): temporarily
  `overflowY=hidden` *only while Mods is active*, restored to `auto`
  on General, Locomotion, Audio, Graphics. These are the same PowerUI
  `ElementStyle` setters used by the game, not a Unity overlay.
- Labels like `ShowLocalAvatar` appear as `Show Local Avatar`.

Only the selected mod's settings are rendered on the right. The native
slider and BepInEx ConfigEntry integration are retained.

## Which settings show up in the VR menu?

RebornMenu now **hides** developer-oriented categories, including
`Diagnostics`, `Investigation`, `Debug`, `Bard Inspector`,
`ExperimentalNativeSettings`, and explicit old QuestHelper switches.
It also hides certain unsafe/self-disabling internal settings and
Ability Labels rendering offsets that are useful only for calibration.

**Hiding does not delete a .cfg value.** Those options remain in the
owning mod's configuration where appropriate, so developer tools,
captures and camera settings continue working. No diagnostic .cfg file
is deleted automatically. A mod with no player-facing settings remains
listed and explains why there are no controls.

Mod developers can hide a setting from the in-game menu without
depending on RebornMenu by adding a `"RebornMenu.Hide"` string tag
to its BepInEx `ConfigDescription`.

## Source-level retired settings

In separate optional mod releases:

- **OrbusAbilityLabels:** no longer binds the old `Bard Inspector`
  options or `Diagnostics` capture keys, and no longer registers the
  retired mesh-inspector and manual-capture MonoBehaviours. Regular
  Bard/Musketeer label rendering and the live Scale config are untouched.
- **OrbusQuestHelper:** no longer binds the unused
  `EnableRegionTaskGrouping`, `EnableHtmlRegionTaskGrouping`, and
  `SpadesMode` switches. The active `AutoOrganiseMissions`,
  `GroupingView`, and on-demand investigation tools remain.
- **RebornCamera / WeaponAdjustments:** debug options remain in their
  own .cfg files while those tools are in active development. RebornMenu
  hides them from the ordinary VR settings view.

BepInEx preserves orphaned keys from older versions on disk. Removing a
`Config.Bind` alone is not sufficient to delete those old file entries.

## Optional one-time cleanup of obsolete on-disk keys

**Close Orbus first** and update Ability Labels + QuestHelper to their
newer releases; otherwise they may re-create the retired keys.

From any PowerShell directory, use:

```powershell
$rm = "$env:USERPROFILE\OneDrive\Documents\OrbusRebornMenu"
& "$rm\tools\clean-obsolete-configs.ps1"
```

This is **preview only**, listing the exact known keys it would remove
from compatible `BepInEx/config` files under your modded instances.

After reviewing the preview, run:

```powershell
& "$rm\tools\clean-obsolete-configs.ps1" -Apply
```

The tool refuses to apply changes while `OrbusVR` is running, makes a
per-file timestamped `.bak` copy, and removes only explicitly retired
Ability Labels / QuestHelper keys. It does not alter active camera,
weapon diagnostics, player presets, or unknown config keys. Restore
backups if needed.

If an instance is stored outside the normal
`%LOCALAPPDATA%\OrbusRebornModdingInstances` folder, pass
`-Root "C:\path\to\instances"`.

## Test checklist

1. Publish updated RebornMenu, Ability Labels, QuestHelper via their own
   existing `release.ps1` scripts, then refresh the three mods in
   RebornManager (no need to update RebornCamera or WeaponAdjustments).
2. Open Orbus Settings > Mods. The outer page should not scroll; the
   right settings panel should scroll by itself, and the left list
   should scroll only if it contains more mods than its height.
3. Switch to General or Graphics; normal Orbus Settings scrolling
   should return.
4. Confirm Ability Labels' Scale control still changes label size.
5. Confirm QuestHelper still groups missions by region/activity and
   RebornCamera continues working.
6. Optionally run preview and backup-first cleanup on the now-unused
   config keys.

This is source-ready, **not in-game verified**. If nested scrollbars
still appear or the Orbus CSS setter is unavailable in your client,
share a new screenshot and the `[RebornMenu]` lines.
