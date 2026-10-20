using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusRebornMenu;

[BepInPlugin("com.horizon.orbus.rebornmenu", "RebornMenu", "0.2.2")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> EnableNativePrototype = null!;
    internal static ConfigEntry<bool> EnableNativeDiagnostics = null!;

    public override void Load()
    {
        Logger = Log;
        EnableNativePrototype = Config.Bind(
            "ExperimentalNativeSettings", "EnableModsTab", true,
            "Attempt to add a small Mods entry INSIDE a recognised Orbus Settings " +
            "page using its existing PowerUI and VR pointer. Reverts to the game's " +
            "unmodified UI on next restart if disabled. This experimental version " +
            "does not edit other mods' settings yet.");

        EnableNativeDiagnostics = Config.Bind(
            "ExperimentalNativeSettings", "LogSettingsMetadata", true,
            "Log at most 12 sanitized native Settings candidate node fingerprints " +
            "to BepInEx/LogOutput.log. Never saves Settings HTML, input or player data. " +
            "Restart required when changing this diagnostic option.");

        // Critical: the old in-world rectangle, wrist M, custom cursor, and
        // temporary startup preview are NOT loaded or instantiated.
        NativeModsSettingsBridge.Start(Log);
        if (EnableNativeDiagnostics.Value)
        {
            AddComponent<NativeSettingsProbeBehaviour>();
            Log.LogInfo("[RebornMenu/Probe] Ctrl+Shift+F7 captures bounded native DOM " +
                "node metadata on demand. Open Missions once, then Settings, " +
                "and press the shortcut while the game window is focused.");
        }
        Log.LogInfo("[RebornMenu] native Settings v0.2.2 read-only document probe loaded.");
        Log.LogInfo("[RebornMenu] No wrist rune, UI canvas, VR controller polling, " +
            "or independent laser is used. Other mods remain independent.");
    }
}
