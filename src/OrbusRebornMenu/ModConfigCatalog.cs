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
    }

    internal sealed class Setting
    {
        internal ConfigEntryBase Entry = null!;
        internal string Section = "";
        internal string Name = "";
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

    private static Setting? Create(ConfigEntryBase entry)
    {
        try
        {
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

            // Some mods store all options in their plugin class's static
            // fields. Every such ConfigEntry is a live object already.
            if (group.Settings.Count == 0) continue;
            group.Settings.Sort((a,b) =>
            {
                int section = string.Compare(a.Section, b.Section,
                    StringComparison.OrdinalIgnoreCase);
                return section != 0 ? section :
                    string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            pages[type] = group;
        }

        var result = pages.Values.OrderBy(x => x.Name,
            StringComparer.OrdinalIgnoreCase).ToList();
        Plugin.Logger.LogInfo("[RebornMenu] Discovered " + result.Count +
            " mods with live BepInEx controls (bounded numeric / bool / enum only).");
        return result;
    }
}
