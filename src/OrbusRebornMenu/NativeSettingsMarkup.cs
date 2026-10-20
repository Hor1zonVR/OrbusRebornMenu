using System;
using System.Text.RegularExpressions;

namespace OrbusRebornMenu;

/// <summary>
/// Conservative, testable native-Settings HTML extension.
/// Never reparents or removes any game-owned DOM nodes. If the native
/// Settings target is not recognisable, leaves the entire original
/// string byte-for-byte unchanged.
/// </summary>
public static class NativeSettingsMarkup
{
    public const string Marker = "<!-- RebornMenu:NativeSettings:v1 -->";
    public const string TabId = "rebornmenu-settings-tab";
    public const string PanelId = "rebornmenu-settings-page";

    private static readonly Regex ExactSettingsId = new(
        @"^(?:settings|settings-(?:container|content|page|panel|tab)|(?:options|preferences)-(?:container|content|page|panel))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    // HTML needs to be an ordinary settings content fragment, not a chat,
    // inventory, journal, mission or account-authentication document.
    public static bool TryExtend(string? targetId, string? html,
        out string result, out string reason)
    {
        result = html ?? string.Empty;
        reason = "Target is not an explicitly recognised Settings content node";
        if (string.IsNullOrWhiteSpace(targetId) ||
            !ExactSettingsId.IsMatch(targetId)) return false;

        reason = "Settings HTML is missing or outside safe bounds";
        if (html == null || html.Length < 60 || html.Length > 250_000)
            return false;

        reason = "Already extended";
        if (html.Contains(Marker, StringComparison.Ordinal)) return false;

        reason = "Native fragment does not look like a normal settings panel";
        if (!html.Contains("<div", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("<table", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("<ul", StringComparison.OrdinalIgnoreCase))
            return false;

        // An id match is not sufficient if a client uses the same node for
        // data listings. Avoid an accidental overlay on a different page.
        reason = "Suspiciously mixed document; refusing injection";
        if (html.Contains("data-missionid-", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("vendor-mission", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("<script", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("<iframe", StringComparison.OrdinalIgnoreCase))
            return false;

        const string block =
            "\n<!-- RebornMenu:NativeSettings:v1 -->" +
            "<div id=\"rebornmenu-extension\" style=\"margin-top:11px;padding:7px;\">" +
            "<div id=\"rebornmenu-settings-tab\" style=\"display:inline-block;" +
                "padding:8px 17px;background-color:#60482f;color:#e5d8bc;" +
                "border:2px solid #8b724b;font-size:18px;cursor:pointer;\">" +
                "Mods" +
            "</div>" +
            "<div id=\"rebornmenu-settings-page\" style=\"display:none;" +
                "background-color:#998770;border:2px solid #5a4129;" +
                "color:#231b14;padding:12px;margin-top:8px;font-size:16px;\">" +
                "<div style=\"font-size:22px;font-weight:bold;\">RebornMenu</div>" +
                "<p>Native Settings integration test</p>" +
                "<p>Your mod configuration pages will appear here.</p>" +
                "<p>No game settings have been changed.</p>" +
            "</div>" +
            "</div>";

        result = html + block;
        reason = "Extended exactly one recognised Settings HTML fragment";
        return true;
    }
}
