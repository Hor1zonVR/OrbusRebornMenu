using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusRebornMenu;

[BepInPlugin("com.horizon.orbus.rebornmenu", "RebornMenu", "0.1.1")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<float> MenuDistance = null!;
    internal static ConfigEntry<float> MenuScale = null!;
    internal static ConfigEntry<int> RightTriggerButton = null!;
    internal static ConfigEntry<float> PointerPitch = null!;
    internal static ConfigEntry<bool> ShowWristRune = null!;
    internal static ConfigEntry<bool> StartupPreview = null!;

    public override void Load()
    {
        Logger = Log;
        MenuDistance = Config.Bind("Display", "DistanceMeters", 0.85f,
            new ConfigDescription("Distance of the floating parchment from your view.",
                new AcceptableValueRange<float>(0.55f, 1.5f)));
        MenuScale = Config.Bind("Display", "Scale", 1f,
            new ConfigDescription("Size of the menu.", new AcceptableValueRange<float>(0.7f, 1.8f)));
        RightTriggerButton = Config.Bind("Input", "RightTriggerJoystickButton", 15,
            new ConfigDescription("Experimental legacy Unity joystick button index for the RIGHT trigger (0-19). Confirm in VR before changing.",
                new AcceptableValueRange<int>(0, 19)));
        PointerPitch = Config.Bind("Input", "ControllerPointerPitchDegrees", 0f,
            new ConfigDescription("Fine-tune VR pointer aim if the controller forward direction is inaccurate.",
                new AcceptableValueRange<float>(-50f, 50f)));
        ShowWristRune = Config.Bind("Input", "ShowLeftWristRune", true,
            "Show a small rune under your left wrist when its face is turned towards the headset.");

        StartupPreview = Config.Bind("Diagnostics", "ShowStartupPreview", true,
            "Show the test parchment for 25 seconds after the real VR eye camera becomes ready. Disable after confirming it renders.");

        // Independent of the game's native journal, PowerUI or XR rig hierarchy.
        AddComponent<MenuRuntime>();
        Log.LogInfo("[RebornMenu] v0.1.1 visibility/input diagnostic build loaded.");
        Log.LogInfo("[RebornMenu] INSERT or CTRL+ALT+M opens/closes parchment; startup preview runs once when the real VR camera is ready.");
        Log.LogInfo("[RebornMenu] This build does not edit configs or auto-install through RM yet.");
    }
}
