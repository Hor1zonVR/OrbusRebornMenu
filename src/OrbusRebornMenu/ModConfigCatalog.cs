using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;

namespace OrbusRebornMenu;

/// <summary>
/// Discovers live BepInEx ConfigEntry objects without reloading .cfg files.
/// Third-party mod authors can use ordinary Config.Bind(...) calls with
/// AcceptableValueRange<T>; RebornMenu needs no per-mod code or reference.
/// Strings are intentionally excluded (they may contain API keys or secrets).
/// </summary>
internal static class ModConfigCatalog
{
    internal sealed class ModPage
    {
        internal string Id = "";
        internal string Name = "";
        internal readonly List<Setting> Settings = new();
        internal MethodInfo? CustomRenderer;
    }

    internal sealed class Setting
    {
        internal ConfigEntryBase Entry = null!;
        internal string Section = "";
        internal string Name = "";
        internal string DisplayName = "";
        internal string Hint = "";
        internal string Description = "";
        internal Type ValueType = typeof(float);
        internal double Min;
        internal double Max;
        internal string Kind = "";

        internal string DisplayValue()
        {
            try
            {
                object value = Entry.BoxedValue;
                if (Kind == "toggle") return (bool)value ? "Enabled" : "Disabled";
                if (Kind == "enum") return value.ToString() ?? "";
                double numeric = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (ValueType == typeof(int)) return numeric.ToString("0", CultureInfo.InvariantCulture);
                double range = Max - Min;
                return numeric.ToString(range < .02 ? "0.0000" :
                    range < .2 ? "0.000" : "0.00", CultureInfo.InvariantCulture);
            }
            catch { return "?"; }
        }

        internal bool TrySetNumeric(double value)
        {
            if (Kind != "range" || !double.IsFinite(value)) return false;
            try
            {
                value = Math.Clamp(value, Min, Max);
                Entry.BoxedValue = ValueType == typeof(int)
                    ? (object)(int)Math.Round(value)
                    : ValueType == typeof(float) ? (object)(float)value
                    : value;
                return true;
            }
            catch { return false; }
        }

        internal bool TryToggle()
        {
            if (Kind != "toggle") return false;
            try
            {
                Entry.BoxedValue = !(bool)Entry.BoxedValue;
                return true;
            }
            catch { return false; }
        }

        internal bool TrySetEnum(string selected)
        {
            if (Kind != "enum" || !ValueType.IsEnum) return false;
            try
            {
                if (!Enum.GetNames(ValueType).Contains(selected,
                        StringComparer.Ordinal)) return false;
                Entry.BoxedValue = Enum.Parse(ValueType, selected);
                return true;
            }
            catch { return false; }
        }
    }

    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic |
                                     BindingFlags.Instance | BindingFlags.Static;

    private static object? Member(object? owner, string name)
    {
        if (owner == null) return null;
        try
        {
            Type type = owner is Type t ? t : owner.GetType();
            object? instance = owner is Type ? null : owner;
            var property = type.GetProperty(name, All);
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(instance);
            return type.GetField(name, All)?.GetValue(instance);
        }
        catch { return null; }
    }

