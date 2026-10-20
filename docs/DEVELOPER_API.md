# RebornMenu developer contract

> **Design contract v0 — no compiled SDK has been released yet.** The manifest is an initial schema proposal. The in-game plugin must be built and tested before any runtime provider methods become a stable API.

**Quickest way to support RebornMenu:** use regular BepInEx 6 IL2CPP \`Config.Bind(section, key, default, description)\` entries. That's it. When RebornMenu is installed and working, it will discover registered entries and show a generic config page. **Your mod must continue working without RebornMenu.**

## 1. Universal fallback

- RebornMenu discovers **currently loaded BepInEx plugin instances**, identified by the BepInEx plugin GUID, and enumerates their declared config entries.
- Configuration sections become page groups; keys become controls. The editor uses BepInEx's typed value, default, description and \`AcceptableValueRange\` / \`AcceptableValueList\` to select and validate widgets.
- Saving a value must use the owning BepInEx \`ConfigFile\`; loaded entries may notify via \`SettingChanged\`. The mod determines whether it applies live.
- A disabled/not-loaded mod may have files in \`BepInEx/config\`; these get a separate conservative **file-only** page with a restart notice. Unsupported formats stay read-only, and only files physically within the selected game instance can be edited.
- Do not invent numeric ranges or automatically turn any arbitrary \`.cfg\` into a safe editor.

**C# example — works with or without RebornMenu**

\`\`\`csharp
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;

[BepInPlugin("com.example.mymod", "MyMod", "1.0.0")]
public sealed class Plugin : BasePlugin
{
    private ConfigEntry<bool> _visible = null!;
    private ConfigEntry<float> _scale = null!;

    public override void Load()
    {
        _visible = Config.Bind("Display", "Visible", true,
            "Show the HUD above the wand.");

        _scale = Config.Bind("Display", "Scale", 1f,
            new ConfigDescription("HUD size.",
                new AcceptableValueRange<float>(0.25f, 3f)));

        // Your regular plugin remains fully functional.
        // RebornMenu is a separate, optional companion.
    }
}
\`\`\`

For values that should apply immediately, the mod can implement its own \`SettingChanged\` handler (and should safely unsubscribe on unload where supported). RebornMenu must not claim every mod reacts without a restart.

## 2. Optional declarative custom layout

Put \`rebornmenu.json\` alongside your mod's DLL if you want a more polished layout with explicit tabs, ordering, labels and descriptions. See [CastMetrics example](../examples/CastMetrics/rebornmenu.json) and [JSON schema](../schemas/rebornmenu.schema.json).

The manifest is **metadata only**, never executable code. It must not contain external images/URLs that RebornMenu automatically downloads, raw Unity scene object names, file paths to edit, assembly/type/method invocation requests or C# scripts.

Minimum form:

\`\`\`json
{
  "schemaVersion": 1,
  "pluginGuid": "com.example.mymod",
  "displayName": "My Mod",
  "tabs": [
    {
      "id": "display",
      "title": "Display",
      "controls": [
        {
          "id": "show",
          "kind": "toggle",
          "binding": { "section": "Display", "key": "Visible" }
        }
      ]
    }
  ]
}
\`\`\`

**Manifest semantics**

| Property | Purpose |
| --- | --- |
| \`schemaVersion\` | Versioned declarative contract, currently proposal \`1\` |
| \`pluginGuid\` | Exact BepInEx GUID; prevents a manifest from claiming a different mod's config |
| \`displayName\`, \`summary\` | Human-readable display text; fallback to plugin info when omitted |
| \`tabs[]\` | Presentation grouping and order; no additional settings storage |
| \`controls[].binding\` | BepInEx \`section\` + \`key\` belonging to the **same** plugin GUID |
| \`controls[].kind\` | \`toggle\`, \`slider\`, \`number\`, \`select\`, \`text\`, \`readout\` |
| \`controls[].title\`, \`description\` | Optional polish; fall back to standard BepInEx metadata |
| \`placement\` | Optional declarative config bindings for offsets/scale; never automatically hijacks Unity objects |

**Validation and fallback**

- Unknown major schema versions → ignore manifest and expose normal BepInEx settings.
- Unknown binding or mismatched type/range → show it as unavailable; **do not write a new phantom entry**.
- Duplicate plugin GUIDs → warn and do not let one plugin edit another plugin's values.
- A manifest cannot opt out of showing ordinary settings. Any untouched entries appear under **Other settings**.
- Manifests cannot enable disabled plugins, install DLLs, perform networking, execute commands or add arbitrary native game hooks.
- Only read the manifest from the owning plugin's installed directory; do not follow links outside the instance.

## 3. Enhanced runtime providers (planned, NOT yet published)

Certain features can't be done with a simple manifest: HUD preview rendering, live counters, controller-driven placement or safe mod-specific actions. These require a runtime bridge.

**Proposed rules:**
1. A **small versioned optional bridge assembly** will expose a registration method after IL2CPP loading has been verified. We'll freeze its concrete C# types and package version after the first VR proof-of-concept.
2. The author can register page metadata and callbacks only for their **own** BepInEx GUID. Registration reports failure if the companion is absent or incompatible.
3. Controls receive validated updates from standard config entries. Runtime providers may expose **read-only live data** and safe, explicit actions.
4. Provider lifetime is tied to its loaded plugin and the current scene. The menu must not keep a dangling delegate after scene unload or plugin failure.
5. If registration fails, all regular settings still work. No gameplay feature may require RebornMenu to be loaded.
6. RebornMenu never invents a mapping from arbitrary \`.cfg\` values to a Unity GameObject. An author must provide a safe, explicit placement target.

**Don't add an imaginary \`using RebornMenu.SDK\` dependency to mods yet.** This contract is intentionally conceptual until the real bridge exists.

## 4. Shared HUD placement contract

A mod wanting **grab-to-move, rotation, scale and a preview** exposes a placement target through the eventual runtime provider. RebornMenu handles user input and visual handles; the mod owns the HUD GameObject and persistence bindings.

Typical configuration sections:

\`\`\`text
[Placement]
Anchor = Wand
RightMeters = 0
UpMeters = 0.13
ForwardMeters = 0
YawDegrees = 0
Scale = 1
\`\`\`

The same values must remain editable even without the VR placement provider (using standard settings). The provider commits them through the owning config entries so there is only **one authoritative saved position**.

**Placement sequence:** Open mod → Edit position → temporarily collapse panel → show target outline/handle → grab and move/scale → Preview → Confirm / Cancel / Reset → save. If tracking is lost or the scene changes, cancel the edit and release input capture.

This feature is **not automatic for arbitrary third-party HUDs**. Mod authors who do nothing get ordinary position/scale sliders if they exposed those config keys; they don't magically get draggable game objects.

## 5. How each of our planned mods uses this

| Plugin | Vanilla BepInEx editor | Enhanced page / shared placement |
| --- | --- | --- |
| **CastMetrics** | Show casts/s, rolling window, idle time, scale, wand offsets | Live casts/s preview, wand anchor, physical grab/scale |
| **QuestHelper (QH)** | Quest grouping, auto-organise and Spades compatibility mode | Mission-specific pages, notifications, moveable optional helper HUD |
| **GoblinTimer** | Alerts, sounds, notification toggles, visibility | Countdown preview, world/wrist anchor, positioning |
| **RebornCam** | FOV, camera modes, render distance, keyframe settings | Camera-specific buttons/live readouts only where safely supported |

Neither QuestHelper nor GoblinTimer should be changed until RebornMenu is independently functional and the shared provider model is proven.

## 6. Compatibility test plan

- Unity 2018.4.30 PCVR, Community Edition and Orbus Renewed.
- BepInEx v6 IL2CPP and game-generated interop (same instance being tested).
- \`bool\`, enum, bounded float, unconstrained numeric, text, invalid values, read-only fallback.
- Save → launch → value persists. Restore Default and Reset All.
- RebornMenu absent/disabled → target mods still load and behave normally.
- Scene/world transition while UI or placement edit is active.
- Hand-tracking loss/controller disconnect, duplicate input, VR and desktop.
- Offscreen HUD or oversized panel → Reset HUD layout and Recenter Menu still reachable.
- Mod update and game update with config preserved.

See [Architecture](ARCHITECTURE.md) and [RM integration requirements](RM_INTEGRATION.md).
