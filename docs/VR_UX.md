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

## Grounded visual reference: real OrbusVR screenshots (20 October 2026)

These details come from the player's **actual screenshots of OrbusVR mission, inventory, achievements, tooltip and quest-book interfaces**. Treat these as the primary visual reference rather than generic medieval/parchment concept art. The screenshot files live in the user's conversation; this documentation records observed details but does not redistribute game screenshot assets.

### Two distinct native UI styles

**A — framed parchment boards (use as the MAIN RebornMenu style)**

- The Missions and Inventory panels are independent, fairly flat rectangular boards with a **muted greige / tan parchment surface**, weathered slightly uneven edges, thin dark brown wooden/metal-looking perimeter and corner detailing.
- The game does **not** use an extravagant fantasy scroll frame, a glowing border or heavily skeuomorphic leather book on these panels.
- Navigation uses **small raised brown tabs along the top**: *Equipment*, *Talents*, *Missions*, *Achievements*. The selected tab has a restrained amber/gold highlight rather than a large solid-colour switch.
- Between the boards sits a column of **square, softly rounded brown icon tabs** with simple cream pictograms (person, bag, people, envelope, gear, information, X). Their border/embossed shadow supplies the sense of depth.
- Inventory items live in **small inset brown/tan square slots** with a dark outer rim and slight bevel. Active selection is a fine amber outline; inactive slots are darker. We should adapt this slot language to mod icons, toggles and selectable options.
- Text is straightforward and legible, with bright off-white headings on some board sections and **dark brown/black ink** for supporting tooltip/quest text. Do not introduce cursive fantasy fonts merely for atmosphere.
- Some native boards have a **narrow pale-blue vertical scrollbar** on one edge. Use a similarly thin scrollbar as a restrained affordance; never an oversized bright blue widget.
- The floating tooltip is a small **dark brown rectangular inset with a thick border and subtly rounded corners**, containing white/off-white text and restrained cyan accents. This is a good model for setting descriptions and compatibility warnings.

**B — free-floating quest book pages (use for HELP / DOCUMENTATION, not primary mod controls)**

- A separate quest journal appears as two joined warm ochre parchment pages, with **black serif-like / illustrated compact text**, a narrow centre seam, light paper creases and no heavy frame.
- Under the opened pages is a row of bright category icons (Quests, Map, Recipes, Spells, Class Info). This appearance is distinctive, but putting sliders and VR controls onto tiny journal text would be difficult to read and inconsistent with the game's settings board.
- Use this open-book look for optional first-use help, mod instructions, or credits only if it can be rendered clearly at VR scale.

### First implementation choice

**One self-contained framed parchment board**, summoned by the left-wrist rune and rendered as a Unity world-space panel, with a native-looking top tab row:

```text
  [ Mods ]  [ Settings ]  [ Appearance ]                  [ X ]
╭────────────────────────────────────────────────────────────╮
│                       Installed Mods                       │
│                                                            │
│   [▣] CastMetrics                              ›           │
│   [▣] QuestHelper                              ›           │
│   [▣] RebornCam                                ›           │
│   [▣] GoblinTimer                              ›           │
│                                                            │
│   ───────────────────────────────────────────────────      │
│   Recenter                           Settings   Help       │
╰────────────────────────────────────────────────────────────╯
```

On selecting CastMetrics, reuse the **same board** and replace its content with appropriately grouped config controls. Don't spawn the game's actual inventory or move native PlayerMenu/PowerUI objects. A dedicated panel may borrow the *visual styling* but owns its own rendering, hit areas and input.

### Approximate colour targets (reference, not final measured game asset values)

| UI role | Screenshot-derived starting point |
| --- | --- |
| Main board paper | muted warm greige ~ `#998770` (shading/textures make values vary) |
| Raised navigation tabs | mid-to-dark brown `#60482F` |
| Border/wood rim | deeper weathered brown `#5A4129` |
| Printed dark ink | near-black warm brown `#231B14` |
| Hover/selected highlight | thin muted golden-amber `#D8A437` |
| Off-white heading/icons | parchment-cream `#E5D8BC` |
| Tooltip plate | deep warm brown `#3B2E21` |
| Optional scrollbar | muted desaturated pale blue, used sparingly |

Palette swatches are **rough visual matches from the screenshots**, not a claim of original game texture/material RGB values. Prefer referencing the game's already-loaded permitted UI textures/fonts at runtime; if unavailable, use an original UI asset pack tuned against these screenshots.

### Readability and interaction lessons from screenshots

- Real native Orbus panels have compact labels, but our **VR configuration values must be larger than tiny native quest-book text**. Keep the native shapes and textures; give buttons, sliders and labels adequate spacing/contrast.
- Never layer unfinished mod text directly over the existing Missions panel. Screenshots of early QuestHelper work show how loose white/cyan labels can overlap native Missions/Achievements tabs; RebornMenu is its **own contained canvas**, not a loose text overlay.
- Keep the centre of the panel uncluttered. Top tabs, small mod icon slots and a narrow side rail make more sense than numerous unrelated HUD elements.
- A wrist ornament opener should borrow **the small brown raised-icon language** rather than resemble a floating modern blue pill button. The rune may softly highlight **only when the left wrist faces the player**, and cannot take normal casting input.
- Save menu scale, recenter position and left/right wrist preference so smaller VR displays and seated players can use it.

## Layout of the parchment

```text
┌──────────────────────── Mods ────────────────────────┐
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
