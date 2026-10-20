using System;
using UnityEngine;
using UnityEngine.XR;

namespace OrbusRebornMenu;

/// <summary>
/// First deliberately small, independent VR proof-of-concept.
/// It does not enumerate or edit other plugins yet and never changes Orbus's
/// native journal, PowerUI, avatar rig, spells or inventory UI.
/// </summary>
public sealed class MenuRuntime : MonoBehaviour
{
    public MenuRuntime(IntPtr pointer) : base(pointer) { }

    private GameObject? _panel;
    private GameObject? _rune;
    private GameObject? _cursor;
    private TextMesh? _status;
    private TextMesh? _hint;
    private bool _menuOpen;
    private bool _wasRightPressed;
    private bool _wristVisible;
    private float _nextCameraSearch;
    private Camera? _eyeCamera;
    private bool _warnedFont;
    private bool _wasTracking;
    private bool _startupLogged;
    private bool _previewStarted;
    private float _previewCloseAt;
    private float _nextDiagnosticAt;
    private float _nextErrorLogAt;
    private int _previousJoystickMask;
    private int _joystickReports;
    private bool _loggedWristVisible;

    // Local panel coordinates, in metres before scale.
    private const float HalfWidth = 0.37f;
    private const float HalfHeight = 0.255f;
    private const float RuneRadius = 0.055f;

    private void Start()
    {
        try
        {
            BuildPanel();
            BuildWristRune();
            BuildCursor();
            Plugin.Logger.LogInfo("[RebornMenu] Prototype geometry built; waiting for headset / camera.");
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError("[RebornMenu] Could not create prototype: " + ex);
        }
    }

    private void Update()
    {
        try { Tick(); }
        catch (Exception ex)
        {
            // Prevent one malformed transform or scene transition from
            // crashing the game or taking control of the XR rig.
            if (Time.realtimeSinceStartup >= _nextErrorLogAt)
            {
                _nextErrorLogAt = Time.realtimeSinceStartup + 3f;
                Plugin.Logger.LogWarning("[RebornMenu] Runtime input/frame skipped: " + ex);
            }
        }
    }

