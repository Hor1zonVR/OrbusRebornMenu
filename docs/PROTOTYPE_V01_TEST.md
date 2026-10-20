# RebornMenu v0.1 — install and update through RebornManager

> **Experimental VR prototype.** The DLL source is still awaiting a full build against real IL2CPP game references and headset testing. Do **not** make RebornMenu a required core companion yet.

## Regular mod workflow — no instance chosen during building

RebornMenu is a normal, optional BepInEx client plugin until we have proven its VR UI and generic configuration editor. It is **not automatically installed when BepInEx is prepared yet**.

- Changes are developed in this GitHub repository.
- The maintainer runs `./release.ps1` on Windows to build from the newest `main` and publish **OrbusRebornMenu.dll** to GitHub Releases.
- The build script **automatically finds and remembers a local BepInEx interop reference directory**. It does **not** ask for an installation destination and **never installs a DLL in any game**.
- RM lists **RebornMenu** in **Client Mods**, and a normal stable release can be installed, updated, disabled or uninstalled for the instance you're managing.
- A GitHub *prerelease* created with `./release.ps1 -Preview` deliberately does **not** appear in RM's stable catalogue. Use preview for experiments before promoting the version.
- Once the runtime is stable, RM can manage it as a core companion, installed automatically after BepInEx initialization; normal BepInEx mods must not depend on it.

### To publish a new version (maintainers)

On Windows with Git, GitHub CLI and .NET SDK installed:

```powershell
git clone https://github.com/Hor1zonVR/OrbusRebornMenu.git
cd OrbusRebornMenu
.\release.ps1
```

Or, in a repo you've already cloned:

```powershell
git switch main
git pull --ff-only
.\release.ps1
```

If the current version tag has already been released and there are actual plugin source changes, the publisher bumps its patch version automatically. It verifies the build succeeds **before** creating a release. `-GameDir` is an optional *compiler-reference override*, not a destination for installing the mod.

Once the DLL release exists, use **RM → your instance → Browse Client Mods → RebornMenu → Install/Update**. This is the ordinary RM workflow—no PowerShell file copying and no manual instance picker when you're already inside an instance.

### First VR tests

- **Ctrl+Alt+M** opens/closes/recentres a basic framed parchment. Check if it appears **inside the headset**, not only on your monitor.
- Turn the underside of your left wrist toward the headset. A small rune should appear.
- Aim the other controller at the rune, press trigger, and test opening/closing.
- Press **Test VR pointer** in the panel; confirm feedback reads *VR pointer works!*.
- Recenter and close; test again after teleporting/scene changes.
- Verify all normal combat and grabbing controls continue functioning with the menu closed.

Legacy Unity 2018 trigger mapping is **unverified** with Quest 3 via Link. If the right trigger is not detected, check `BepInEx/config/com.horizon.orbus.rebornmenu.cfg` under `RightTriggerJoystickButton` (default `15`, supported `0–19`). Adjust `ControllerPointerPitchDegrees` if aiming is offset.

The project is intentionally **not a generic config editor yet**. That comes after the menu can reliably open, close and accept VR interactions.

### Logs and recovery

Inspect `BepInEx/LogOutput.log` for `[RebornMenu]`. If the prototype causes problems, close the game and disable **RebornMenu** in RM—do not alter the native Orbus files or other plugins.

For technical issues, record: game edition/version, BepInEx log, whether the parchment was visible on the headset, left wrist rune orientation, trigger mapping, and any gameplay input interference.
