using UnityEngine;

namespace HeliosDebugger
{
    [CreateAssetMenu(fileName = "HeliosDebuggerTheme", menuName = "Astra/Helios/Theme")]
    public sealed class HeliosThemeProfile : ScriptableObject
    {
        [Header("Surfaces")]
        [SerializeField] private Color _panel = new Color(0.051f, 0.059f, 0.071f, 0.97f);
        [SerializeField] private Color _header = new Color(0.078f, 0.09f, 0.11f, 0.98f);
        [SerializeField] private Color _navigation = new Color(0.063f, 0.075f, 0.09f, 0.98f);
        [SerializeField] private Color _row = new Color(0.086f, 0.098f, 0.122f, 0.96f);
        [SerializeField] private Color _input = new Color(0.059f, 0.071f, 0.086f, 0.98f);
        [SerializeField] private Color _button = new Color(0.122f, 0.145f, 0.18f, 0.96f);
        [SerializeField] private Color _border = new Color(0.137f, 0.153f, 0.18f, 1f);
        [SerializeField] private Color _elevated = new Color(0.078f, 0.09f, 0.11f, 0.99f);
        [SerializeField] private Color _shadow = new Color(0f, 0f, 0f, 0.36f);

        [Header("Content")]
        [SerializeField] private Color _text = new Color(0.902f, 0.914f, 0.937f, 1f);
        [SerializeField] private Color _mutedText = new Color(0.545f, 0.576f, 0.631f, 1f);
        [SerializeField] private Color _accent = new Color(0.22f, 0.741f, 0.973f, 1f);
        [SerializeField] private Color _success = new Color(0.263f, 0.824f, 0.561f, 1f);
        [SerializeField] private Color _warning = new Color(0.984f, 0.749f, 0.141f, 1f);
        [SerializeField] private Color _error = new Color(0.973f, 0.353f, 0.353f, 1f);

        [Header("States")]
        [SerializeField] private Color _buttonHover = new Color(0.157f, 0.184f, 0.227f, 0.98f);
        [SerializeField] private Color _buttonPressed = new Color(0.086f, 0.471f, 0.671f, 0.98f);
        [SerializeField] private Color _buttonDisabled = new Color(0.1f, 0.112f, 0.13f, 0.48f);
        [SerializeField] private Color _selected = new Color(0.22f, 0.741f, 0.973f, 0.16f);

        [Header("Sizing")]
        [SerializeField] private int _baseFontSize = 13;
        [SerializeField] private int _titleFontSize = 20;
        [SerializeField] private int _sectionFontSize = 14;
        [SerializeField] private int _captionFontSize = 11;
        [SerializeField] private float _rowHeight = 34f;
        [SerializeField] private float _spacing = 8f;
        [SerializeField] private float _cornerRadius = 8f;
        [SerializeField] private float _cardCornerRadius = 10f;
        [SerializeField] private float _controlCornerRadius = 6f;
        [SerializeField] private float _shadowOffset = 10f;

        public Color Panel => _panel;
        public Color Header => _header;
        public Color Navigation => _navigation;
        public Color Row => _row;
        public Color Input => _input;
        public Color Button => _button;
        public Color Border => _border;
        public Color Elevated => _elevated;
        public Color Shadow => _shadow;
        public Color Text => _text;
        public Color MutedText => _mutedText;
        public Color Accent => _accent;
        public Color Success => _success;
        public Color Warning => _warning;
        public Color Error => _error;
        public Color ButtonHover => _buttonHover;
        public Color ButtonPressed => _buttonPressed;
        public Color ButtonDisabled => _buttonDisabled;
        public Color Selected => _selected;
        public int BaseFontSize => Mathf.Max(10, _baseFontSize);
        public int TitleFontSize => Mathf.Max(BaseFontSize, _titleFontSize);
        public int SectionFontSize => Mathf.Max(11, _sectionFontSize);
        public int CaptionFontSize => Mathf.Max(9, _captionFontSize);
        public float RowHeight => Mathf.Max(28f, _rowHeight);
        public float Spacing => Mathf.Max(0f, _spacing);
        public float SpaceXs => 4f;
        public float SpaceSm => 8f;
        public float SpaceMd => 12f;
        public float SpaceLg => 16f;
        public float SpaceXl => 24f;
        public float CornerRadius => Mathf.Max(0f, _cornerRadius);
        public float CardCornerRadius => Mathf.Max(CornerRadius, _cardCornerRadius);
        public float ControlCornerRadius => Mathf.Max(0f, _controlCornerRadius);
        public float ShadowOffset => Mathf.Max(0f, _shadowOffset);

        public static HeliosThemeProfile CreateRuntimeDefault()
        {
            HeliosThemeProfile profile = CreateInstance<HeliosThemeProfile>();
            profile.name = "HeliosDebuggerTheme_RuntimeDefault";
            return profile;
        }
    }
}