    private void Tick()
    {
        bool diagnosticKey = Input.GetKeyDown(KeyCode.Insert);
        bool chordKey = Input.GetKeyDown(KeyCode.M) &&
            (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
            (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        if (diagnosticKey || chordKey)
        {
            Plugin.Logger.LogInfo("[RebornMenu] Keyboard opener detected: " +
                (diagnosticKey ? "Insert" : "Ctrl+Alt+M") +
                " | focused=" + Application.isFocused);
            _previewStarted = true;
            Toggle();
        }

        if (_panel == null || _rune == null) return;
        Camera? eye = FindEyeCamera();
        if (eye == null)
        {
            SetWristVisible(false);
            SetCursorVisible(false);
            return;
        }

        if (!_previewStarted && Plugin.StartupPreview.Value &&
            XRSettings.enabled && XRSettings.isDeviceActive &&
            eye.name == "Camera (eye)")
        {
            _previewStarted = true;
            Recenter(eye);
            _menuOpen = true;
            _panel.SetActive(true);
            _previewCloseAt = Time.realtimeSinceStartup + 25f;
            Plugin.Logger.LogInfo("[RebornMenu] AUTO-OPENED startup preview for 25s" +
                " | eye=" + eye.name + " | panel=" + _panel.transform.position +
                " | eyeMask=0x" + eye.cullingMask.ToString("X8") +
                " | panelLayer=" + _panel.layer);
        }

        if (_previewCloseAt > 0f && Time.realtimeSinceStartup >= _previewCloseAt)
        {
            Plugin.Logger.LogInfo("[RebornMenu] Startup preview complete; closing panel.");
            Close();
        }

        if (_menuOpen)
        {
            _panel.transform.localScale = Vector3.one * Plugin.MenuScale.Value;
            if (!_panel.activeSelf) _panel.SetActive(true);
        }

        if (Time.realtimeSinceStartup >= _nextDiagnosticAt)
        {
            _nextDiagnosticAt = Time.realtimeSinceStartup + 15f;
            Plugin.Logger.LogInfo("[RebornMenu] Status: eye=" + eye.name +
                " | stereo=" + eye.stereoEnabled +
                " | focused=" + Application.isFocused +
                " | XRactive=" + XRSettings.isDeviceActive +
                " | menuOpen=" + _menuOpen + " | wristVisible=" + _wristVisible +
                " | panelLayer=" + _panel.layer + " | eyeMask=0x" +
                eye.cullingMask.ToString("X8"));
        }

        if (!XRSettings.enabled || !XRSettings.isDeviceActive)
        {
            SetWristVisible(false);
            SetCursorVisible(false);
            if (!_startupLogged)
            {
                _startupLogged = true;
                Plugin.Logger.LogInfo("[RebornMenu] Desktop mode: CTRL+ALT+M opens the test parchment.");
            }
            return;
        }

        if (!TryWorldHand(XRNode.LeftHand, eye, out Vector3 leftPos, out Quaternion leftRot))
        {
            SetWristVisible(false);
        }
        else
        {
            Vector3 runePos = leftPos + leftRot * new Vector3(0f, -0.055f, -0.075f);
            Vector3 wristNormal = leftRot * Vector3.down;
            _rune.transform.position = runePos;
            Vector3 towardsHead = eye.transform.position - runePos;
            _rune.transform.rotation = Quaternion.LookRotation(
                towardsHead.normalized, eye.transform.up) *
                Quaternion.Euler(0f, 180f, 0f);

            // First headset test: keep this forgiving. Old implementation
            // assumed a strict controller-local "palm down" direction that
            // can be inverted on Oculus and hide the rune permanently.
            Vector3 toWrist = runePos - eye.transform.position;
            bool inView = toWrist.sqrMagnitude > 0.0001f &&
                          Vector3.Dot(eye.transform.forward, toWrist.normalized) > 0.20f;
            float distance = towardsHead.magnitude;
            SetWristVisible(Plugin.ShowWristRune.Value && inView &&
                distance > 0.09f && distance < 0.90f);
            if (_wristVisible && !_loggedWristVisible)
            {
                _loggedWristVisible = true;
                Plugin.Logger.LogInfo("[RebornMenu] Left wrist rune became visible." +
                    " | dist=" + distance.ToString("0.00") +
                    " | at=" + runePos.ToString());
            }
        }

        bool rightTracking = TryWorldHand(XRNode.RightHand, eye,
            out Vector3 rightPos, out Quaternion rightRot);
        if (rightTracking && !_wasTracking)
        {
            _wasTracking = true;
            Plugin.Logger.LogInfo("[RebornMenu] Legacy XR left/right tracking available.");
        }

        // Record *actual* joystick button transitions for this Oculus runtime.
        // v0.1 assumed button 15 is the trigger without evidence.
        int pressedMask = 0;
        for (int i = 0; i < 20; i++)
        {
            if (Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 + i)))
                pressedMask |= 1 << i;
        }
        int newPresses = pressedMask & ~_previousJoystickMask;
        _previousJoystickMask = pressedMask;
        if (newPresses != 0 && _joystickReports < 24)
        {
            _joystickReports++;
            Plugin.Logger.LogInfo("[RebornMenu] Joystick button down: mask=0x" +
                newPresses.ToString("X8") + " | indices=" +
                DescribeButtons(newPresses) +
                " | configuredTrigger=" + Plugin.RightTriggerButton.Value);
        }
        bool pressed = Application.isFocused &&
            Input.GetKey((KeyCode)((int)KeyCode.JoystickButton0 +
                                    Plugin.RightTriggerButton.Value));
        bool tapped = pressed && !_wasRightPressed;
        _wasRightPressed = pressed;

        if (!rightTracking)
        {
            SetCursorVisible(false);
            return;
        }

        Vector3 aim = (rightRot * Quaternion.Euler(
            Plugin.PointerPitch.Value, 0f, 0f)) * Vector3.forward;
        if (_wristVisible && _rune.activeSelf && AimSphere(rightPos, aim,
                _rune.transform.position, RuneRadius) && tapped)
        {
            Toggle();
            // Require a new trigger press before clicking the open panel.
            SetCursorVisible(false);
            return;
        }

        if (_menuOpen && _panel.activeSelf &&
            TryHitPanel(rightPos, aim, out Vector3 localHit, out Vector3 worldHit))
        {
            if (_cursor != null)
            {
                _cursor.transform.position = worldHit + _panel.transform.forward * 0.008f;
                _cursor.transform.rotation = _panel.transform.rotation;
                SetCursorVisible(true);
            }
            if (tapped)
            {
                // Deliberately huge test hitboxes, independent of game UI.
                // Closing is top-right, recenter is lower-left.
                if (localHit.x > 0.235f && localHit.y > 0.135f)
                {
                    Close();
                }
                else if (localHit.x < -0.125f && localHit.y < -0.145f)
                {
                    Recenter(eye);
                    if (_status != null) _status.text = "Panel recentered";
                }
                else if (localHit.x > -0.13f && localHit.x < 0.18f &&
                         localHit.y > -0.105f && localHit.y < -0.018f)
                {
                    if (_status != null) _status.text = "VR pointer works!";
                    Plugin.Logger.LogInfo("[RebornMenu] Verified a VR press inside the prototype panel.");
                }
            }
        }
        else
        {
            SetCursorVisible(false);
        }
    }

