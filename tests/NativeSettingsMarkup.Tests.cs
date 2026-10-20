using OrbusRebornMenu;

static void Expect(bool ok, string message)
{
    if (!ok) throw new Exception("FAIL: " + message);
}
const string settings = "<div class='config-group'><div class='game-setting'>Game Audio Settings</div></div>";
Expect(NativeSettingsMarkup.TryExtend("settings-container", settings,
    out string extended, out _), "Recognised Settings fragment should extend");
Expect(extended.StartsWith(settings, StringComparison.Ordinal),
    "Original Settings HTML must remain byte-for-byte unchanged");
Expect(extended.Contains(NativeSettingsMarkup.Marker),
    "Extension must be marked to prevent repeated insertion");
Expect(extended.Contains("id=\"" + NativeSettingsMarkup.TabId + "\"") &&
       extended.Contains("id=\"" + NativeSettingsMarkup.PanelId + "\""),
    "Native Mods tab and content must have stable ids");
Expect(!NativeSettingsMarkup.TryExtend("settings-container", extended,
    out string repeated, out _) && repeated == extended,
    "Repeated injections must be blocked without altering markup");
Expect(!NativeSettingsMarkup.TryExtend("missions-container", settings,
    out string mission, out _) && mission == settings,
    "The Missions page must remain untouched");
Expect(!NativeSettingsMarkup.TryExtend("settings-container",
    settings + "<div data-missionid-42='42'>Quest</div>",
    out string mixed, out _) && mixed.EndsWith("Quest</div>"),
    "Unexpected mission content must fail closed");
Expect(!NativeSettingsMarkup.TryExtend("settings-container",
    settings + "<script>untrusted()</script>",
    out _, out _), "Script-like content must be rejected");
Expect(!NativeSettingsMarkup.TryExtend("settings-container",
    new string('x', 250001), out _, out _),
    "Oversized input must not be transformed");
Expect(!NativeSettingsMarkup.TryExtend("chat", settings, out _, out _),
    "Unrelated UI fragments must not be changed");
Expect(!NativeSettingsMarkup.TryExtend("settings-container", "<p>No groups</p>",
    out _, out _), "Unknown native layout must fail closed");
Console.WriteLine("PASS: recognized Settings-only extension, original preservation, " +
    "idempotence, rejection of unrelated/suspicious/oversized HTML.");
