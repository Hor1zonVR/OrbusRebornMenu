using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace OrbusRebornMenu;

/// <summary>
/// Experimental, self-contained entry inside Orbus's existing Settings
/// PowerUI. No wrist geometry, new Unity Canvas, XR/controller input, game
/// assets or permanent file changes.
///
/// The pure transformer runs only on narrowly named Settings fragments.
/// We register callbacks ONLY on the injected element, through the native
/// pointer/PowerUI event pipeline. Unknown layouts remain untouched.
/// </summary>
internal static class NativeModsSettingsBridge
{
    private const string HarmonyId = "com.horizon.orbus.rebornmenu.nativesettings";
    private const BindingFlags Flags =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static;

    private static Harmony? _patch;
    private static ManualLogSource? _log;
    private static bool _disabled;
    private static int _extensions;
    private static int _registered;
    private static int _failedBindings;
    private static readonly HashSet<long> BoundPointers = new();
    private static readonly List<object> KeepAlive = new();

    internal static void Start(ManualLogSource log)
    {
        _log = log;
        if (!Plugin.EnableNativePrototype.Value)
        {
            _log.LogInfo("[RebornMenu] Native Settings prototype disabled by config.");
            return;
        }

        Type? legacy = FindType("PowerUI.Element");
        Type? modern = FindType("Dom.Element");
        Type? htmlDoc = FindType("PowerUI.HtmlDocument");
        Type? selected = legacy ?? (htmlDoc != null ? modern : null);
        if (selected == null)
        {
            _log.LogWarning("[RebornMenu] No supported PowerUI Settings renderer. " +
                "Native UI left unchanged; no custom floating panel will appear.");
            return;
        }

        MethodInfo? setter = selected.GetMethods(Flags).FirstOrDefault(m =>
            m.Name == "set_innerHTML" && !m.IsStatic &&
            m.ReturnType == typeof(void) && m.GetParameters().Length == 1 &&
            m.GetParameters()[0].ParameterType == typeof(string));

        if (setter == null)
        {
            _log.LogWarning("[RebornMenu] No recognised native HTML setter on " +
                selected.FullName + ". Settings left unchanged.");
            return;
        }

        try
        {
            var prefix = typeof(NativeModsSettingsBridge).GetMethod(
                nameof(BeforeSetHtml), BindingFlags.Static | BindingFlags.NonPublic);
            var postfix = typeof(NativeModsSettingsBridge).GetMethod(
                nameof(AfterSetHtml), BindingFlags.Static | BindingFlags.NonPublic);
            if (prefix == null || postfix == null) throw new MissingMethodException();

            _patch = new Harmony(HarmonyId);
            _patch.Patch(setter, prefix: new HarmonyMethod(prefix),
                postfix: new HarmonyMethod(postfix));
            _log.LogInfo("[RebornMenu] Native Settings v0.2 experimental hook ready: " +
                selected.FullName + ".set_innerHTML. " +
                "Only recognised Settings content can be extended.");
        }
        catch (Exception ex)
        {
            _log.LogWarning("[RebornMenu] PowerUI hook unavailable: " +
                ex.GetType().Name + " " + ex.Message);
            SafeDisable();
        }
    }

