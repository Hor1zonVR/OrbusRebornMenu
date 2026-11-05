using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OrbusRebornMenu;

/// <summary>
/// Small native Settings integration, kept independent of Spades' extra row.
/// Never rewrites the original HTML or modifies game assets. Appends one
/// native-styled tab after Graphics and a settings page inside the existing
/// scroll region; refuses to run when the real original controls aren't found.
/// </summary>
public sealed class NativeModsRuntimeBehaviour : MonoBehaviour
{
    public NativeModsRuntimeBehaviour(IntPtr pointer) : base(pointer) { }

    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static;
    private float _nextAttempt;
    private bool _loggedNoAnchor;
    private bool _loggedFailure;
    private long _boundTab;
    private object? _document;
    private object? _tab;
    private object? _page;
    private object? _value;
    private object? _minus;
    private object? _plus;

    private static Type? FindType(string name)
    {
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type? t = a.GetType(name, false);
                if (t != null) return t;
            }
            catch { }
        }
        return null;
    }

    private static object? Get(object? target, string name)
    {
        if (target == null) return null;
        try
        {
            Type t = target is Type type ? type : target.GetType();
            object? instance = target is Type ? null : target;
            PropertyInfo? property = t.GetProperty(name, Flags);
            if (property != null && property.GetIndexParameters().Length == 0)
                return property.GetValue(instance);
            FieldInfo? field = t.GetField(name, Flags);
            if (field != null) return field.GetValue(instance);
            return t.GetMethod("get_" + name, Flags, null,
                Type.EmptyTypes, null)?.Invoke(instance, null);
        }
        catch { return null; }
    }

    private static bool Set(object? target, string name, string value)
    {
        if (target == null) return false;
        try
        {
            Type t = target.GetType();
            PropertyInfo? property = t.GetProperty(name, Flags);
            if (property != null && property.CanWrite &&
                property.PropertyType == typeof(string))
            {
                property.SetValue(target, value);
                return true;
            }
            MethodInfo? setter = t.GetMethod("set_" + name, Flags, null,
                new[] { typeof(string) }, null);
            if (setter == null) return false;
            setter.Invoke(target, new object[] { value });
            return true;
        }
        catch { return false; }
    }

    private static object? CallOne(object? target, string name, string value)
    {
        if (target == null) return null;
        try
        {
            MethodInfo? method = target.GetType().GetMethod(name, Flags,
                null, new[] { typeof(string) }, null);
            return method?.Invoke(target, new object[] { value });
        }
        catch { return null; }
    }

    private static bool AppendHtml(object? target, string html)
    {
        if (target == null) return false;
        try
        {
            // Available on BOTH PowerUI.Element (Community) and
            // PowerUI.HtmlElement (Spades); appends without rebuilding
            // existing tab elements or their original native handlers.
            MethodInfo? method = target.GetType().GetMethod("appendChild", Flags,
                null, new[] { typeof(string) }, null);
            if (method == null) return false;
            method.Invoke(target, new object[] { html });
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[RebornMenu] appendChild failed safely: " +
                (ex.InnerException ?? ex).GetType().Name);
            return false;
        }
    }

    private static object? ById(object? doc, string id)
    {
        return CallOne(doc, "getElementById", id);
    }

    private static object? FirstByClass(object? doc, string className)
    {
        object? collection = CallOne(doc, "getElementsByClassName", className);
        if (collection == null) return null;
        try
        {
            object? len = Get(collection, "length") ?? Get(collection, "Count");
            if (len != null && Convert.ToInt32(len, CultureInfo.InvariantCulture) <= 0)
                return null;
            MethodInfo? item = collection.GetType().GetMethod("item", Flags,
                null, new[] { typeof(int) }, null)
                ?? collection.GetType().GetMethod("get_Item", Flags,
                null, new[] { typeof(int) }, null);
            return item?.Invoke(collection, new object[] { 0 });
        }
        catch { return null; }
    }

    private static long Identity(object? node)
    {
        if (node == null) return 0;
        if (Get(node, "Pointer") is IntPtr ptr && ptr != IntPtr.Zero)
            return ptr.ToInt64();
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(node);
    }

    private static bool LooksLikeOriginalSettings(object header, object doc)
    {
        // The original Settings header has exactly these four tab class
        // identifiers in BOTH captured PlayerMenu templates. No guessing
        // based on an empty placeholder or the Spades-only addon row.
        object? html = Get(header, "innerHTML");
        if (html is string source && source.Length >= 180 &&
            source.Contains("data-switchtab-general", StringComparison.Ordinal) &&
            source.Contains("data-switchtab-locomotion", StringComparison.Ordinal) &&
            source.Contains("data-switchtab-audio", StringComparison.Ordinal) &&
            source.Contains("data-switchtab-graphics", StringComparison.Ordinal))
            return true;

        // Some PowerUI builds do not expose innerHTML but do index children.
        foreach (string name in new[] { "general", "locomotion", "audio", "graphics" })
            if (FirstByClass(doc, "data-switchtab-" + name) == null)
                return false;
        return true;
    }

    private void Update()
    {
        if (!Plugin.EnableLiveModsTab.Value) return;
        if (Time.realtimeSinceStartup < _nextAttempt) return;
        _nextAttempt = Time.realtimeSinceStartup + 1.25f;
        try { EnsureUi(); }
        catch (Exception ex)
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                Plugin.Logger.LogWarning("[RebornMenu] Mods tab integration skipped: " +
                    ex.GetType().Name + " " + ex.Message);
            }
        }
    }

    private void EnsureUi()
    {
        Type? type = FindType("ng") ?? FindType("PlayerMenu");
        object? doc = Get(type, "sew") ?? Get(type, "document");
        if (doc == null) return;

        object? header = ById(doc, "settings-tab-controls");
        object? content = FirstByClass(doc, "largesettingsmenu-inner");
        if (header == null || content == null ||
            !LooksLikeOriginalSettings(header, doc))
        {
            if (!_loggedNoAnchor)
            {
                _loggedNoAnchor = true;
                Plugin.Logger.LogWarning("[RebornMenu] Native Mods tab waiting: " +
                    "original Settings tabs or content region aren't available. " +
                    "No game DOM was modified.");
            }
            return;
        }

        _loggedNoAnchor = false;
        object? tab = ById(doc, "rebornmenu-settings-tab");
        object? page = ById(doc, "rebornmenu-settings-page");

        // If a prior partial attempt lost either node, don't duplicate or
        // edit the original document. This requires a menu reload to retry.
        if ((tab == null) != (page == null))
        {
            if (!_loggedFailure)
            {
                _loggedFailure = true;
                Plugin.Logger.LogWarning("[RebornMenu] Incomplete native Mods nodes; " +
                    "leaving existing UI intact until next menu reload.");
            }
            return;
        }

        if (tab == null)
        {
            // Keep the actual Orbus CSS classes, background image, font and
            // native button effects. Narrow the original tabs a little to
            // fit five controls in the existing 570px Settings header.
            const string newTab =
                "<div id=\"rebornmenu-settings-tab\" " +
                "class=\"settings-tab-control data-switchtab-mods\" " +
                "style=\"width:104px;margin-right:8px;\">Mods</div>";
            if (!AppendHtml(header, newTab)) return;

            const string newPage =
                "<div id=\"rebornmenu-settings-page\" " +
                    "class=\"settings-tab data-settingstab-mods\" " +
                    "style=\"display:none;\">" +
                "<h4>Ability Labels</h4>" +
                "<p>Change the size of the abbreviations above Bard and Musketeer abilities.</p>" +
                "<ul class=\"menu-list\" style=\"margin-top:10px;\">" +
                  "<li id=\"rebornmenu-size-minus\" class=\"compact option\">Smaller</li>" +
                  "<li id=\"rebornmenu-size-plus\" class=\"compact option\">Larger</li>" +
                "</ul>" +
                "<p id=\"rebornmenu-size-value\">Label size: 1.00x</p>" +
                "<p>Adjusts the installed Ability Labels mod. Changes save automatically.</p>" +
                "</div>";
            if (!AppendHtml(content, newPage)) return;
            tab = ById(doc, "rebornmenu-settings-tab");
            page = ById(doc, "rebornmenu-settings-page");
            if (tab == null || page == null) return;
            Plugin.Logger.LogInfo("[RebornMenu] Native Mods tab appended " +
                "after Graphics with game-owned CSS; no original nodes replaced.");
        }

        _document = doc;
        _tab = tab;
        _page = page;
        _value = ById(doc, "rebornmenu-size-value");
        _minus = ById(doc, "rebornmenu-size-minus");
        _plus = ById(doc, "rebornmenu-size-plus");

        long pointer = Identity(tab);
        if (pointer == _boundTab) return;

        if (!NativeModsSettingsBridge.TryBindNativeClick(tab, ShowMods,
                out string problem))
        {
            Plugin.Logger.LogWarning("[RebornMenu] Native Mods pointer " +
                "listener unavailable: " + problem);
            return;
        }
        if (_minus == null || !NativeModsSettingsBridge.TryBindNativeClick(
                _minus, () => AdjustSize(-0.1f), out problem))
        {
            Plugin.Logger.LogWarning("[RebornMenu] Smaller button unavailable: " +
                problem);
            return;
        }
        if (_plus == null || !NativeModsSettingsBridge.TryBindNativeClick(
                _plus, () => AdjustSize(0.1f), out problem))
        {
            Plugin.Logger.LogWarning("[RebornMenu] Larger button unavailable: " +
                problem);
            return;
        }
        _boundTab = pointer;
        NarrowOriginalTabWidths(doc);
        RefreshValue();
        Plugin.Logger.LogInfo("[RebornMenu] Native Mods VR controls ready. " +
            "Settings > Mods > Ability Labels > Smaller/Larger.");
    }

    private static void NarrowOriginalTabWidths(object doc)
    {
        foreach (string name in new[] { "general", "locomotion", "audio", "graphics" })
        {
            object? old = FirstByClass(doc, "data-switchtab-" + name);
            if (old == null) continue;
            object? style = Get(old, "style");
            // Deliberately change only width/margin, preserving all original
            // visuals, native mouse handlers, and selected-state classes.
            Set(style, "width", "104px");
            Set(style, "marginRight", "8px");
        }
    }

    private void ShowMods()
    {
        if (_document == null || _tab == null || _page == null) return;
        try
        {
            // Existing vanilla navigation switches other pages. Our panel
            // is one ordinary settings-tab, so the original page-switching
            // routine should also hide it when another tab is selected.
            foreach (string name in new[] { "general", "locomotion", "audio", "graphics" })
            {
                object? oldPage = FirstByClass(_document, "data-settingstab-" + name);
                Set(Get(oldPage, "style"), "display", "none");

                object? oldTab = FirstByClass(_document, "data-switchtab-" + name);
                if (oldTab != null)
                {
                    string classes = Get(oldTab, "className")?.ToString() ?? "";
                    if (classes.Contains("settings-tab-control-active",
                        StringComparison.Ordinal))
                        Set(oldTab, "className",
                            classes.Replace("settings-tab-control-active", "",
                                StringComparison.Ordinal).Trim());
                }
            }

            Set(Get(_page, "style"), "display", "block");
            string ownClasses = Get(_tab, "className")?.ToString() ?? "";
            if (!ownClasses.Contains("settings-tab-control-active",
                StringComparison.Ordinal))
                Set(_tab, "className",
                    ownClasses + " settings-tab-control-active");
            RefreshValue();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[RebornMenu] Native Mods open failed: " +
                ex.GetType().Name);
        }
    }

    private void AdjustSize(float delta)
    {
        if (!AbilityLabelsSettings.TryReadScale(out float old))
        {
            Set(_value, "textContent", "Ability Labels is not installed.");
            return;
        }

        float next = (float)Math.Round(Math.Clamp(old + delta, 0.5f, 2f), 2);
        if (!AbilityLabelsSettings.TryWriteScale(next))
        {
            Set(_value, "textContent", "Unable to change Ability Labels size.");
            return;
        }

        RefreshValue();
        Plugin.Logger.LogInfo("[RebornMenu] Ability Labels size changed to " +
            next.ToString("0.00", CultureInfo.InvariantCulture) + "x.");
    }

    private void RefreshValue()
    {
        string value = AbilityLabelsSettings.TryReadScale(out float scale)
            ? "Label size: " + scale.ToString("0.00", CultureInfo.InvariantCulture) + "x"
            : "Ability Labels isn't installed";
        Set(_value, "textContent", value);
    }
}
