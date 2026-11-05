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
    private object? _outerSettingsScroll;
    private bool _outerScrollDisabled;
    private bool _reportedOuterScroll;
    private long _outerScrollPointer;
    private readonly List<ModConfigCatalog.ModPage> _mods = new();
    private string _selectedMod = "";
    private long _sidebarBuilt;

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
                    "style=\"display:none;position:relative;width:540px;height:410px;" +
                    "overflow:hidden;padding:0;\">" +
                  "<div id=\"rm-mod-list\" style=\"position:absolute;left:3px;" +
                    "top:8px;width:148px;height:390px;overflow-x:hidden;" +
                    "overflow-y:auto;padding:0;\"></div>" +
                  "<div id=\"rm-mod-content\" style=\"position:absolute;left:174px;" +
                    "top:4px;width:350px;height:394px;overflow-x:hidden;" +
                    "overflow-y:auto;padding:0;\"></div>" +
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
        _sidebar = ById(doc, "rm-mod-list");
        _content = ById(doc, "rm-mod-content");
        _outerSettingsScroll = FirstByClass(doc, "largesettingsmenu-inner");
        long activeScrollPointer = Identity(_outerSettingsScroll);
        if (_outerScrollPointer != activeScrollPointer)
        {
            // The whole PlayerMenu document may be rebuilt after rejoining
            // or switching shards; do not reuse the previous DOM's state.
            _outerScrollPointer = activeScrollPointer;
            _outerScrollDisabled = false;
        }

        if (_sidebar == null || _content == null)
        {
            ReportBindingProblem("Mods sidebar/content not found in native document.");
            return;
        }

        if (_boundTab != Identity(tab))
        {
            _boundTab = 0;
            _sidebarBuilt = 0;
            _layoutApplied = false;
            if (!NativeModsSettingsBridge.TryBindNativeClick(tab, ShowMods,
                    out string problem))
            {
                ReportBindingProblem("Mods tab: " + problem);
                return;
            }
            _boundTab = Identity(tab);
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
            Plugin.Logger.LogInfo("[RebornMenu] Native Mods tab ready: " +
                "native sidebar, automatic live BepInEx config pages, " +
                "Orbus-style segmented sliders.");
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
            StringComparison.Ordinal))
        {
            SetOuterSettingsScroll(false);
            return;
        }

        foreach (string name in new[] { "general", "locomotion", "audio", "graphics" })
        {
            object? other = FirstByClass(_document, "data-switchtab-" + name);
            string classes = Get(other, "className")?.ToString() ?? "";
            if (!classes.Contains("settings-tab-control-active",
                StringComparison.Ordinal)) continue;

            Set(Get(_page, "style"), "display", "none");
            SetOuterSettingsScroll(false);
            Set(_tab, "className", modsClasses.Replace(
                "settings-tab-control-active", "", StringComparison.Ordinal).Trim());
            break;
        }
    }

    private void SetOuterSettingsScroll(bool modsVisible)
    {
        // Orbus already scrolls the whole .largesettingsmenu-inner.
        // Disable that THIRD scrollbar only on the Mods tab; the left
        // list and right config panel keep their own overflow-y:auto.
        // Restore the vanilla scrolling for every original Settings tab.
        if (_outerSettingsScroll == null) return;
        if (_outerScrollDisabled == modsVisible) return;

        object? style = Get(_outerSettingsScroll, "style");
        string overflow = modsVisible ? "hidden" : "auto";
        bool changed = Set(style, "overflowY", overflow);
        if (!changed)
            changed = Set(style, "overflow", modsVisible ? "hidden" : "auto");

        if (changed)
            _outerScrollDisabled = modsVisible;
        else if (!_reportedOuterScroll)
        {
            _reportedOuterScroll = true;
            Plugin.Logger.LogWarning("[RebornMenu] Parent Settings scroll " +
                "could not be changed via PowerUI style API. The Mods panels " +
                "remain bounded to their own dimensions.");
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
            SetOuterSettingsScroll(true);
            string ownClasses = Get(_tab, "className")?.ToString() ?? "";
            if (!ownClasses.Contains("settings-tab-control-active",
                StringComparison.Ordinal))
                Set(_tab, "className",
                    ownClasses + " settings-tab-control-active");
            EnsureModList();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[RebornMenu] Native Mods open failed: " +
                ex.GetType().Name);
        }
    }


    private static string FriendlyName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        var text = new System.Text.StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];
            if (i > 0 && char.IsUpper(current) &&
                (char.IsLower(name[i - 1]) ||
                (i + 1 < name.Length && char.IsLower(name[i + 1]) &&
                 char.IsUpper(name[i - 1]))))
                text.Append(' ');
            text.Append(current);
        }
        return text.ToString();
    }

    private static string Escape(string? text)
    {
        return (text ?? "").Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
    }

    private void EnsureModList()
    {
        if (_sidebar == null || _content == null || _document == null) return;
        long pointer = Identity(_sidebar);
        if (pointer == _sidebarBuilt) return;

        _mods.Clear();
        _mods.AddRange(ModConfigCatalog.Discover());

        if (_mods.Count == 0)
        {
            Set(_sidebar, "innerHTML", "<h4>Installed Mods</h4><p>No compatible " +
                "config entries found.</p>");
            Set(_content, "innerHTML",
                "<h4>No settings yet</h4><p>Mods can expose native controls " +
                "with BepInEx Config.Bind and acceptable numeric ranges.</p>");
            _sidebarBuilt = pointer;
            return;
        }

        if (!_mods.Any(m => m.Id == _selectedMod))
            _selectedMod = _mods.FirstOrDefault(m =>
                m.Name.IndexOf("Ability Labels", StringComparison.OrdinalIgnoreCase) >= 0
                || m.Name.IndexOf("AbilityLabels", StringComparison.OrdinalIgnoreCase) >= 0
                )?.Id ?? _mods[0].Id;

        var html = new System.Text.StringBuilder();
        html.Append("<h4 style='margin:3px 0 10px 0'>Installed Mods</h4>");
        html.Append("<ul class='menu-list' style='padding-left:0;margin:0'>");
        for (int i = 0; i < _mods.Count; i++)
        {
            html.Append("<li id='rm-mod-").Append(i).Append("' ");
            html.Append("class='compact option' style='width:134px;min-height:24px;");
            html.Append("margin-bottom:4px;font-size:13px'>");
            html.Append(Escape(_mods[i].Name)).Append("</li>");
        }
        html.Append("</ul>");

        if (!Set(_sidebar, "innerHTML", html.ToString()))
        {
            ReportBindingProblem("Cannot render native mod navigation.");
            return;
        }

        // No RebornMenu source modification is required for another mod:
        // its already-loaded ConfigEntry objects were discovered above.
        for (int i = 0; i < _mods.Count; i++)
        {
            int selectedIndex = i;
            object? nav = ById(_document, "rm-mod-" + i);
            if (nav == null)
            {
                ReportBindingProblem("Mod navigation " + i + ": node missing");
                return;
            }
            if (!NativeModsSettingsBridge.TryBindNativeClick(
                    nav, () => SelectMod(selectedIndex), out string reason))
            {
                ReportBindingProblem("Mod navigation " + i + ": " + reason);
                return;
            }
        }

        _sidebarBuilt = pointer;
        RenderSelected();
        Plugin.Logger.LogInfo("[RebornMenu] Native Mods list available: " +
            _mods.Count + " mod pages. No per-mod menu code required.");
    }

    private void SelectMod(int index)
    {
        if (index < 0 || index >= _mods.Count) return;
        _selectedMod = _mods[index].Id;
        RenderSelected();
    }

    private void RenderSelected()
    {
        if (_document == null || _content == null || _sidebar == null) return;
        ModConfigCatalog.ModPage? mod = _mods.FirstOrDefault(m =>
            m.Id == _selectedMod);
        if (mod == null) return;

        var html = new System.Text.StringBuilder();
        html.Append("<h4 style='margin:3px 0 9px 0'>")
            .Append(Escape(mod.Name)).Append("</h4>");
        html.Append("<p style='margin-bottom:12px;font-size:13px'>")
            .Append("Changes save to each mod's own BepInEx config.</p>");

        string section = "";
        int index = 0;
        foreach (ModConfigCatalog.Setting setting in mod.Settings.Take(35))
        {
            if (!string.Equals(section, setting.Section,
                    StringComparison.Ordinal))
            {
                section = setting.Section;
                html.Append("<h4 style='margin-top:14px;margin-bottom:9px'>")
                    .Append(Escape(section)).Append("</h4>");
            }
            html.Append("<div style='margin-bottom:15px'>");
            html.Append("<p style='font-size:14px;margin-bottom:6px'>")
                .Append(Escape(FriendlyName(setting.Name))).Append("</p>");
            if (setting.Kind == "range")
            {
                double value = ReadNumber(setting);
                double fraction = Math.Clamp((value-setting.Min) /
                    (setting.Max-setting.Min), 0, 1);
                int thumb = (int)Math.Round(fraction * 232);
                html.Append("<div class='sliderGroup' style='margin:0 0 6px 0'>");
                html.Append("<div id='rm-slider-").Append(index)
                    .Append("' class='rangeSlider' style='width:248px;height:12px;");
                html.Append("display:block;position:relative'>");
                html.Append("<div id='rm-thumb-").Append(index)
                    .Append("' class='rangeSelector' style='left:")
                    .Append(thumb).Append("px'></div>");
                // 31 transparent, individually clickable zones make the
                // native-looking slider usable with the VR laser even on
                // clients where a DOM MouseEvent has no local offsetX API.
                for (int j = 0; j <= 30; j++)
                {
                    html.Append("<div id='rm-zone-").Append(index).Append("-")
                        .Append(j).Append("' style='position:absolute;top:-6px;left:")
                        .Append(j * 8).Append("px;width:8px;height:24px;")
                        .Append("background-color:rgba(255,255,255,0.01)'></div>");
                }
                html.Append("</div></div>");
                html.Append("<p id='rm-val-").Append(index)
                    .Append("' style='font-size:13px;margin-top:8px'>")
                    .Append(Escape(setting.DisplayValue())).Append("</p>");
            }
            else if (setting.Kind == "toggle")
            {
                html.Append("<ul class='menu-list' style='margin-top:4px'>")
                    .Append("<li class='compact option' id='rm-toggle-")
                    .Append(index).Append("' style='width:245px'>")
                    .Append(Escape(setting.DisplayValue()))
                    .Append("</li></ul>");
            }
            else if (setting.Kind == "enum")
            {
                string[] names = Enum.GetNames(setting.ValueType).Take(12).ToArray();
                html.Append("<ul class='menu-list' style='margin-top:4px'>");
                for (int j = 0; j < names.Length; j++)
                    html.Append("<li class='compact option' id='rm-enum-")
                        .Append(index).Append("-").Append(j)
                        .Append("' style='width:245px'>")
                        .Append(Escape(names[j])).Append("</li>");
                html.Append("</ul>");
            }
            html.Append("</div>");
            index++;
        }

        if (mod.Settings.Count == 0)
            html.Append("<p style='margin-top:18px'>No player-facing settings " +
                "are exposed by this mod yet.</p>");

        if (!Set(_content, "innerHTML", html.ToString()))
        {
            ReportBindingProblem("Could not create selected mod controls.");
            return;
        }

        int max = Math.Min(mod.Settings.Count, 35);
        for (int i = 0; i < max; i++)
        {
            int settingIndex = i;
            ModConfigCatalog.Setting setting = mod.Settings[i];
            if (setting.Kind == "range")
            {
                for (int j = 0; j <= 30; j++)
                {
                    int step = j;
                    object? zone = ById(_document, "rm-zone-" + i + "-" + j);
                    if (zone == null)
                    {
                        ReportBindingProblem("Slider point " + i + "/" + j +
                            ": missing node");
                        continue;
                    }
                    if (!NativeModsSettingsBridge.TryBindNativeClick(zone,
                            () => SetSliderStep(settingIndex, step), out string problem))
                    {
                        ReportBindingProblem("Slider point " + i + "/" + j +
                            ": " + problem);
                        continue;
                    }

                    // Optional drag: on supported PowerUI clients, hovering a
                    // zone with the trigger held updates the same config entry.
                    NativePointerEvents.Bind(zone, "mousemove", evt =>
                    {
                        object? held = Get(evt, "heldDown");
                        if (held is bool pressed && pressed)
                            SetSliderStep(settingIndex, step);
                    }, out _);
                }
            }
            else if (setting.Kind == "toggle")
            {
                object? button = ById(_document, "rm-toggle-" + i);
                if (button == null)
                    ReportBindingProblem("Toggle " + i + ": missing node");
                else if (!NativeModsSettingsBridge.TryBindNativeClick(
                        button, () => ToggleSetting(settingIndex), out string problem))
                    ReportBindingProblem("Toggle " + i + ": " + problem);
            }
            else if (setting.Kind == "enum")
            {
                string[] names = Enum.GetNames(setting.ValueType).Take(12).ToArray();
                for (int j = 0; j < names.Length; j++)
                {
                    string name = names[j];
                    object? button = ById(_document, "rm-enum-" + i + "-" + j);
                    if (button == null)
                        ReportBindingProblem("Enum " + i + ": missing node");
                    else if (!NativeModsSettingsBridge.TryBindNativeClick(
                            button, () => SelectEnum(settingIndex, name),
                            out string problem))
                        ReportBindingProblem("Enum " + i + ": " + problem);
                }
            }
        }

        for (int i = 0; i < _mods.Count; i++)
        {
            object? button = ById(_document, "rm-mod-" + i);
            Set(Get(button, "style"), "color",
                _mods[i].Id == _selectedMod ? "#eda60c" : "#d5bd95");
        }
    }

    private static double ReadNumber(ModConfigCatalog.Setting setting)
    {
        try { return Convert.ToDouble(setting.Entry.BoxedValue,
            CultureInfo.InvariantCulture); }
        catch { return setting.Min; }
    }

    private ModConfigCatalog.Setting? SelectedSetting(int index)
    {
        ModConfigCatalog.ModPage? mod = _mods.FirstOrDefault(m =>
            m.Id == _selectedMod);
        return mod != null && index >= 0 && index < mod.Settings.Count
            ? mod.Settings[index] : null;
    }

    private void SetSliderStep(int index, int step)
    {
        ModConfigCatalog.Setting? setting = SelectedSetting(index);
        if (setting == null || setting.Kind != "range") return;
        double value = setting.Min + (setting.Max-setting.Min) *
            Math.Clamp(step / 30.0, 0, 1);
        if (!setting.TrySetNumeric(value)) return;

        object? thumb = ById(_document, "rm-thumb-" + index);
        Set(Get(thumb, "style"), "left",
            ((int)Math.Round(Math.Clamp((ReadNumber(setting)-setting.Min) /
                (setting.Max-setting.Min), 0, 1) * 232)) + "px");
        Set(ById(_document, "rm-val-" + index), "textContent",
            setting.DisplayValue());
    }

    private void ToggleSetting(int index)
    {
        ModConfigCatalog.Setting? setting = SelectedSetting(index);
        if (setting == null || !setting.TryToggle()) return;
        Set(ById(_document, "rm-toggle-" + index), "textContent",
            setting.DisplayValue());
    }

    private void SelectEnum(int index, string name)
    {
        ModConfigCatalog.Setting? setting = SelectedSetting(index);
        if (setting == null || !setting.TrySetEnum(name)) return;
        for (int j = 0; j < Math.Min(12, Enum.GetNames(setting.ValueType).Length); j++)
        {
            object? button = ById(_document, "rm-enum-" + index + "-" + j);
            Set(Get(button, "style"), "color",
                Enum.GetNames(setting.ValueType)[j] == name
                    ? "#eda60c" : "#d5bd95");
        }
    }
}