    private Camera? FindEyeCamera()
    {
        if (_eyeCamera != null && _eyeCamera.isActiveAndEnabled &&
            _eyeCamera.name == "Camera (eye)")
            return _eyeCamera;
        // Bootstrap cameras can be active before the player's real VR rig.
        // Retry instead of caching a stereo bootstrap camera forever.
        if (Time.realtimeSinceStartup < _nextCameraSearch)
            return _eyeCamera != null && _eyeCamera.isActiveAndEnabled ? _eyeCamera : null;
        _nextCameraSearch = Time.realtimeSinceStartup + 1f;
        _eyeCamera = null;

        Camera[] available = Camera.allCameras;
        foreach (Camera cam in available)
        {
            if (cam != null && cam.name == "Camera (eye)" &&
                cam.isActiveAndEnabled && cam.stereoEnabled)
            {
                _eyeCamera = cam;
                break;
            }
        }

        if (_eyeCamera == null)
        {
            foreach (Camera cam in available)
            {
                if (cam != null && cam.isActiveAndEnabled && cam.stereoEnabled)
                {
                    _eyeCamera = cam;
                    break;
                }
            }
        }

        if (_eyeCamera == null && (!XRSettings.enabled || !XRSettings.isDeviceActive))
            _eyeCamera = Camera.main;
        return _eyeCamera;
    }

    private static bool TryWorldHand(XRNode node, Camera eye,
        out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        Vector3 local = InputTracking.GetLocalPosition(node);
        Quaternion localRotation = InputTracking.GetLocalRotation(node);
        if (local.sqrMagnitude < 0.000001f &&
            Quaternion.Angle(localRotation, Quaternion.identity) < 0.01f)
            return false;

        // Unity 2018 legacy XR returns poses relative to tracking origin.
        // Align that origin to Orbus's REAL VR eye transform. We do not
        // parent to, move, or override the game XR rig.
        Vector3 headLocal = InputTracking.GetLocalPosition(XRNode.Head);
        Quaternion headRotation = InputTracking.GetLocalRotation(XRNode.Head);
        Quaternion originRotation = eye.transform.rotation *
                                    Quaternion.Inverse(headRotation);
        Vector3 originPosition = eye.transform.position -
                                 originRotation * headLocal;

        position = originPosition + originRotation * local;
        rotation = originRotation * localRotation;
        return true;
    }

    private static string DescribeButtons(int mask)
    {
        string result = "";
        for (int i = 0; i < 20; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            if (result.Length > 0) result += ",";
            result += i.ToString();
        }
        return result;
    }

    private void Toggle()
    {
        if (_menuOpen)
        {
            Close();
            return;
        }
        Camera? eye = FindEyeCamera();
        if (eye == null)
        {
            Plugin.Logger.LogWarning("[RebornMenu] Cannot open: no working camera yet.");
            return;
        }
        _previewCloseAt = 0f;
        Recenter(eye);
        _menuOpen = true;
        _panel?.SetActive(true);
        if (_status != null) _status.text = "World-space panel opened";
        Plugin.Logger.LogInfo("[RebornMenu] OPENED test parchment.");
    }

