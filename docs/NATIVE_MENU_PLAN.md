# RebornMenu — native Orbus Settings integration (current direction)

**Status: investigation and design. Not implemented yet.**

## Decision

The original v0.1/v0.1.1 wrist-rune + manually constructed floating parchment proved uncomfortable: the menu was hard to read, text appeared detached, and the experiment drew a cursor without Orbus's normal controller beam. We are **retiring this opener and renderer** for future releases.

The new goal is a **Mods entry in Orbus's existing in-game Settings/menu navigation**, using the original UI framing and existing VR pointer pipeline where safely possible. No independent wrist rune or special floating menu by default.

## What we know from the first read-only UI survey

- Unity UI is present: 14 active canvases, 854 selectables, 8 GraphicRaycasters at time of capture.
- Many real world-space UI objects and buttons use the UI-Interactable layer with colliders.
- The first broad report was dominated by wardrobe/station objects. It was not a targeted capture of the Settings menu.
- The native player journal/inventory may also use PowerUI, which does **not** necessarily expose individual rendered menu controls as Unity Canvas children.
- None of these findings prove a safe way to insert a working native Mods tab yet.

## Next data collection — RebornCam v0.9.12

RebornCam now contains a read-only **NativeMenuDiffInspector**.

1. With the actual Orbus menu CLOSED, press **Ctrl + Shift + I** once (baseline).
2. Open the normal inventory/menu and its **Settings** tab.
3. With Settings OPEN, press **Ctrl + Shift + I** again.
4. Send one resulting `BepInEx/config/RebornCam/UIReports/OrbusMenu_*.txt`.

The report compares active/new UI objects, prioritises player Settings candidates, and includes selected native component names and pointer intersections. No need for the older separate OrbusVRInput file. If the UI is PowerUI document-based, that is the next investigation.

## Conditional technical path

### If Settings uses an actual Unity UI Canvas/Selectable hierarchy

Test a **single separate, reversible 'Mods' navigation entry** attached to the verified menu tab container. Reuse appropriate existing material/font/visual assets at runtime (not redistributed asset files), and the game's **own** Canvas + raycaster + pointer events.

Do not clone game callback delegates, replace the game's Settings panel, or move other existing button objects. The test must not affect opening/closing inventory, or normal item interaction.

### If Settings uses PowerUI (or a mixed system)

Inspect only **the verified loaded document and API metadata** first. Some Community/Renewed client builds use different document types (for example PowerUI.Document vs PowerUI.HtmlDocument). Do not assume native DOM insertion is possible based on a Unity canvas survey. If safe insertion cannot be demonstrated, use a small, independent page linked from a verified native control, built from validated available game UI primitives rather than raw free-floating TextMeshes.

If no safe native integration exists, expose that limitation rather than silently returning to an untested wrist opener.

## Acceptance criteria

- An ordinary Orbus user can find **Mods** from existing in-game Settings/navigation.
- Menus have the actual parchment/brown tabs/font feel shown in player screenshots.
- Native VR laser remains visible and controls selectable (no independent beam or input hijack).
- Mod list and generic config editing work as separate features after menu integration.
- Both Community and Renewed are tested independently.
- The entire integration can be disabled/removed without corrupting the original UI, gameplay controls, or saved game.
- RebornMenu continues as an **optional RM-installed client mod** during testing; automatic BepInEx companion comes only after it is stable.
