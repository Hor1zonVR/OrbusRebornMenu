using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusRebornMenu;

[BepInPlugin("com.horizon.orbus.rebornmenu", "RebornMenu", "0.2.9")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> EnableNativePrototype = null!;
    internal static ConfigEntry<bool> EnableNativeDiagnostics = null!;
    internal static ConfigEntry<bool> EnableLiveModsTab = null!;

    public override void Load()
    {
        Logger = Log;
        EnableNativePrototype = Config.Bind(
            "ExperimentalNativeSettings", "EnableModsTab", false,
            "Reserved for a future Mods tab after Spades upper-row tabs. " +
            "Native tab insertion is disabled until the exact live anchor is verified.");

        EnableNativeDiagnostics = Config.Bind(
            "ExperimentalNativeSettings", "LogSettingsMetadata", true,
            "Log at most 12 sanitized native Settings candidate node fingerprints " +
            "to BepInEx/LogOutput.log. Never saves Settings HTML, input or player data. " +
            "Restart required when changing this diagnostic option.");

        EnableLiveModsTab = Config.Bind(
            "NativeMods", "EnableLiveModsTab", true,
            "Append a native-styled Mods tab after Graphics in the original " +
            "Orbus Settings row. Does not change the Spades upper-row tabs. " +
            "The mod safely skips unsupported or empty menu documents.");

        // Critical: the old in-world rectangle, wrist M, custom cursor, and
        // temporary startup preview are NOT loaded or instantiated.
        NativeModsSettingsBridge.Start(Log);
        if (EnableLiveModsTab.Value)
            AddComponent<NativeModsRuntimeBehaviour>();
        if (EnableNativeDiagnostics.Value)
        {
            AddComponent<NativeSettingsProbeBehaviour>();
            Log.LogInfo("[RebornMenu/Probe] Ctrl+Shift+F7 captures bounded native DOM " +
                "node metadata on demand. Open Missions once, then Settings, " +
                "and press the shortcut while the game window is focused.");
        }
        Log.LogInfo("[RebornMenu] native Settings Mods-after-Graphics experiment loaded; legacy fragment injector disabled.");
        Log.LogInfo("[RebornMenu] No wrist rune, UI canvas, VR controller polling, " +
            "or independent laser is used. Other mods remain independent.");
    }
}
