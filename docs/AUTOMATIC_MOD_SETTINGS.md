# RebornMenu: automatic native mod settings (experimental)

RebornMenu's Mods tab lives immediately after Graphics in the original
Orbus Settings row. The two custom FourOfSpades tabs remain unchanged.

## What the new Mods interface does

- A scrollable native **mod list** on the left; the selected mod's
  settings appear on the right.
- Discovers **live BepInEx ConfigEntry** objects when Mods is opened.
- Groups settings under their original BepInEx config sections.
- Displays **bool** settings as native toggle buttons.
- Displays **enum** settings as native option lists (first 12 options).
- Displays **int / float / double** settings with BepInEx
  `AcceptableValueRange<T>` bounds as Orbus-style
  `rangeSlider` / `rangeSelector` controls.
- Writes directly to the original `ConfigEntryBase.BoxedValue`,
  respecting the owning mod's AcceptableValueRange and normal BepInEx
  save-on-change semantics. **No cloned .cfg readers.**
- Free-form strings, unsupported types, and unbounded numerics are
  deliberately NOT shown. Config strings may be API keys or secrets.

This is an experimental first implementation and needs an in-game test.
The slider presents 31 clickable positions, including both endpoints;
it also attempts hover-with-trigger-held movement where supported by
the actual client's native PowerUI event dispatch. This does not claim
fully smooth dragging on every client until verified in VR.

For UI safety, the first test caps rendering to 35 entries per selected
mod and 12 enum choices per setting. Mods with no supported settings
do not appear in the list. More sophisticated filtering, opt-in display
metadata, search, continuous drag, value formatters, and refresh controls
will be added after the native layout and pointer model is verified.

## Mod authors: no RebornMenu dependency required

Ordinary BepInEx config entries are enough. For example:

```csharp
using BepInEx.Configuration;

var size = Config.Bind("Visuals", "LabelSize", 1.0f,
    new ConfigDescription(
        "Scale the text above abilities",
        new AcceptableValueRange<float>(0.5f, 2.0f)));

var enabled = Config.Bind("Visuals", "Enabled", true,
    "Show labels above abilities");
```

If the plugin is loaded normally through BepInEx, RebornMenu discovers
the live config entries via its chainloader plugin registry and/or the
plugin class's static config fields. It does **not** need a special
RebornMenu build or compile-time reference.

For a mod author who wants a particularly nice page, organise keys
into logical `Config.Bind` sections and provide meaningful
`ConfigDescription` text and `AcceptableValueRange<T>` constraints.

## Testing

1. Run `& "$env:USERPROFILE\OneDrive\Documents\OrbusRebornMenu\release.ps1"`
   after syncing the GitHub source.
2. Update RebornMenu in RebornManager; keep Ability Labels installed.
3. Launch the game, open **Settings → Mods**.
4. Select **Ability Labels** in the left navigation.
5. Click several points along the **Scale** slider. The displayed value
   should change without restarting. Equip Bard or Musketeer to confirm
   the labels' visible size follows it.
6. Switch to another discovered mod and test a non-sensitive toggle.
7. Confirm the original General/Locomotion/Audio/Graphics and the
   FourOfSpades Classes/Analyzer tabs still operate.

**Please share the exact `[RebornMenu]` log lines or a screenshot if a
control is absent or unresponsive.** No large scene diagnostic is needed.

## Friendly labels and short slider hints (no RebornMenu dependency)

Mods can optionally attach human-readable labels and help text using
ordinary BepInEx `ConfigDescription.Tags`. RebornMenu will continue to
discover entries that don't use these tags; they're purely optional.

```csharp
var height = Config.Bind("Labels", "AboveOrbMetres", 0.072f,
    new ConfigDescription(
        "How far above the orb the label should appear.",
        new AcceptableValueRange<float>(-0.03f, 0.18f),
        "RebornMenu.Label=Vertical Position (m)",
        "RebornMenu.Hint=Higher (+) or lower (-) relative to the orb"));
```

Ability Labels now exposes three live sliders: Label Size (0.50–2.00x),
Vertical Position (-0.03–0.18m, default 0.072m), and
Forward / Backward (-0.05–0.10m, default 0.016m). Positive depth brings
the labels toward the headset; negative depth moves them away. The
existing values are preserved when upgrading, and the render loop reads
them on every frame.

## Player-facing settings versus developer diagnostics

RebornMenu hides common internal-only sections in the VR menu:
`Diagnostics`, `Investigation`, `Debug`, `Bard Inspector`,
`ExperimentalNativeSettings`, and known deprecated compatibility
switches. Those values are **not deleted** from the owning mod's config.

