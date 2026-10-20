# RebornMenu: wrist opener and native-feeling parchment UX

**Status:** design decision for first playable prototype. No visual or input interaction is claimed to be implemented yet.

## Default entry point: discreet rune beneath the left wrist

Our recommended initial experience is a **small magical rune on a left-wrist ornament**, not a permanent floating button or an intrusive addition to Orbus's native journal.

1. Raise/turn the underside of the left wrist into a comfortable view. A subtle rune becomes visible/readable.
2. Aim at its small, deliberately forgiving interaction area with the opposite controller's existing pointer and press trigger.
3. Spawn a legible **world-space parchment panel** ahead of the player, oriented for reading. Do not continually head-lock it while the user moves.
4. Use the other hand's pointer + trigger to select **Mods → Installed mod → Settings**.
5. Close from the parchment's top-right corner or interact with the wrist rune again. Re-opening can recenter it if needed.

The rune should appear only when tracking is valid and the player's wrist is deliberately facing their view. The initial panel must never obscure vision or prevent combat/movement while closed.

**Wrist is an option, not an imposition:** allow left/right wrist, visibility, activation gesture and menu scale/position to be changed. Provide a separate recoverable keyboard shortcut during PCVR development (candidate **Ctrl+Alt+M**, subject to conflict testing) and a **Recenter** action. Never require another mod to be present just to open the menu.

### Why not a tab inside the game's native menu first?

Orbus uses a physical journal/compass interaction, and the game's native mission/journal UI differs between Community and Renewed. QuestHelper investigations recorded different `PowerUI.Document` and `PowerUI.HtmlDocument` implementations, including fragile DOM reparenting.

RebornMenu's v0.1 opener must be **independent of the game's PlayerMenu/PowerUI documents** to avoid affecting quests, inventory, combat or journal navigation. We can consider a supplemental journal shortcut *after* the self-contained menu is stable and independently tested on both game editions. Do not replace the wrist/recenter fallback even if journal integration is added later.

## How to keep it visually authentic

RebornMenu should feel like another Orbus journal page—not a desktop settings window projected into VR.

**Parchment visual language**
- Warm, slightly aged, lightly textured parchment with uneven edges; avoid blinding white or neon UI panels.
- Dark brown/ink headings, legible dark text, restrained carved or hand-drawn rune ornamentation.
- A thin wooden/leather/metal-bound edge inspired by Orbus book/journal details; no oversized generic fantasy borders.
- In-world-sized, touch-friendly buttons that look like paper tabs, stamped seals or simple ink controls.
- Sliders: inked track with a bronze/wooden handle; toggles: small stamped checkmarks; selections: tabbed parchment chips.
- Gentle open/close animation (short unrolling/page reveal or paper flourish) and optional quiet parchment sound; no enormous particle effects.
- Section labels and icons should be true to Orbus-style typography, not a fake handwriting font that is hard to read in VR.
- Provide a high-contrast setting and large-text mode without departing entirely from the parchment appearance.

**Asset fidelity and compatibility**
- **First choice:** identify the original game's UI textures, font rendering and border/ornament assets *in the installed client's live Unity resources*, if available and permitted. Reference them at runtime where safe; do not modify the game's original UI assets.
- **Fallback:** bundle our own purpose-made, visually consistent parchment textures and border art. Match against screenshots of actual game menus provided by the user, not generic medieval reference art.
- Do not hardcode a single material or native PowerUI API across Community/Renewed; verify asset availability and gracefully fall back.
- Any redistributed artwork must be original or used with permission/licence.
- The panel is a world-space Unity object, not a screen-space IMGUI window; desktop debug UI is only a testing fallback.

## Layout of the parchment

```text
┌──────────────────── The Mod Ledger ────────────────────┐
│  ‹ Back                         MODS        Close  ✕  │
│                                                        │
│  Your installed enchantments                            │
│                                                        │
│  ✦ CastMetrics                 Settings  ›              │
│  ✦ QuestHelper                 Settings  ›              │
│  ✦ RebornCam                   Settings  ›              │
│  ✦ GoblinTimer                 Settings  ›              │
│                                                        │
│  -----------------------------------------------       │
│  Recenter   •   Text size   •   Reset layout            │
└────────────────────────────────────────────────────────┘
```

Avoid overwhelming pages with every configuration entry at once: group by mod and by existing BepInEx sections; make settings controls large enough to select without precision aiming. Scrolling must be consistent with Orbus pointer interaction.

### Placement and interaction rules

- On first open, place it a comfortable distance from the headset and near eye level; make the distance adjustable. It should remain in world space after opening, not cover the player's view whenever they look around.
- Allow grabbing the parchment's border/handle to reposition it, and a simple scale control; save per instance.
- Do not assume finger tracking or physics pinching on every device. **Controller pointer + trigger is the baseline.** The existing right-hand controller transform/UI pointer can be discovered without patching or replacing Orbus's native UI events.
- Consume button input **only while the menu's own interactive targets are being operated**, with a small safety hold or debounce when opening; never globally intercept the trigger while a player casts spells.
- Dismiss or safely hide the menu on scene/world transitions, tracking loss or if a hand disappears. The next open should work regardless.
- Keep the wrist button out of the journal compass interaction area (left shoulder) and away from equipped weapon grabs. A separate button hitbox should be inert if palm orientation doesn't allow intentional activation.
- A keyboard recovery shortcut can open and recenter the parchment even when the wrist target is invisible. Provide a reset action in the RebornManager desktop UI later.
- Opening or closing RebornMenu must not pause the game or move the XR rig.

## Practical test order

**Spike A — wrist detection and opener:** record left/right XR hand pose, confirm world-space projection on Community and Renewed, ensure rune attaches to the actual hand rather than the desktop camera. Log only non-sensitive diagnostics; show a simple placeholder mark at first.

**Spike B — parchment object:** add the panel with placeholder textures, correctly sized readable text and a tracked close button. Confirm it appears in the VR headset with no interaction conflicts.

**Spike C — pointing:** aim with the existing Orbus controller pointer or a self-contained raycast adapter, test hover + trigger and repeated open/close.

**Spike D — authentic material pass:** capture permitted UI visual references (parchment, ink, wood edging, text), build/ship custom assets if originals are not safe/accessible, inspect both game editions.

**Spike E — generic BepInEx settings:** use CastMetrics and QuestHelper config sections to prove toggles, sliders and persistence; no optional runtime provider dependency yet.

### First milestone acceptance criteria

1. Raise left wrist; small rune visible only with intended wrist pose.
2. Trigger on rune reliably opens and closes a **readable parchment** in PCVR.
3. Menu can be grabbed and recentered, and never causes trapped input.
4. Works independently of the game's native journal/PowerUI and doesn't interfere with spell casting.
5. Compatible with Community and Renewed, or marked experimental on untested edition.
6. Desktop fallback/recovery works; removing menu does not stop any other mod.