    private static IEnumerable<Type> PluginTypes()
    {
        // Assemblies can contain unrelated types, but scanning happens
        // only when Mods opens/refreshes; never during a Unity frame loop.
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            string name;
            try { name = assembly.GetName().Name ?? ""; }
            catch { continue; }
            // Massive Unity IL2CPP generated interop libraries contain tens
            // of thousands of types, none of which are BepInEx mod entrypoints.
            if (name.StartsWith("Il2Cpp", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("UnityEngine", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("System", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Assembly-CSharp", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Mono.", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("BepInEx.", StringComparison.OrdinalIgnoreCase))
                continue;

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(x => x != null).Cast<Type>().ToArray();
            }
            catch { continue; }
            foreach (Type type in types)
            {
                if (type.IsAbstract) continue;
                bool isPlugin = false;
                try
                {
                    isPlugin = type.GetCustomAttributes(typeof(BepInPlugin), false)
                        .Length > 0;
                }
                catch { }
                if (isPlugin) yield return type;
            }
        }
    }

    private static ConfigEntryBase? ResolveEntry(object? maybeEntry) =>
        maybeEntry as ConfigEntryBase;

    private static IEnumerable<ConfigEntryBase> FromConfig(object? config)
    {
        if (config is not IEnumerable sequence) yield break;
        foreach (object? item in sequence)
        {
            ConfigEntryBase? entry = ResolveEntry(Member(item, "Value"));
            if (entry != null) yield return entry;
        }
    }

    private static IEnumerable<ConfigEntryBase> StaticEntries(Type type)
    {
        foreach (FieldInfo field in type.GetFields(All))
        {
            if (!field.IsStatic ||
                !typeof(ConfigEntryBase).IsAssignableFrom(field.FieldType))
                continue;
            ConfigEntryBase? entry = null;
            try { entry = ResolveEntry(field.GetValue(null)); }
            catch { }
            if (entry != null) yield return entry;
        }
    }

    private static void AddFromPlugin(ModPage group, Type type, object? instance)
    {
        var unique = new HashSet<ConfigEntryBase>(group.Settings.Select(x => x.Entry));
        object? config = Member(instance, "Config");
        foreach (ConfigEntryBase entry in FromConfig(config).Concat(StaticEntries(type)))
        {
            if (!unique.Add(entry)) continue;
            Setting? setting = Create(entry);
            if (setting != null) group.Settings.Add(setting);
        }
    }

    // The native Mods page is a PLAYER SETTINGS surface, not a diagnostic
    // panel. Keep developer knobs in their original .cfg files so tools and
    // keyboard diagnostics continue to work, but do not show them in VR.
    // Future mods can explicitly opt out with the literal
    // ConfigDescription tag "RebornMenu.Hide" (no library dependency).
    private static bool HiddenInMenu(ConfigEntryBase entry)
    {
        string section = entry.Definition.Section;
        string key = entry.Definition.Key;
        string description = entry.Description.Description ?? "";

        foreach (string hidden in new[]
        {
            "Diagnostics", "Diagnostic", "Investigation", "Debug",
            "Bard Inspector", "ExperimentalNativeSettings",
            "Developer", "DevTools", "Internal"
        })
            if (string.Equals(section, hidden, StringComparison.OrdinalIgnoreCase))
                return true;

        if (description.StartsWith("DEPRECATED", StringComparison.OrdinalIgnoreCase) ||
            description.StartsWith("Legacy compatibility", StringComparison.OrdinalIgnoreCase) ||
            description.StartsWith("Legacy v0.", StringComparison.OrdinalIgnoreCase))
            return true;

        // Specific obsolete or development-only knobs still found on
        // working mods. They remain readable in their owner's .cfg file.
        if ((section == "Missions" && (key == "EnableRegionTaskGrouping" ||
                key == "EnableHtmlRegionTaskGrouping" || key == "SpadesMode")) ||
            (section == "LiveCapture" && key == "AutoStopOnSecondTrigger") ||
            (section == "NativeMods" && key == "EnableLiveModsTab"))
            return true;

        // Keep the raw font-unit tuning hidden, but expose the live
        // vertical/depth positioning controls requested by players.
        if (section == "Labels" && key == "ReadableTextSize")
            return true;

        try
        {
            if (entry.Description.Tags is IEnumerable tags)
                foreach (object? tag in tags)
                    if (tag is string t &&
                        string.Equals(t, "RebornMenu.Hide",
                            StringComparison.OrdinalIgnoreCase))
                        return true;
        }
        catch { }
        return false;
    }

    // Any BepInEx mod can customise a setting label and hint without
    // linking against RebornMenu. Unknown tags are ignored.
    private static string UiTag(ConfigEntryBase entry, string prefix)
    {
        try
        {
            if (entry.Description.Tags is IEnumerable tags)
                foreach (object? tag in tags)
                    if (tag is string text &&
                        text.StartsWith(prefix, StringComparison.Ordinal) &&
                        text.Length > prefix.Length)
                        return text.Substring(prefix.Length).Trim();
        }
        catch { }
        return "";
    }

    private static Setting? Create(ConfigEntryBase entry)
    {
        try
        {
            if (HiddenInMenu(entry)) return null;
            Type type = entry.SettingType;
            string kind = type == typeof(bool) ? "toggle" :
                type.IsEnum ? "enum" : "";
            double minimum = 0;
            double maximum = 0;

            if (type == typeof(float) || type == typeof(double) ||
                type == typeof(int))
            {
                object? range = entry.Description.AcceptableValues;
                if (range == null ||
                    !range.GetType().Name.StartsWith("AcceptableValueRange",
                        StringComparison.Ordinal))
                    return null;
                object? low = Member(range, "MinValue");
                object? high = Member(range, "MaxValue");
                if (low == null || high == null) return null;
                minimum = Convert.ToDouble(low, CultureInfo.InvariantCulture);
                maximum = Convert.ToDouble(high, CultureInfo.InvariantCulture);
                if (!double.IsFinite(minimum) ||
                    !double.IsFinite(maximum) ||
                    maximum <= minimum) return null;
                kind = "range";
            }

            // We only render known safe value types. Free-form strings are
            // deliberately never shown, even in descriptions or values.
            if (kind.Length == 0) return null;
            return new Setting
            {
                Entry = entry,
                Section = entry.Definition.Section,
                Name = entry.Definition.Key,
                DisplayName = UiTag(entry, "RebornMenu.Label="),
                Hint = UiTag(entry, "RebornMenu.Hint="),
                Description = entry.Description.Description ?? "",
                ValueType = type,
                Kind = kind,
                Min = minimum,
                Max = maximum
            };
        }
        catch { return null; }
    }

    internal static List<ModPage> Discover()
    {
        var pages = new Dictionary<Type, ModPage>();
        var instances = new Dictionary<Type, object>();

        // BepInEx keeps loaded plugin instances and their ConfigFiles in
        // its chainloader registry. Resolve reflectively for both v6
        // IL2CPP and older BepInEx distributions.
        foreach (string chainloaderTypeName in new[]
        {
            "BepInEx.Bootstrap.Chainloader",
            "BepInEx.Unity.IL2CPP.IL2CPPChainloader"
        })
        {
            Type? chainloader = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { chainloader = assembly.GetType(chainloaderTypeName, false); }
                catch { }
                if (chainloader != null) break;
            }
            object? infos = Member(chainloader, "PluginInfos");
            if (infos is not IEnumerable all) continue;
            foreach (object item in all)
            {
                object? info = Member(item, "Value");
                object? instance = Member(info, "Instance");
                if (instance == null) continue;
                instances[instance.GetType()] = instance;
            }
            if (instances.Count > 0) break;
        }

        foreach (Type type in PluginTypes())
        {
            object[] attribs;
            try { attribs = type.GetCustomAttributes(typeof(BepInPlugin), false); }
            catch { continue; }
            if (attribs.Length == 0) continue;
            BepInPlugin metadata = (BepInPlugin)attribs[0];
            var group = new ModPage { Id = metadata.GUID, Name = metadata.Name };
            instances.TryGetValue(type, out object? instance);
            AddFromPlugin(group, type, instance);
            // A mod may opt into its own native page without referencing
            // RebornMenu. Resolve its public provider only in its own
            // BepInPlugin assembly, and match the exact plugin GUID.
            group.CustomRenderer = NativeCustomModPages.Discover(type, metadata.GUID);

            // Some mods store all options in their plugin class's static
            // fields. Every such ConfigEntry is a live object already.
            // Keep installed mods visible even if they expose only
            // diagnostics or unsupported config types. Their page will
            // explain that no ordinary settings are available yet.
            group.Settings.Sort((a,b) =>
            {
                // Show the three Ability Labels adjustments together:
                // size, vertical, and depth. All other settings keep their
                // normal BepInEx section/setting ordering.
                if (group.Name.IndexOf("AbilityLabels", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    group.Name.IndexOf("Ability Labels", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string[] order = { "Scale", "AboveOrbMetres", "TowardHeadMetres" };
                    int ai = a.Section == "Labels" ? Array.IndexOf(order, a.Name) : -1;
                    int bi = b.Section == "Labels" ? Array.IndexOf(order, b.Name) : -1;
                    if (ai >= 0 && bi >= 0) return ai.CompareTo(bi);
                    if (ai >= 0 || bi >= 0) return ai >= 0 ? -1 : 1;
                }

                int section = string.Compare(a.Section, b.Section,
                    StringComparison.OrdinalIgnoreCase);
                return section != 0 ? section :
                    string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            pages[type] = group;
        }

        var result = pages.Values.OrderBy(x => x.Name,
            StringComparer.OrdinalIgnoreCase).ToList();

        // Initial supported proof of concept must still be discoverable if
        // a client hides plugin instances from BepInEx's public registry.
        // The entry is the SAME live ConfigEntry<float>, not a duplicate.
        ConfigEntryBase? labelScale = AbilityLabelsSettings.LiveScaleEntry();
        if (labelScale != null && !result.Any(m =>
                m.Settings.Any(s => ReferenceEquals(s.Entry, labelScale))))
        {
            Setting? scale = Create(labelScale);
            if (scale != null)
            {
                var labels = new ModPage
                {
                    Id = "com.horizon.orbus.abilitylabels",
                    Name = "Ability Labels"
                };
                labels.Settings.Add(scale);
                result.Add(labels);
                result.Sort((a,b) => string.Compare(a.Name, b.Name,
                    StringComparison.OrdinalIgnoreCase));
            }
        }

        Plugin.Logger.LogInfo("[RebornMenu] Discovered " + result.Count +
            " installed mods; only player-facing bounded numeric / bool / enum controls shown.");
        return result;
    }
}
