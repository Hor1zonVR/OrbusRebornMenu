using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OrbusRebornMenu;

/// <summary>
/// Version 1 custom page convention for ANY installed BepInEx mod.
/// No reference to RebornMenu is required by provider assemblies.
///
/// Provider class, anywhere in the same assembly as the BepInPlugin:
///   public const string RebornMenuModGuid = "the.plugin.guid";
///   public static bool RenderInRebornMenu(object document, object content)
///
/// Return true after rendering. Return false for generic Config.Bind fallback.
/// A provider may own buttons, native keyboard callbacks and its page state.
/// </summary>
internal static class NativeCustomModPages
{
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private static readonly Dictionary<Assembly, Type[]> TypesByAssembly = new();

    private static Type[] Types(Assembly assembly)
    {
        if (TypesByAssembly.TryGetValue(assembly, out Type[]? cached))
            return cached;

        Type[] found;
        try { found = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        {
            found = ex.Types.Where(x => x != null).Cast<Type>().ToArray();
        }
        catch { found = Array.Empty<Type>(); }

        TypesByAssembly[assembly] = found;
        return found;
    }

    internal static MethodInfo? Discover(Type pluginType, string pluginGuid)
    {
        if (string.IsNullOrWhiteSpace(pluginGuid)) return null;

        // Only consider the installed plugin's own assembly. A different
        // plugin cannot claim this page by exposing somebody else's GUID.
        foreach (Type type in Types(pluginType.Assembly))
        {
            if (!type.IsVisible || type.ContainsGenericParameters) continue;

            // Constant is read without running arbitrary plugin code.
            FieldInfo? field = type.GetField("RebornMenuModGuid", PublicStatic);
            if (field == null || !field.IsLiteral || field.FieldType != typeof(string))
                continue;

            string? claimedGuid;
            try { claimedGuid = field.GetRawConstantValue() as string; }
            catch { continue; }
            if (!string.Equals(claimedGuid, pluginGuid, StringComparison.Ordinal))
                continue;

            MethodInfo? method = type.GetMethod("RenderInRebornMenu", PublicStatic,
                null, new[] { typeof(object), typeof(object) }, null);
            if (method == null || !method.IsStatic || method.ReturnType != typeof(bool) ||
                method.ContainsGenericParameters)
                continue;

            return method;
        }
        return null;
    }

    internal static bool TryRender(ModConfigCatalog.ModPage mod, object document,
        object content)
    {
        MethodInfo? renderer = mod.CustomRenderer;
        if (renderer == null) return false;

        try
        {
            return renderer.Invoke(null, new[] { document, content }) is true;
        }
        catch (Exception e)
        {
            // A bad custom page must not prevent the rest of native Mods
            // from working. Standard Config.Bind controls remain available.
            Plugin.Logger.LogWarning("[RebornMenu/CustomPages] " + mod.Id +
                " failed to render (" + (e.InnerException ?? e).GetType().Name +
                "). Falling back to normal mod settings.");
            return false;
        }
    }
}
