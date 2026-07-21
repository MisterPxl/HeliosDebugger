using System;
using UnityEngine;

namespace HeliosDebugger
{
    public enum HeliosReportTransportKind
    {
        LocalExport,
        Webhook,
        NativeShare
    }

    [CreateAssetMenu(fileName = "HeliosDebuggerSettings", menuName = "HeliosDebugger/Settings")]
    public sealed class HeliosDebuggerSettings : ScriptableObject
    {
        [Header("Availability")]
        [SerializeField] private bool _autoBootstrap = true;
        [SerializeField] private bool _developmentBuildOnly = true;
        [SerializeField] private bool _visibleAtStartup;

        [Header("Input")]
        [SerializeField] private bool _showTrigger = true;
        [SerializeField] private bool _requireTripleTap = true;
        [SerializeField] private float _tripleTapWindow = 0.8f;
        [SerializeField] private KeyCode _toggleKey = KeyCode.BackQuote;
        [SerializeField] private bool _enableGamepadCombo = true;
        [SerializeField] private bool _enableKonamiCode = true;

        [Header("Data")]
        [SerializeField] private int _logCapacity = 500;
        [SerializeField] private int _profilerHistoryCapacity = 240;
        [SerializeField] private float _profilerRefreshInterval = 0.25f;

        [Header("Bug Reports")]
        [SerializeField] private string _webhookUrl;
        [SerializeField] private int _maxReportBytes = 5 * 1024 * 1024;
        [SerializeField] private HeliosReportTransportKind _defaultReportTransport = HeliosReportTransportKind.LocalExport;

        public bool AutoBootstrap => _autoBootstrap;
        public bool DevelopmentBuildOnly => _developmentBuildOnly;
        public bool VisibleAtStartup => _visibleAtStartup;
        public bool ShowTrigger => _showTrigger;
        public bool RequireTripleTap => _requireTripleTap;
        public float TripleTapWindow => Mathf.Max(0.2f, _tripleTapWindow);
        public KeyCode ToggleKey => _toggleKey;
        public bool EnableGamepadCombo => _enableGamepadCombo;
        public bool EnableKonamiCode => _enableKonamiCode;
        public int LogCapacity => Mathf.Max(32, _logCapacity);
        public int ProfilerHistoryCapacity => Mathf.Max(30, _profilerHistoryCapacity);
        public float ProfilerRefreshInterval => Mathf.Max(0.05f, _profilerRefreshInterval);
        public string WebhookUrl => _webhookUrl;
        public int MaxReportBytes => Mathf.Max(1024, _maxReportBytes);
        public HeliosReportTransportKind DefaultReportTransport => _defaultReportTransport;

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