    private static Type? FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type? found = assembly.GetType(fullName, false);
                if (found != null) return found;
            }
            catch { }
        }
        return null;
    }

    private static object? Member(object? owner, string name)
    {
        if (owner == null) return null;
        try
        {
            Type type = owner is Type t ? t : owner.GetType();
            object? obj = owner is Type ? null : owner;
            var p = type.GetProperty(name, Flags);
            if (p != null && p.GetIndexParameters().Length == 0)
                return p.GetValue(obj);
            var field = type.GetField(name, Flags);
            if (field != null) return field.GetValue(obj);
            return type.GetMethod("get_" + name, Flags, null,
                Type.EmptyTypes, null)?.Invoke(obj, null);
        }
        catch { return null; }
    }

    private static bool Put(object? owner, string name, object value)
    {
        if (owner == null) return false;
        try
        {
            Type type = owner.GetType();
            PropertyInfo? property = type.GetProperty(name, Flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(owner, value);
                return true;
            }
            MethodInfo? setter = type.GetMethods(Flags).FirstOrDefault(m =>
                m.Name == "set_" + name && m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType.IsInstanceOfType(value));
            if (setter == null) return false;
            setter.Invoke(owner, new[] { value });
            return true;
        }
        catch { return false; }
    }

    private static string ElementId(object element)
    {
        try { return Member(element, "id")?.ToString() ?? ""; }
        catch { return ""; }
    }

    private static void BeforeSetHtml(object __instance, ref string __0)
    {
        if (_disabled || !Plugin.EnableNativePrototype.Value ||
            __instance == null || string.IsNullOrEmpty(__0)) return;

        try
        {
            string id = ElementId(__instance);
            if (NativeSettingsMarkup.TryExtend(id, __0,
                out string extended, out _))
            {
                __0 = extended;
                _extensions++;
                if (_extensions <= 5)
                    _log?.LogInfo("[RebornMenu] Added native Mods entry to " +
                        id + " (preview #" + _extensions + ").");
            }
        }
        catch (Exception ex)
        {
            // No exception may escape into Orbus's native HTML renderer.
            _log?.LogWarning("[RebornMenu] Native Settings extension skipped: " +
                ex.GetType().Name);
            _disabled = true;
        }
    }

    private static void AfterSetHtml(object __instance, string __0)
    {
        if (_disabled || !Plugin.EnableNativePrototype.Value ||
            __instance == null || string.IsNullOrEmpty(__0) ||
            !__0.Contains(NativeSettingsMarkup.Marker, StringComparison.Ordinal))
            return;

        try
        {
            object? document = Member(__instance, "ownerDocument") ??
                               Member(__instance, "document");
            object? tab = ById(document, NativeSettingsMarkup.TabId) ??
                          DescendantById(__instance, NativeSettingsMarkup.TabId);
            object? panel = ById(document, NativeSettingsMarkup.PanelId) ??
                            DescendantById(__instance, NativeSettingsMarkup.PanelId);
            if (tab == null || panel == null)
            {
                if (_failedBindings++ < 4)
                    _log?.LogWarning("[RebornMenu] Mods HTML rendered but native " +
                        "tab/page nodes could not be resolved. No input intercepted.");
                return;
            }

            long identity = PointerId(tab);
            if (!BoundPointers.Add(identity)) return;
            if (BoundPointers.Count > 64)
            {
                _log?.LogWarning("[RebornMenu] Too many native DOM rebuilds; " +
                    "suspending enhancement for this session.");
                _disabled = true;
                return;
            }

            bool opened = false;
            Action openPage = () =>
            {
                try
                {
                    opened = !opened;
                    if (!Put(Member(panel, "style"), "display",
                             opened ? "block" : "none"))
                        _log?.LogWarning("[RebornMenu] Native page display setter unavailable.");
                }
                catch (Exception ex)
                {
                    _log?.LogWarning("[RebornMenu] Settings click ignored: " +
                        ex.GetType().Name);
                }
            };

            if (TryBindNativeClick(tab, openPage, out string error))
            {
                _registered++;
                _log?.LogInfo("[RebornMenu] Native VR Mods button wired (" +
                    _registered + "). Interact with it using Orbus's normal laser.");
            }
            else
            {
                // The tab must never be a mystery dead button. Reveal its
                // safe read-only page inline when native event binding fails.
                Put(Member(panel, "style"), "display", "block");
                if (_failedBindings++ < 4)
                    _log?.LogWarning("[RebornMenu] Native click binding unsupported: " +
                        error + ". Showing inline fallback inside Settings.");
            }
        }
        catch (Exception ex)
        {
            if (_failedBindings++ < 4)
                _log?.LogWarning("[RebornMenu] Failed to wire Mods page: " +
                    ex.GetType().Name + " " + ex.Message);
        }
    }

    private static object? ById(object? document, string id)
    {
        if (document == null) return null;
        try
        {
            MethodInfo? method = document.GetType().GetMethods(Flags)
                .FirstOrDefault(m => (m.Name == "getElementById" ||
                                      m.Name == "GetElementById") &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType ==
                                     typeof(string));
            return method?.Invoke(document, new object[] { id });
        }
        catch { return null; }
    }

    private static object? DescendantById(object root, string id)
    {
        // Read only our fresh HTML's small subtree; never crawl the player's
        // inventory/journal content or other clients' UI nodes.
        var stack = new Stack<object>();
        stack.Push(root);
        int count = 0;
        while (stack.Count > 0 && ++count < 150)
        {
            object item = stack.Pop();
            if (ElementId(item) == id) return item;
            object? child = Member(item, "firstChild");
            int siblings = 0;
            while (child != null && ++siblings <= 80)
            {
                stack.Push(child);
                child = Member(child, "nextSibling");
            }
        }
        return null;
    }

    private static long PointerId(object node)
    {
        try
        {
            object? pointer = Member(node, "Pointer");
            if (pointer is IntPtr p && p != IntPtr.Zero) return p.ToInt64();
        }
        catch { }
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(node);
    }

    private static bool TryBindNativeClick(object element, Action callback,
        out string failure)
    {
        failure = "No compatible native click event";
        const BindingFlags Bind = BindingFlags.Public | BindingFlags.NonPublic |
                                  BindingFlags.Instance;
        var methods = element.GetType().GetMethods(Bind)
            .Where(m => m.Name == "add_OnMouseDown" ||
                        m.Name == "addEventListener")
            .Where(m => m.Name == "add_OnMouseDown"
                ? m.GetParameters().Length == 1
                : (m.GetParameters().Length == 2 ||
                   m.GetParameters().Length == 3) &&
                   m.GetParameters()[0].ParameterType == typeof(string))
            .OrderBy(m => m.Name == "addEventListener" ? 0 : 1)
            .ToArray();

        foreach (MethodInfo method in methods)
        {
            try
            {
                bool modern = method.Name == "addEventListener";
                ParameterInfo[] parameters = method.GetParameters();
                Type nativeDelegateType = parameters[modern ? 1 : 0].ParameterType;
                MethodInfo? invoke = nativeDelegateType.GetMethod("Invoke");
                if (invoke == null || invoke.ReturnType != typeof(void) ||
                    invoke.GetParameters().Length > 1) continue;

                ParameterInfo[] callbackParameters = invoke.GetParameters();
                if (modern && callbackParameters.Length == 1)
                {
                    string full = callbackParameters[0].ParameterType.FullName ?? "";
                    if (full != "Dom.Event" && full != "Dom.MouseEvent" &&
                        full != "Dom.UIEvent" && full != "Dom.PointerEvent")
                        continue;
                }

                Delegate managed;
                if (callbackParameters.Length == 0) managed = callback;
                else
                {
                    ParameterExpression parameter = Expression.Parameter(
                        callbackParameters[0].ParameterType, "nativeEvent");
                    Expression call = Expression.Call(
                        Expression.Constant(callback), typeof(Action).GetMethod(
                            nameof(Action.Invoke))!);
                    managed = Expression.Lambda(
                        typeof(Action<>).MakeGenericType(
                            callbackParameters[0].ParameterType),
                        call, parameter).Compile();
                }

                object? converted = null;
                foreach (MethodInfo support in typeof(DelegateSupport)
                    .GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "ConvertDelegate" &&
                                m.IsGenericMethodDefinition &&
                                m.GetParameters().Length == 1))
                {
                    try
                    {
                        object? test = support.MakeGenericMethod(nativeDelegateType)
                            .Invoke(null, new object[] { managed });
                        if (test != null &&
                            nativeDelegateType.IsInstanceOfType(test))
                        {
                            converted = test;
                            break;
                        }
                    }
                    catch { }
                }
                if (converted == null) continue;

                if (modern)
                {
                    // Prefer click. Register at most one event here to avoid
                    // double toggles when both click and mousedown fire.
                    object[] args = parameters.Length == 3
                        ? new object[] { "click", converted, false }
                        : new object[] { "click", converted };
                    method.Invoke(element, args);
                }
                else method.Invoke(element, new[] { converted });

                KeepAlive.Add(callback);
                KeepAlive.Add(managed);
                KeepAlive.Add(converted);
                failure = "";
                return true;
            }
            catch (Exception ex)
            {
                failure = (ex.InnerException ?? ex).GetType().Name;
            }
        }
        return false;
    }

    private static void SafeDisable()
    {
        _disabled = true;
        try { _patch?.UnpatchSelf(); }
        catch { }
        _patch = null;
    }
}
