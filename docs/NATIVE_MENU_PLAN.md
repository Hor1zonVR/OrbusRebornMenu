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

## October 20 in-headset findings — RebornCam v0.9.12 capture COMPLETED

Source: private two-snapshot report `OrbusMenu_20261020_153745_994.txt` (not committed because it is a user-game diagnostic).

- Captured 2026-10-20 15:37:39 (menu closed) and 15:37:45 (Settings open); scene stayed `BootstrapWeathermaker`, XR active.
- 9,716 → 9,778 captured objects; 62 new, 6 newly active, none newly inactive, one reparented/renamed. `truncated=True`, so this is not an exhaustive Unity world scan.
- New/activated `#RenderDeck-PowerUI` objects included a camera and several MeshFilter/MeshRenderer instances. `#PowerUI/Camera` was present. **Strong evidence the native menu's visible contents are PowerUI-rendered meshes**, not a normal collection of individual Unity UI buttons.
- `PlayerRigv4/Player Menu/TVScreenContent` was active on layer 19 `UI-Interactable` and has a `UnityEngine.BoxCollider`; the `Player Menu` root itself showed only `Transform` in this report. **Do not assume the root component is the native PlayerMenu controller.**
- Active native pointers:
  - `PlayerRigv4/Controller (right)/Grabber/UIPointer` → `TVScreenContent`, distance approximately 0.39 m.
  - `PlayerRigv4/Controller (left)/Grabber/UIPointer` → `TVScreenContent`, distance approximately 0.17 m.
  These are raycast hits on the collider, **not proof of actual DOM click dispatch, button handler or permission to inject content**.
- No individually named Settings tab/button hierarchy was identified in this report. The 68 relevant changes were mostly `New Game Object` PowerUI render meshes and `TVScreenContent` descendants.

### Focused follow-up implementation (awaiting VR report)

RebornCam **v0.9.13 source** introduces a temporary, opt-in read-only PowerUI observer (see [RebornCam PR #17](https://github.com/Hor1zonVR/OrbusRebornCamera/pull/17)):

- `Ctrl + Shift + P` with native menu closed arms a maximum 60-second capture.
- Open Settings, then press `Ctrl + Shift + P` again to save `OrbusPowerUI_*.txt` in `BepInEx/config/RebornCam/UIReports`.
- Uses either legacy `PowerUI.Element.set_innerHTML` or modern `Dom.Element.set_innerHTML` when present, and records a capped summary of recognised tab/menu element IDs, CSS class tokens, document types and method signatures. It does **not** change game DOM, reuse the withdrawn wrist panel, or save original HTML.
- **No Settings tab ID, click handler or working RebornMenu integration has been verified yet.** Wait for the report before adding any native Mods tab.

### Revised primary technical path

**Investigate the active PowerUI document and original menu navigation, not standalone Unity Canvas buttons.** A future Mods entry should ideally be an ordinary native HTML/PowerUI tab with the game's own input dispatch, parchment graphics, styles and controller beam.

Read-only evidence must come first:

1. Identify the live native menu owner and document from the installed client's generated IL2CPP type metadata, or use a verified read-only document observation. **Do not assume** the generic `PowerUI.Document/Element` model applies to Spades.
2. In Spades, previous QuestHelper tests observed newer `Dom.Element.set_innerHTML` and `Dom.Document` APIs. That evidence concerns the *Missions container*, **not** the exact Settings/Mods-tab ownership. Inspect actual Settings IDs/class names and safe surrounding structure without uploading private inventory HTML or registering arbitrary callbacks.
3. Observe the menu's native tab navigation and input event path. A `UIPointer` hitting the `TVScreenContent` box means the beam reaches the right surface; it does not establish how a DOM `onclick` handler fires.
4. Only after the document and relevant tab element are positively verified, prototype a **single reversible Mods entry** in a *test-only* RebornMenu build. Avoid reparenting native item/mission nodes and repeated DOM mutation: QuestHelper previously documented duplication/freeze after unstable DOM edits.
5. Confirm visually in VR with the existing game beam, navigation and close/back actions; then add read-only mod-list browsing, followed by generic BepInEx config editing. Test Community and Renewed independently.

**No additional closed/open menu snapshots required at this stage.** Next diagnostic should be narrow PowerUI/DOM metadata capture rather than another broad Unity object dump.

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
