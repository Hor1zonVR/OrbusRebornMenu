using System;
using UnityEngine;

namespace OrbusRebornMenu;

/// <summary>
/// Optional one-shot, desktop-keyboard-triggered read-only Settings probe.
/// This component adds no canvas, geometry, XR/controller input, HUD, or scene
/// scanner. With diagnostics enabled, only the keyboard chord is polled.
/// </summary>
public sealed class NativeSettingsProbeBehaviour : MonoBehaviour
{
    public NativeSettingsProbeBehaviour(IntPtr pointer) : base(pointer) { }

    private void Update()
    {
        try
        {
            if (!Plugin.EnableNativeDiagnostics.Value) return;
            if (!Input.GetKeyDown(KeyCode.F7)) return;

            bool control = Input.GetKey(KeyCode.LeftControl) ||
                           Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) ||
                         Input.GetKey(KeyCode.RightShift);
            if (control && shift)
                NativeModsSettingsBridge.CaptureDocumentSnapshot();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[RebornMenu/Probe] Manual snapshot skipped: " +
                ex.GetType().Name);
        }
    }
}
