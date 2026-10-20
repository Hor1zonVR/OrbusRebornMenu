# RebornMenu v0.1 — local runtime test (experimental)

> **Important:** This is a prototype branch. No public release, automatic RM install or third-party config editor is available yet.

The first DLL creates an independent, parchment-coloured world-space panel and a small left-wrist rune. It deliberately does **not** edit the game's built-in journal, menus, inventory or XR rig.

## Build for a prepared RM instance

1. Create a clean Community or Renewed instance in RM and install BepInEx.
2. Launch the game once, then close it. Confirm `BepInEx/interop` exists.
3. Clone the RebornMenu repo / switch to `feature/first-vr-runtime-prototype`.
4. From the RebornMenu directory, run:

```powershell
.\build.ps1 -GameDir "$env:LOCALAPPDATA\OrbusRebornModdingInstances\My Orbus Renewed" -Install
```

Use your **actual** RM instance path if it differs. The script changes only the specified instance when `-Install` is given. Run `.\build.ps1` without parameters to choose among detected instances; it never uploads interop files or alters your game's native UI.

## First in-headset tests

- Press **Ctrl+Alt+M** on desktop to open, close, and recenter a plain framed parchment board. **First check whether it is visible inside the VR headset**, not merely the monitor.
- Turn the **underside of the left wrist** toward your headset to reveal a small brown "M" rune.
- While looking at the rune, aim your right controller at it and press its trigger. The menu should open/close.
- When the menu is visible, aim the right-controller pointer at the central **Test VR pointer** label and press the trigger; the status should become **VR pointer works!**
- The **CLOSE** target is upper-right, and **RECENTER** is lower-left.
- Repeat after teleporting or changing worlds, and verify you can cast and grab objects normally when the menu is closed.

The legacy Unity 2018 right-controller trigger mapping has **not** yet been verified on a Quest 3 via Link. The default uses `JoystickButton15`; it can be changed in `BepInEx/config/com.horizon.orbus.rebornmenu.cfg` under `RightTriggerJoystickButton` (0–19). Changing this requires relaunching or reloading if the runtime doesn't pick up the change. `ControllerPointerPitchDegrees` can be adjusted if the ray does not line up with your right controller.

**Do not use this test build during important combat; we're verifying the controller input mapping first.** The prototype intentionally never intercepts or blocks game trigger input.

## Logs and recovery

Check `BepInEx/LogOutput.log` for lines prefixed `[RebornMenu]`. Useful failure details: panel shown on monitor but not headset; wrist rune never visible; rune appears on wrong side; trigger maps to another button; controller pointer angle incorrect; objects appear behind parchment; fonts not visible.

To remove the prototype, close OrbusVR then delete only:

`BepInEx/plugins/OrbusRebornMenu/OrbusRebornMenu.dll`

Your other plugins and settings remain untouched.

## What is intentionally missing

This phase does not enumerate mods or change settings. Once the VR surface and input are proven, the next phase is universal BepInEx config discovery and editing, followed by optional custom pages and real HUD positioning.
