# RebornMenu v0.2 — experimental Mods entry in native Orbus Settings

**Status:** source prototype ready for *local IL2CPP build and headset testing*. Neither native Settings placement nor click handling has been confirmed in-game yet.

## What's different from the abandoned v0.1 wrist experiment?

- RebornMenu no longer creates its own world-space rectangle, wrist rune, cursor or controller ray.
- It does not hook or depend on **RebornCam**. Disable RebornCam if its recent diagnostics affected your game's stability.
- It integrates with the existing **PowerUI HTML rendering path** using the same legacy/modern distinction previously discovered in QuestHelper.
- If the game's HTML setter writes to one of a small set of recognised Settings elements (`settings-container`, `settings-content`, `settings-page`, etc.), RebornMenu appends a small **Mods** entry and a test panel *within the original Settings interface*. It preserves every byte of the original settings HTML, adds no new Unity canvas and never changes the game's existing tab callbacks.
- If the native pointer event can be registered on our Mods entry, tapping it with the **normal Orbus laser** toggles the test page. If event binding fails, the test page is shown inline, so the result isn't a dead, unexplained button.
- If this Settings node doesn't exist or uses a different render path, the integration **does nothing**. We would then need the actual Settings-page structure before safely attaching a separate top-level tab.

This is an **entry inside Settings**, not yet a guaranteed additional tab beside the game's own inventory/Settings navigation. The precise native tab container still has not been identified.

## Install and update through RM

After the first real DLL build and GitHub release:

1. Open RebornManager and select your normal Spades test instance.
2. Update **RebornMenu** under Client Mods. For this test, **disable RebornCam's UI diagnostics**, and make sure RebornCam isn't required.
3. Start OrbusVR. Use the normal in-game menu and open **Settings**.
4. Look for a brown **Mods** entry near the end of the Settings content.
5. If visible, aim using Orbus's **existing controller beam**, press trigger once, and check for a small `RebornMenu — Native Settings integration test` page.
6. Close/reopen Settings; confirm inventory, normal Settings sliders, spellcasting and other gameplay still work.

You do **not** need to press any desktop shortcut, scan Orbus UI or collect reports from RebornCam.

The first page intentionally doesn't yet edit other plugins. It proves navigation, native PowerUI rendering and event handling; universal BepInEx settings editing comes immediately after.

## If Mods doesn't appear

Open your instance's `BepInEx/LogOutput.log` and search only for `[RebornMenu]`.

- **Native Settings ... hook ready**: the patch was installed, not proof that the correct Settings node was found.
- **Added native Mods entry**: the Settings target was recognised and HTML extended.
- **Native VR Mods button wired**: a native click listener was attached.
- **Saw native Settings element, but left it unchanged**: the node or HTML differs; the game was intentionally left untouched.
- **No supported PowerUI Settings renderer**: wrong API/client or missing generated interop.

Send the small group of lines beginning `[RebornMenu]` (or the full log only if necessary). We can correct the exact selector based on evidence inside RebornMenu rather than repeatedly running RebornCam diagnostics.

## Safe rollback

To disable this feature without deleting the plugin, close OrbusVR and set the following in:

`BepInEx/config/com.horizon.orbus.rebornmenu.cfg`

```ini
[ExperimentalNativeSettings]
EnableModsTab = false
```

Restart the game. The original Settings HTML is generated normally. Alternatively, disable RebornMenu in RM and relaunch.

**Important:** Modding native game HTML is experimental; only test on a disposable or backed-up RM instance. If opening Settings causes freezing or broken UI, exit the game and disable RebornMenu before relaunching. This version does not write the native game's asset files or player settings.

## Release process

Source lives at [Hor1zonVR/OrbusRebornMenu](https://github.com/Hor1zonVR/OrbusRebornMenu). Maintainers compile using already-generated BepInEx IL2CPP references and publish a standalone DLL with `./release.ps1`. The build script does **not** copy the mod into a chosen game instance; installation and updates remain RM's job.

**Testing order:** native Settings Mods entry → normal VR laser click → small Settings page → universal config discovery → optional custom mod pages.