    private void Close()
    {
        _menuOpen = false;
        _previewCloseAt = 0f;
        _panel?.SetActive(false);
        SetCursorVisible(false);
        Plugin.Logger.LogInfo("[RebornMenu] CLOSED test parchment.");
    }

    private void Recenter(Camera eye)
    {
        if (_panel == null) return;
        Transform view = eye.transform;
        Vector3 position = view.position + view.forward * Plugin.MenuDistance.Value;
        position -= view.up * 0.025f;
        Vector3 toEye = view.position - position;
        _panel.transform.position = position;
        // Matches the front-facing TextMesh orientation used by CastMetrics.
        _panel.transform.rotation = Quaternion.LookRotation(toEye, Vector3.up) *
                                    Quaternion.Euler(0f, 180f, 0f);
        _panel.transform.localScale = Vector3.one * Plugin.MenuScale.Value;
    }

    private static bool AimSphere(Vector3 rayOrigin, Vector3 direction,
        Vector3 point, float radius)
    {
        Vector3 offset = point - rayOrigin;
        float t = Vector3.Dot(offset, direction);
        if (t < 0.05f || t > 0.8f) return false;
        return (offset - direction * t).sqrMagnitude <= radius * radius;
    }

    private bool TryHitPanel(Vector3 rayOrigin, Vector3 direction,
        out Vector3 local, out Vector3 world)
    {
        local = Vector3.zero;
        world = Vector3.zero;
        if (_panel == null) return false;
        Vector3 normal = _panel.transform.forward;
        float denominator = Vector3.Dot(normal, direction);
        if (Mathf.Abs(denominator) < 0.01f) return false;

        float t = Vector3.Dot(_panel.transform.position - rayOrigin, normal) /
                  denominator;
        if (t < 0.06f || t > 2f) return false;
        world = rayOrigin + direction * t;
        local = _panel.transform.InverseTransformPoint(world);
        return Mathf.Abs(local.x) <= HalfWidth && Mathf.Abs(local.y) <= HalfHeight;
    }

    private void SetWristVisible(bool visible)
    {
        _wristVisible = visible;
        if (_rune != null && _rune.activeSelf != visible) _rune.SetActive(visible);
    }

    private void SetCursorVisible(bool visible)
    {
        if (_cursor != null && _cursor.activeSelf != visible) _cursor.SetActive(visible);
    }

    private void BuildPanel()
    {
        _panel = new GameObject("RebornMenu.PrototypeParchment");
        UnityEngine.Object.DontDestroyOnLoad(_panel);
        CreateRect(_panel.transform, "Parchment", Vector3.zero,
            new Vector2(0.76f, 0.53f), new Color(0.62f, 0.53f, 0.41f));
        CreateRect(_panel.transform, "OuterRim", new Vector3(0f, 0f, 0.002f),
            new Vector2(0.78f, 0.55f), new Color(0.27f, 0.19f, 0.13f));
        CreateRect(_panel.transform, "Paper", new Vector3(0f, 0f, 0.004f),
            new Vector2(0.745f, 0.514f), new Color(0.64f, 0.56f, 0.45f));
        CreateRect(_panel.transform, "HeaderTab", new Vector3(-0.18f, 0.223f, 0.008f),
            new Vector2(0.24f, 0.044f), new Color(0.30f, 0.22f, 0.15f));
        CreateRect(_panel.transform, "CloseTab", new Vector3(0.301f, 0.222f, 0.008f),
            new Vector2(0.093f, 0.044f), new Color(0.30f, 0.22f, 0.15f));
        CreateRect(_panel.transform, "TestTab", new Vector3(0.04f, -0.075f, 0.008f),
            new Vector2(0.31f, 0.071f), new Color(0.38f, 0.28f, 0.19f));
        CreateRect(_panel.transform, "RecenterTab", new Vector3(-0.242f, -0.191f, 0.008f),
            new Vector2(0.225f, 0.05f), new Color(0.30f, 0.22f, 0.15f));
        CreateText(_panel.transform, "Title", "MODS", new Vector3(-0.277f, 0.241f, 0.015f),
            36, new Color(0.94f, 0.86f, 0.69f));
        CreateText(_panel.transform, "CloseText", "X CLOSE", new Vector3(0.26f, 0.24f, 0.015f),
            22, new Color(0.94f, 0.86f, 0.69f));
        CreateText(_panel.transform, "Body",
            "REBORN MENU    v0.1.1\n\n" +
            "Orbus-style interface test\n" +
            "VR settings pages coming next.\n\n" +
            "Left wrist rune: open / close",
            new Vector3(-0.33f, 0.16f, 0.015f), 28,
            new Color(0.13f, 0.10f, 0.075f));
        CreateText(_panel.transform, "TestText", "[ Test VR pointer ]",
            new Vector3(-0.085f, -0.058f, 0.015f), 26,
            new Color(0.95f, 0.86f, 0.70f));
        CreateText(_panel.transform, "RecenterText", "RECENTER",
            new Vector3(-0.329f, -0.178f, 0.015f), 23,
            new Color(0.95f, 0.86f, 0.70f));
        _status = CreateText(_panel.transform, "Status", "Ready",
            new Vector3(-0.33f, -0.136f, 0.016f), 23,
            new Color(0.28f, 0.19f, 0.12f));
        _hint = CreateText(_panel.transform, "Hint",
            "Insert / Ctrl+Alt+M  |  VR trigger",
            new Vector3(-0.080f, -0.181f, 0.016f), 21,
            new Color(0.28f, 0.19f, 0.12f));
        _panel.SetActive(false);
    }

