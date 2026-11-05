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
    private string _lastWaitReason = "";
    private bool _loggedFailure;
    private long _boundTab;
    private long _boundMinus;
    private long _boundPlus;
    private bool _layoutApplied;
    private string _lastBindingWarning = "";
    private object? _document;
    private object? _tab;
    private object? _page;
    private object? _sidebar;
    private object? _content;
    private readonly List<ModConfigCatalog.ModPage> _mods = new();
    private string _selectedMod = "";
    private long _sidebarBuilt;
    private long _renderedContent;
    private bool _dragSupported;

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

    private static string _lastAppendWarning = "";

    private static bool AppendHtml(object? target, string html)
    {
        if (target == null) return false;
        try
        {
            Type type = target.GetType();

            // Confirmed in the Renewed IL2CPP dump: Dom.Element exports
            // insertAdjacentHTML(string positionName, string html),
            // appendInnerHTML(string), and append(string).
            // appendChild(string) belongs to PowerUI.HtmlElement only;
            // our live Settings lookup returns Dom.Element, so that
            // earlier call could never work.
            MethodInfo? adjacent = type.GetMethod("insertAdjacentHTML", Flags,
                null, new[] { typeof(string), typeof(string) }, null);
            if (adjacent != null)
            {
                adjacent.Invoke(target, new object[] { "beforeend", html });
                return true;
            }

            MethodInfo? appendInner = type.GetMethod("appendInnerHTML", Flags,
                null, new[] { typeof(string) }, null);
            if (appendInner != null)
            {
                appendInner.Invoke(target, new object[] { html });
                return true;
            }

            // Backward-compatible PowerUI.HtmlElement overload, only if
            // the returned type actually exposes it. Never rewrite
            // innerHTML, because that would destroy the original handlers.
            MethodInfo? legacy = type.GetMethod("appendChild", Flags,
                null, new[] { typeof(string) }, null);
            if (legacy != null)
            {
                legacy.Invoke(target, new object[] { html });
                return true;
            }

            string missing = type.FullName + ": no recognised HTML append API.";
            if (_lastAppendWarning != missing)
            {
                _lastAppendWarning = missing;
                Plugin.Logger.LogWarning("[RebornMenu] " + missing +
                    " Original menu left unchanged.");
            }
            return false;
        }
        catch (Exception ex)
        {
            Exception actual = ex.InnerException ?? ex;
            string warning = target.GetType().FullName + ": " +
                actual.GetType().Name + " " + actual.Message;
            if (_lastAppendWarning != warning)
            {
                _lastAppendWarning = warning;
                Plugin.Logger.LogWarning("[RebornMenu] Native HTML append failed: " +
                    warning + ". No fallback DOM rebuild attempted.");
            }
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
        // Prefer the loaded game's PlayerMenu document; the modern Spades
        // runtime may also expose a Dom.Document via PowerUI.UI.document or
        // the existing read-only HTML observer. Every candidate MUST have
        // the original Settings header and the four named vanilla controls.
        Type? type = FindType("ng") ?? FindType("PlayerMenu");
        var documents = new List<object>();
        object? playerDoc = Get(type, "sew") ?? Get(type, "document");
        if (playerDoc != null) documents.Add(playerDoc);

        object? global = Get(FindType("PowerUI.UI"), "document");
        if (global != null &&
            !documents.Any(d => Identity(d) == Identity(global)))
            documents.Add(global);

        foreach (object observed in NativeModsSettingsBridge.KnownDocuments())
        {
            if (!documents.Any(d => Identity(d) == Identity(observed)))
                documents.Add(observed);
        }

        object? doc = null;
        object? header = null;
        object? content = null;
        var candidates = new List<string>();
        foreach (object candidate in documents)
        {
            object? candidateHeader = ById(candidate, "settings-tab-controls");
            object? candidateContent = FirstByClass(candidate, "largesettingsmenu-inner");
            bool originalTabs = candidateHeader != null &&
                LooksLikeOriginalSettings(candidateHeader, candidate);
            if (candidates.Count < 4)
            {
                string headerHtml = Get(candidateHeader, "innerHTML") as string ?? "";
                bool general = FirstByClass(candidate, "data-switchtab-general") != null;
                bool graphics = FirstByClass(candidate, "data-switchtab-graphics") != null;
                candidates.Add(candidate.GetType().Name +
                    "[header=" + (candidateHeader != null) +
                    ",headerLength=" + headerHtml.Length +
                    ",page=" + (candidateContent != null) +
                    ",general=" + general +
                    ",graphics=" + graphics +
                    ",originalTabs=" + originalTabs + "]");
            }
            if (candidateHeader == null || candidateContent == null ||
                !originalTabs)
                continue;
            doc = candidate;
            header = candidateHeader;
            content = candidateContent;
            break;
        }

        if (doc == null || header == null || content == null)
        {
            // The first Update often runs before login creates PlayerMenu.
            // Re-check every 1.25s and log on STATE CHANGE, rather than
            // permanently hiding all subsequent useful diagnostics.
            string reason = "menuType=" + (type?.FullName ?? "<none>") +
                ",playerDocument=" + (playerDoc?.GetType().Name ?? "<none>") +
                ",docs=" + documents.Count +
                (candidates.Count > 0 ? ",checks=" + string.Join(";", candidates) : "");
            if (_lastWaitReason != reason)
            {
                _lastWaitReason = reason;
                Plugin.Logger.LogInfo("[RebornMenu] Waiting for real Settings: " +
                    reason + ". No game DOM modified.");
            }
            return;
        }

        if (_lastWaitReason != "ready")
        {
            _lastWaitReason = "ready";
            Plugin.Logger.LogInfo("[RebornMenu] Verified original Settings " +
                "header and scroll panel in " + doc.GetType().FullName + ".");
        }
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
                "style=\"display:none;width:104px;margin-right:8px;\">Mods</div>";
            if (!AppendHtml(header, newTab)) return;

            const string newPage =
                "<div id=\"rebornmenu-settings-page\" " +
                    "class=\"settings-tab data-settingstab-mods\" " +
                    "style=\"display:none;position:relative;min-height:450px;\">" +
                  "<div id=\"rm-mod-list\" style=\"position:absolute;left:3px;" +
                    "top:8px;width:148px;height:420px;overflow-y:auto;\"></div>" +
                  "<div id=\"rm-mod-content\" style=\"position:absolute;left:174px;" +
                    "top:4px;width:350px;height:420px;overflow-y:auto;\"></div>" +
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

        // Bind each node at most once per native pointer. Partial support
        // cannot register duplicate callbacks on the next update.
        if (_boundTab != Identity(tab))
        {
            _boundTab = 0;
            _layoutApplied = false;
            if (!NativeModsSettingsBridge.TryBindNativeClick(tab, ShowMods,
                    out string problem))
            {
                ReportBindingProblem("Mods tab: " + problem);
                return;
            }
            _boundTab = Identity(tab);
        }

        if (_minus == null)
        {
            ReportBindingProblem("Smaller: element missing");
            return;
        }
        if (_boundMinus != Identity(_minus))
        {
            _boundMinus = 0;
            if (!NativeModsSettingsBridge.TryBindNativeClick(
                    _minus, () => AdjustSize(-0.1f), out string problem))
            {
                ReportBindingProblem("Smaller: " + problem);
                return;
            }
            _boundMinus = Identity(_minus);
        }

        if (_plus == null)
        {
            ReportBindingProblem("Larger: element missing");
            return;
        }
        if (_boundPlus != Identity(_plus))
        {
            _boundPlus = 0;
            if (!NativeModsSettingsBridge.TryBindNativeClick(
                    _plus, () => AdjustSize(0.1f), out string problem))
            {
                ReportBindingProblem("Larger: " + problem);
                return;
            }
            _boundPlus = Identity(_plus);
        }

        if (!_layoutApplied)
        {
            if (!NarrowOriginalTabWidths(doc))
            {
                ReportBindingProblem("Original tab sizing API unavailable; " +
                    "Mods hidden to avoid overlap.");
                return;
            }
            _layoutApplied = true;
            RefreshValue();
            Plugin.Logger.LogInfo("[RebornMenu] Native Mods VR controls ready. " +
                "Settings > Mods > Ability Labels > Smaller/Larger.");
        }

        Set(Get(_tab, "style"), "display", "inline-block");
        SyncOtherTabs();
    }

    private void ReportBindingProblem(string message)
    {
        if (_lastBindingWarning == message) return;
        _lastBindingWarning = message;
        Plugin.Logger.LogWarning("[RebornMenu] Native Mods controls: " + message);
    }

    private static bool NarrowOriginalTabWidths(object doc)
    {
        var tabs = new List<object>();
        foreach (string name in new[] { "general", "locomotion", "audio", "graphics" })
        {
            object? old = FirstByClass(doc, "data-switchtab-" + name);
            if (old == null) return false;
            tabs.Add(old);
        }

        bool result = true;
        foreach (object old in tabs)
        {
            object? style = Get(old, "style");
            // Preserve Orbus's original images, text, event handlers and
            // active classes. Only adjust tab dimensions to fit all five.
            result = Set(style, "width", "104px") && result;
            result = Set(style, "marginRight", "8px") && result;
        }
        return result;
    }

    private void SyncOtherTabs()
    {
        if (_document == null || _tab == null || _page == null) return;
        string modsClasses = Get(_tab, "className")?.ToString() ?? "";
        if (!modsClasses.Contains("settings-tab-control-active",
            StringComparison.Ordinal)) return;

        foreach (string name in new[] { "general", "locomotion", "audio", "graphics" })
        {
            object? other = FirstByClass(_document, "data-switchtab-" + name);
            string classes = Get(other, "className")?.ToString() ?? "";
            if (!classes.Contains("settings-tab-control-active",
                StringComparison.Ordinal)) continue;

            Set(Get(_page, "style"), "display", "none");
            Set(_tab, "className", modsClasses.Replace(
                "settings-tab-control-active", "", StringComparison.Ordinal).Trim());
            break;
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
