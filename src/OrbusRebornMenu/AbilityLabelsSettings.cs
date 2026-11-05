using System;
using System.Reflection;
using BepInEx.Configuration;

namespace OrbusRebornMenu;

/// <summary>
/// The first RebornMenu integration: operate on Ability Labels' LIVE ConfigEntry,
/// not a second ConfigFile instance or an independently cached .cfg file.
/// This is intentionally optional; RebornMenu starts without Ability Labels.
/// </summary>
internal static class AbilityLabelsSettings
{
    private const string TargetTypeName = "OrbusAbilityLabels.Plugin";
    private const string ScaleFieldName = "LabelScale";

    private static ConfigEntry<float>? ResolveScale()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? pluginType;
            try { pluginType = assembly.GetType(TargetTypeName, false); }
            catch { continue; }
            if (pluginType == null) continue;

            try
            {
                FieldInfo? field = pluginType.GetField(ScaleFieldName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return field?.GetValue(null) as ConfigEntry<float>;
            }
            catch { return null; }
        }
        return null;
    }

    // Fallback for generic config discovery when a BepInEx IL2CPP
    // chainloader instance does not publicly expose its ConfigFile.
    internal static ConfigEntryBase? LiveScaleEntry() => ResolveScale();

    internal static bool TryReadScale(out float scale)
    {
        scale = 1f;
        try
        {
            ConfigEntry<float>? entry = ResolveScale();
            if (entry == null) return false;
            scale = entry.Value;
            return true;
        }
        catch { return false; }
    }

    internal static bool TryWriteScale(float desiredScale)
    {
        if (float.IsNaN(desiredScale) || float.IsInfinity(desiredScale))
            return false;

        try
        {
            ConfigEntry<float>? entry = ResolveScale();
            if (entry == null) return false;
            entry.Value = Math.Clamp(desiredScale, 0.5f, 2.0f);
            // ConfigEntry.Value notifies the mod and BepInEx saves the config.
            // AbilityLabelVisuals reads LabelScale.Value every LateUpdate.
            return true;
        }
        catch { return false; }
    }
}
