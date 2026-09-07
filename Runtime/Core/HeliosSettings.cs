using System;
using UnityEngine;

namespace HeliosDebugger
{
    public enum HeliosTriggerCorner
    {
        BottomRight,
        BottomLeft,
        TopRight,
        TopLeft
    }

    public enum HeliosTriggerActivation
    {
        SingleTap,
        DoubleTap,
        TripleTap,
        TapAndHold
    }

    public enum HeliosCanvasPlacement
    {
        ScreenSpaceOverlay,
        ScreenSpaceCamera,
        WorldSpace
    }

    [CreateAssetMenu(fileName = "HeliosDebuggerSettings", menuName = "Astra/Helios/Settings")]
    public sealed class HeliosDebuggerSettings : ScriptableObject
    {
        [Header("Availability")]
        [SerializeField] private bool _autoBootstrap = true;
        [SerializeField] private bool _developmentBuildOnly = true;
        [SerializeField] private bool _visibleAtStartup;

        [Header("Input")]
        [SerializeField] private bool _showTrigger = true;
        [SerializeField] private HeliosTriggerCorner _triggerCorner = HeliosTriggerCorner.BottomRight;
        [SerializeField] private HeliosTriggerActivation _triggerActivation = HeliosTriggerActivation.TripleTap;
        [SerializeField] private float _tripleTapWindow = 0.8f;
        [SerializeField] private float _triggerHoldDuration = 0.75f;
        [SerializeField] private Vector2 _triggerOffset = new Vector2(18f, 18f);
        [SerializeField] private Vector2 _triggerSize = new Vector2(54f, 54f);
        [SerializeField] private string _triggerLabel = "H";
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote;
        [SerializeField] private bool _closeOnEscape = true;
        [SerializeField] private bool _enableGamepadCombo = true;
        [SerializeField] private bool _enableKonamiCode = true;

        [Header("Layout")]
        [SerializeField] private string _defaultTabId = "HeliosDebugger.HeliosConsoleTab";
        [SerializeField] private bool _rememberLastTab = true;
        [SerializeField] private bool _showPinnedOverlay = true;
        [SerializeField] private bool _showDockedConsole;
        [SerializeField] private bool _showDockedProfiler;
        [SerializeField] private HeliosCanvasPlacement _canvasPlacement = HeliosCanvasPlacement.ScreenSpaceOverlay;
        [SerializeField] private float _worldSpaceScale = 0.001f;
        [SerializeField, Range(0.2f, 1f)] private float _panelOpacity = 0.97f;
        [SerializeField] private HeliosThemeProfile _theme;

        [Header("Data")]
        [SerializeField] private int _logCapacity = 500;
        [SerializeField] private int _profilerHistoryCapacity = 240;
        [SerializeField] private float _profilerRefreshInterval = 0.25f;

        [Header("Bug Reports")]
        [SerializeField] private string _webhookUrl;
        [SerializeField] private int _maxReportBytes = 5 * 1024 * 1024;
        [SerializeField] private string _defaultReportTransportId = "local.export";

        [Header("Access")]
        [SerializeField] private bool _requirePin;
        [SerializeField] private string _pinSalt;
        [SerializeField] private string _pinHash;
        [SerializeField] private float _pinSessionMinutes = 15f;

        [Header("Release")]
        [SerializeField] private bool _allowInReleaseBuild;
        [SerializeField] private bool _strictReleaseValidation = true;

        public bool AutoBootstrap => _autoBootstrap;
        public bool DevelopmentBuildOnly => _developmentBuildOnly;
        public bool VisibleAtStartup => _visibleAtStartup;
        public bool ShowTrigger => _showTrigger;
        public HeliosTriggerCorner TriggerCorner => _triggerCorner;
        public HeliosTriggerActivation TriggerActivation => _triggerActivation;
        public float TripleTapWindow => Mathf.Max(0.2f, _tripleTapWindow);
        public float TriggerHoldDuration => Mathf.Max(0.2f, _triggerHoldDuration);
        public Vector2 TriggerOffset => _triggerOffset;
        public Vector2 TriggerSize => new Vector2(Mathf.Max(32f, _triggerSize.x), Mathf.Max(32f, _triggerSize.y));
        public string TriggerLabel => string.IsNullOrWhiteSpace(_triggerLabel) ? "H" : _triggerLabel;
        public KeyCode ToggleKey => _toggleKey;
        public bool CloseOnEscape => _closeOnEscape;
        public bool EnableGamepadCombo => _enableGamepadCombo;
        public bool EnableKonamiCode => _enableKonamiCode;
        public string DefaultTabId => _defaultTabId;
        public bool RememberLastTab => _rememberLastTab;
        public bool ShowPinnedOverlay => _showPinnedOverlay;
        public bool ShowDockedConsole => _showDockedConsole;
        public bool ShowDockedProfiler => _showDockedProfiler;
        public HeliosCanvasPlacement CanvasPlacement => _canvasPlacement;
        public float WorldSpaceScale => Mathf.Max(0.00001f, _worldSpaceScale);
        public float PanelOpacity => Mathf.Clamp(_panelOpacity, 0.2f, 1f);
        public HeliosThemeProfile Theme => _theme;
        public int LogCapacity => Mathf.Max(32, _logCapacity);
        public int ProfilerHistoryCapacity => Mathf.Max(30, _profilerHistoryCapacity);
        public float ProfilerRefreshInterval => Mathf.Max(0.05f, _profilerRefreshInterval);
        public string WebhookUrl => _webhookUrl;
        public int MaxReportBytes => Mathf.Max(1024, _maxReportBytes);
        public string DefaultReportTransportId => string.IsNullOrWhiteSpace(_defaultReportTransportId)
            ? "local.export"
            : _defaultReportTransportId;
        public bool RequirePin => _requirePin;
        public string PinSalt => _pinSalt;
        public string PinHash => _pinHash;
        public float PinSessionMinutes => Mathf.Max(0.1f, _pinSessionMinutes);
        public bool AllowInReleaseBuild => _allowInReleaseBuild;
        public bool StrictReleaseValidation => _strictReleaseValidation;

#if UNITY_EDITOR
        public void SetPinCredentials(string salt, string hash)
        {
            _pinSalt = salt ?? string.Empty;
            _pinHash = hash ?? string.Empty;
            // Saving credentials without requiring them silently disables the
            // challenge; configuring a PIN implies it should be enforced.
            _requirePin = !string.IsNullOrEmpty(_pinSalt) && !string.IsNullOrEmpty(_pinHash);
        }
#endif

        public static HeliosDebuggerSettings LoadOrDefault()
        {
            HeliosDebuggerSettings settings = Resources.Load<HeliosDebuggerSettings>("HeliosDebuggerSettings");
            return settings != null ? settings : CreateRuntimeDefault();
        }

        public static HeliosDebuggerSettings CreateRuntimeDefault()
        {
            HeliosDebuggerSettings settings = CreateInstance<HeliosDebuggerSettings>();
            settings.name = "HeliosDebuggerSettings_RuntimeDefault";
            return settings;
        }
    }

    [Serializable]
    public sealed class HeliosSerializablePair
    {
        [SerializeField] private string _key;
        [SerializeField] private string _value;

        public HeliosSerializablePair(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public string Key => _key;
        public string Value => _value;
    }
}
