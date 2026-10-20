using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusRebornMenu;

[BepInPlugin("com.horizon.orbus.rebornmenu", "RebornMenu", "0.2.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> EnableNativePrototype = null!;

    public override void Load()
    {
        Logger = Log;
        EnableNativePrototype = Config.Bind(
            "ExperimentalNativeSettings", "EnableModsTab", true,
            "Attempt to add a small Mods entry INSIDE a recognised Orbus Settings " +
            "page using its existing PowerUI and VR pointer. Reverts to the game's " +
            "unmodified UI on next restart if disabled. This experimental version " +
            "does not edit other mods' settings yet.");

        // Critical: the old in-world rectangle, wrist M, custom cursor, and
        // temporary startup preview are NOT loaded or instantiated.
        NativeModsSettingsBridge.Start(Log);
        Log.LogInfo("[RebornMenu] v0.2.0 experimental native Settings tab loaded.");
        Log.LogInfo("[RebornMenu] No wrist rune, Unity UI canvas, controller polling, " +
            "or independent laser is used. Other mods remain independent.");
    }
}