    private void BuildWristRune()
    {
        _rune = new GameObject("RebornMenu.LeftWristRune");
        UnityEngine.Object.DontDestroyOnLoad(_rune);
        CreateRect(_rune.transform, "RuneBack", Vector3.zero,
            new Vector2(0.068f, 0.068f), new Color(0.25f, 0.17f, 0.11f));
        CreateRect(_rune.transform, "RuneFace", new Vector3(0f, 0f, 0.002f),
            new Vector2(0.057f, 0.057f), new Color(0.42f, 0.31f, 0.20f));
        CreateText(_rune.transform, "RuneLetter", "M",
            new Vector3(-0.015f, 0.02f, 0.01f), 39, new Color(0.95f, 0.8f, 0.48f));
        _rune.SetActive(false);
    }

    private void BuildCursor()
    {
        _cursor = new GameObject("RebornMenu.PointerHit");
        UnityEngine.Object.DontDestroyOnLoad(_cursor);
        CreateRect(_cursor.transform, "Cursor", Vector3.zero,
            new Vector2(0.012f, 0.012f), new Color(0.98f, 0.84f, 0.37f));
        _cursor.SetActive(false);
    }

    private static void CreateRect(Transform parent, string name,
        Vector3 localPosition, Vector2 size, Color colour)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = "RebornMenu." + name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = localPosition;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);
        Collider? collider = obj.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        Renderer? renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        Shader? shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        var material = new Material(shader);
        material.color = colour;
        renderer.material = material;
    }

    private TextMesh CreateText(Transform parent, string name, string value,
        Vector3 position, int fontSize, Color colour)
    {
        var go = new GameObject("RebornMenu." + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var text = go.AddComponent<TextMesh>();
        text.anchor = TextAnchor.UpperLeft;
        text.alignment = TextAlignment.Left;
        text.fontSize = fontSize;
        text.characterSize = 0.0010f;
        text.fontStyle = FontStyle.Normal;
        text.color = colour;
        text.text = value;
        try
        {
            Font? font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null)
            {
                text.font = font;
                MeshRenderer? renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.material = font.material;
            }
        }
        catch (Exception ex)
        {
            if (!_warnedFont)
            {
                _warnedFont = true;
                Plugin.Logger.LogWarning("[RebornMenu] Font fallback: " + ex.Message);
            }
        }
        return text;
    }

    private void OnDestroy()
    {
        _menuOpen = false;
        if (_panel != null) UnityEngine.Object.Destroy(_panel);
        if (_rune != null) UnityEngine.Object.Destroy(_rune);
        if (_cursor != null) UnityEngine.Object.Destroy(_cursor);
    }
}