If your plugin has a single player-facing setting in an internal section,
put that setting into a normal section such as `General`, `Visuals`, or
`Gameplay`. To explicitly hide a supported `Config.Bind` entry without
requiring any RebornMenu dependency, use a `ConfigDescription` string tag:

```csharp
var developerCapture = Config.Bind(
    "General", "EnableCapture", false,
    new ConfigDescription(
        "Development-only capture",
        null,
        "RebornMenu.Hide"));
```

For cleaning up **obsolete keys already on disk**, see
[Scroll and obsolete-config cleanup](MODS_SCROLL_AND_CONFIG_CLEANUP.md).
That operation is separate and opt-in, with backups.

## Safety and compatibility

Only adds or refreshes elements owned by RebornMenu. It never rewrites
vanilla Settings pages, original button event handlers, or Spades' tabs.
If the original Settings DOM is not recognised, RebornMenu skips
insertion. Enable/disable via `[NativeMods] EnableLiveModsTab`, then
restart if needed.

The automatic UI is a convenience layer, not a permission boundary:
it invokes the mod's **existing** live configuration. Mod authors should
never expose sensitive config values as numeric/bool/enums if those
values should not be shown in VR.

## Generic custom mod pages (v0.2.12 and later)

**RebornMenu is a reusable host, not a registry of known mods.** Any installed
BepInEx mod can supply a full right-side Mods page without updating or
referencing RebornMenu. The regular Config.Bind controls remain the default.

### Provider contract — version 1

Put a **public** provider class in the **same managed assembly** as your
BepInEx plugin class. It must expose BOTH of these public members:

1. A compile-time constant `public const string RebornMenuModGuid` matching
   the plugin's exact `[BepInPlugin(GUID, ...)]` GUID.
2. `public static bool RenderInRebornMenu(object document, object content)`.

Return `true` only when your custom UI has successfully rendered. Return
`false` if it is unavailable; RebornMenu will show the ordinary Config.Bind
settings for that mod instead. No extra JSON registration, shared API DLL,
RebornMenu source edit, or other plugin dependency is necessary.

```csharp
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;

[BepInPlugin("com.example.orbus.testmod", "TestMod", "1.0.0")]
public sealed class MyModPlugin : BasePlugin
{
    public override void Load() { /* initialise your mod */ }
}

public static class MyCustomSettingsPage
{
    public const string RebornMenuModGuid = "com.example.orbus.testmod";

    public static bool RenderInRebornMenu(object document, object content)
    {
        if (document == null || content == null) return false;
        var set = content.GetType().GetMethod("set_innerHTML",
            BindingFlags.Instance | BindingFlags.Public, null,
            new[] { typeof(string) }, null);
        if (set == null) return false;
        set.Invoke(content, new object[] {
            "<h4>My Custom Mod</h4><p>Page supplied by my mod.</p>"
        });
        return true;
    }
}
```

For buttons, implement your own PowerUI event-handler bindings (such as
the existing native `Dom.Event` / `addEventListener` delegate bridge),
state refresh, and input/VR keyboard behaviour. RebornMenu supplies
only the **existing Mods tab, left-hand mod navigation, and right-hand
content DOM element**. The content area is approximately 350px wide;
use the existing Orbus `menu-list` and `compact option` styles.

### Discovery and isolation

- RebornMenu discovers installed BepInEx `[BepInPlugin]` classes as usual.
- It examines **only that plugin's own assembly** for a public provider
  with the exact constant GUID and required method signature.
- It neither probes every Unity/IL2CPP assembly for page renderers nor
  hardcodes any plugin names, including WeaponAdjustments.
- Selecting the mod invokes its provider; selecting another returns to
  that mod's normal or custom page.
- If the provider is missing, returns false, or throws, RebornMenu
  falls back to the mod's normal config controls without breaking
  the other mod pages.
- The mod owns its save data, validation, page state, event handlers
  and callbacks. RebornMenu does not read private mod data.
- The GUID must be unique to each BepInEx plugin. When multiple plugins
  share an assembly, each provider must declare the intended GUID.

### Current built-in example

OrbusWeaponAdjustments v0.2.3+ opts in with
`WeaponPresetUi.RebornMenuModGuid` and
`WeaponPresetUi.RenderInRebornMenu(document, content)`.
The same mechanism is available immediately to all future Orbus mods.
No future RebornMenu updates are required **for adding new pages using
this v1 contract**. A future extension of the contract itself may
require a RebornMenu version update.
