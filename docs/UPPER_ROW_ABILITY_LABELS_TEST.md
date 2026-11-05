# RebornMenu: Spades upper-row tabs and Ability Labels proof-of-concept

## Status (2026-11-05)

**Diagnostic build only. No Mods tab is inserted yet.** The original PlayerMenu TextAsset has four Settings controls (General / Locomotion / Audio / Graphics), while the live FourOfSpades client adds two more tabs *above* that row. There is no confirmed safe DOM insertion target for that upper row yet.

We will put **Mods third in Spades' upper row**, reusing actual Orbus UI classes, backgrounds, fonts, layout and normal VR laser — not drawing a Unity canvas or an imitation parchment. The existing Spades tabs and lower four vanilla Settings tabs must not move or overlap.

Because this has not been identified at runtime, the old generic Settings-fragment injection is **hard-disabled**, regardless of stale `EnableModsTab=true` config values.

## Ability Labels is ready for the first real settings control

The independent `AbilityLabelsSettings` bridge resolves the loaded `OrbusAbilityLabels.Plugin.LabelScale` field, which is a `ConfigEntry<float>`.

- Read existing [Labels] Scale, default 1.00.
- Valid range 0.50 to 2.00.
- Write to the **existing live ConfigEntry**, not a duplicate config-file reader.
- Saving the value uses the mod's own BepInEx config; AbilityLabelVisuals reads it during LateUpdate.
- When Ability Labels is absent, the bridge returns unavailable and changes nothing.

The code currently only checks the bridge during the diagnostic and logs whether it is available. No UI control is wired yet.

## One headset test to locate the actual upper row

1. From any PowerShell directory: `& "$env:USERPROFILE\OneDrive\Documents\OrbusRebornMenu\release.ps1"`
2. Refresh RebornManager Client Mods, update RebornMenu, and ensure Ability Labels is installed.
3. Start the FourOfSpades/Renewed instance.
4. Open the in-game **Settings** menu. The two extra Spades tabs should be visible above General and Locomotion.
5. On the PC keyboard while Orbus is focused, press **Ctrl+Shift+F7** once.
6. Exit normally and share just the `[RebornMenu/Probe]` lines from `BepInEx/LogOutput.log`.

Expected log lines:
- `PlayerMenu direct document`
- `LIVE settings-tab-controls` plus existing lower tab positions
- `Upper-row candidate` and `Upper-row neighbourhood` (the Spades extra-row search)
- `Ability Labels config bridge READY` (if Ability Labels is loaded)

If upper-row candidates are empty or no player document is found, do **not** guess an injection position. We must refine this narrow diagnostic based on actual output.

## Implementation after upper-row verification

- Insert a single Mods control **after** the two known Spades tabs, without reparenting or editing their nodes.
- Reuse native tab/selected classes and game event dispatch.
- Add a native settings page using original headings and control rows.
- First interactive control: Ability Labels / [Labels] Scale.
- VR test: change scale and see Bard/Musketeer labels resize without restarting.
- Only after verified, discover other loaded BepInEx configs and render safe generic controls.

No changes to Ability Labels are needed for this first test.
