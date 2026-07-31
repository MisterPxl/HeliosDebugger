using UnityEngine;

namespace HeliosDebugger
{
    [CreateAssetMenu(fileName = "HeliosDebuggerTheme", menuName = "HeliosDebugger/Theme")]
    public sealed class HeliosThemeProfile : ScriptableObject
    {
        [Header("Surfaces")]
        [SerializeField] private Color _panel = new Color(0.02f, 0.025f, 0.035f, 0.97f);
        [SerializeField] private Color _header = new Color(0.08f, 0.1f, 0.14f, 0.98f);
        [SerializeField] private Color _navigation = new Color(0.05f, 0.06f, 0.08f, 0.98f);
        [SerializeField] private Color _row = new Color(0.08f, 0.1f, 0.13f, 0.96f);
        [SerializeField] private Color _input = new Color(0.07f, 0.09f, 0.12f, 0.98f);
        [SerializeField] private Color _button = new Color(0.16f, 0.2f, 0.26f, 0.96f);

        [Header("Content")]
        [SerializeField] private Color _text = Color.white;
        [SerializeField] private Color _mutedText = new Color(1f, 1f, 1f, 0.58f);
        [SerializeField] private Color _accent = new Color(0.2f, 0.74f, 1f, 0.95f);
        [SerializeField] private Color _warning = new Color(1f, 0.78f, 0.32f);
        [SerializeField] private Color _error = new Color(1f, 0.38f, 0.32f);

        [Header("Sizing")]
        [SerializeField] private int _baseFontSize = 14;
        [SerializeField] private float _rowHeight = 44f;
        [SerializeField] private float _spacing = 6f;

        public Color Panel => _panel;
        public Color Header => _header;
        public Color Navigation => _navigation;
        public Color Row => _row;
        public Color Input => _input;
        public Color Button => _button;
        public Color Text => _text;
        public Color MutedText => _mutedText;
        public Color Accent => _accent;
        public Color Warning => _warning;
        public Color Error => _error;
        public int BaseFontSize => Mathf.Max(10, _baseFontSize);
        public float RowHeight => Mathf.Max(28f, _rowHeight);
        public float Spacing => Mathf.Max(0f, _spacing);

        public static HeliosThemeProfile CreateRuntimeDefault()
        {
            HeliosThemeProfile profile = CreateInstance<HeliosThemeProfile>();
            profile.name = "HeliosDebuggerTheme_RuntimeDefault";
            return profile;
        }
    }
}
