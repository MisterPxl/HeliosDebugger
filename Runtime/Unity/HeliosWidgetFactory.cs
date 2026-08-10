using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public readonly struct HeliosButtonStyle
    {
        public HeliosButtonStyle(Color normal, Color hover, Color pressed, Color disabled, Color text, float radius, int horizontalPadding)
        {
            Normal = normal;
            Hover = hover;
            Pressed = pressed;
            Disabled = disabled;
            Text = text;
            Radius = radius;
            HorizontalPadding = horizontalPadding;
        }

        public Color Normal { get; }
        public Color Hover { get; }
        public Color Pressed { get; }
        public Color Disabled { get; }
        public Color Text { get; }
        public float Radius { get; }
        public int HorizontalPadding { get; }

        public static HeliosButtonStyle Secondary(HeliosThemeProfile theme)
        {
            return new HeliosButtonStyle(theme.Button, theme.ButtonHover, theme.ButtonPressed, theme.ButtonDisabled, theme.Text, theme.ControlCornerRadius, 12);
        }

        public static HeliosButtonStyle Primary(HeliosThemeProfile theme)
        {
            Color hover = Color.Lerp(theme.Accent, Color.white, 0.1f);
            Color pressed = Color.Lerp(theme.Accent, Color.black, 0.18f);
            return new HeliosButtonStyle(theme.Accent, hover, pressed, theme.ButtonDisabled, Color.white, theme.ControlCornerRadius, 12);
        }

        public static HeliosButtonStyle Ghost(HeliosThemeProfile theme)
        {
            return new HeliosButtonStyle(Color.clear, theme.Selected, theme.ButtonPressed, Color.clear, theme.MutedText, theme.ControlCornerRadius, 10);
        }

        public static HeliosButtonStyle Danger(HeliosThemeProfile theme)
        {
            Color normal = new Color(theme.Error.r, theme.Error.g, theme.Error.b, 0.18f);
            Color hover = new Color(theme.Error.r, theme.Error.g, theme.Error.b, 0.28f);
            Color pressed = new Color(theme.Error.r, theme.Error.g, theme.Error.b, 0.42f);
            return new HeliosButtonStyle(normal, hover, pressed, theme.ButtonDisabled, theme.Error, theme.ControlCornerRadius, 12);
        }

        public static HeliosButtonStyle Icon(HeliosThemeProfile theme)
        {
            return new HeliosButtonStyle(Color.clear, theme.Selected, theme.ButtonPressed, Color.clear, theme.MutedText, theme.ControlCornerRadius, 0);
        }
    }

    public sealed class HeliosSwitchControl
    {
        private readonly Image _track;
        private readonly Image _knob;
        private readonly TextMeshProUGUI _label;
        private readonly HeliosThemeProfile _theme;

        public HeliosSwitchControl(Button button, Image track, Image knob, TextMeshProUGUI label, HeliosThemeProfile theme)
        {
            Button = button;
            _track = track;
            _knob = knob;
            _label = label;
            _theme = theme;
        }

        public Button Button { get; }

        public void SetValue(bool isOn, string label)
        {
            if (_track != null)
                _track.color = isOn ? _theme.Accent : _theme.Button;
            if (_label != null)
            {
                _label.text = label;
                _label.color = isOn ? Color.white : _theme.Text;
            }
            if (_knob != null)
            {
                _knob.color = Color.white;
                RectTransform knobRect = _knob.rectTransform;
                knobRect.anchorMin = new Vector2(isOn ? 1f : 0f, 0.5f);
                knobRect.anchorMax = new Vector2(isOn ? 1f : 0f, 0.5f);
                knobRect.pivot = new Vector2(isOn ? 1f : 0f, 0.5f);
                knobRect.anchoredPosition = new Vector2(isOn ? -4f : 4f, 0f);
            }
        }
    }

    public sealed class HeliosWidgetFactory
    {
        private readonly Font _font;
        private readonly TMP_FontAsset _fontAsset;
        private readonly HeliosThemeProfile _theme;
        private readonly bool _ownsFont;
        private readonly bool _ownsFontAsset;
        private static bool _fontWarningLogged;
        private static TMP_Settings _runtimeTmpSettings;

        public HeliosWidgetFactory(HeliosThemeProfile theme = null)
        {
            _theme = theme != null ? theme : HeliosThemeProfile.CreateRuntimeDefault();
            _font = Resources.Load<Font>("HeliosDebugger/Fonts/Inter-Regular");
            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", _theme.BaseFontSize);
                _ownsFont = _font != null;
            }

            _fontAsset = ResolveFontAsset(_font, out _ownsFontAsset);
        }

        public HeliosThemeProfile Theme => _theme;

        /// <summary>
        /// Destroys the font assets this factory generated at runtime. Loaded
        /// asset fonts are left untouched. Call when the owning UI is torn down.
        /// </summary>
        public void DestroyGeneratedAssets()
        {
            if (_ownsFontAsset)
                HeliosObjectUtility.Destroy(_fontAsset);
            if (_ownsFont)
                HeliosObjectUtility.Destroy(_font);
        }

        public GameObject CreatePanel(string name, Transform parent, Color color)
        {
            return CreateSurface(name, parent, color, _theme.CardCornerRadius);
        }

        public GameObject CreateSurface(string name, Transform parent, Color color, float radius)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = HeliosShapeLibrary.RoundedRect(radius);
            image.type = Image.Type.Sliced;
            image.raycastTarget = color.a > 0.001f;
            return go;
        }

        public TextMeshProUGUI CreateText(string name, Transform parent, string text, int fontSize = 0, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            if (_fontAsset != null)
                label.font = _fontAsset;
            else if (!_fontWarningLogged)
            {
                _fontWarningLogged = true;
                Debug.LogWarning("HeliosDebugger could not resolve a TMP font asset. Import TMP Essential Resources from Tools > HeliosDebugger > Import TMP Essential Resources.");
            }
            label.text = text;
            label.fontSize = fontSize > 0 ? fontSize : _theme.BaseFontSize;
            label.alignment = ToTmpAlignment(alignment);
            label.color = _theme.Text;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public Button CreateButton(string name, Transform parent, string label, UnityAction onClick)
        {
            return CreateButton(name, parent, label, onClick, HeliosButtonStyle.Secondary(_theme));
        }

        public Button CreateButton(string name, Transform parent, string label, UnityAction onClick, HeliosButtonStyle style)
        {
            GameObject go = CreateSurface(name, parent, style.Normal, style.Radius);
            Button button = go.AddComponent<Button>();
            Image background = go.GetComponent<Image>();
            background.raycastTarget = true;
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateColorBlock(style);
            if (onClick != null)
                button.onClick.AddListener(onClick);

            TextMeshProUGUI text = CreateText("Label", go.transform, label, _theme.BaseFontSize, TextAnchor.MiddleCenter);
            text.color = style.Text;
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform, style.HorizontalPadding, 0f, style.HorizontalPadding, 1f);
            return button;
        }

        public Button CreateIconButton(string name, Transform parent, Sprite icon, UnityAction onClick)
        {
            Button button = CreateButton(name, parent, string.Empty, onClick, HeliosButtonStyle.Icon(_theme));
            AddIcon(button.transform, icon, _theme.MutedText, 18f);
            return button;
        }

        public Image AddIcon(Transform parent, Sprite icon, Color color, float size)
        {
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            Image image = iconGo.GetComponent<Image>();
            image.sprite = icon;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
            return image;
        }

        public TMP_InputField CreateInput(string name, Transform parent, string placeholder, UnityAction<string> onChanged)
        {
            GameObject go = CreateSurface(name, parent, _theme.Input, _theme.ControlCornerRadius);
            TMP_InputField input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = go.GetComponent<Image>();
            input.transition = Selectable.Transition.ColorTint;
            input.colors = CreateColorBlock(new HeliosButtonStyle(
                _theme.Input,
                Color.Lerp(_theme.Input, _theme.Accent, 0.08f),
                Color.Lerp(_theme.Input, _theme.Accent, 0.16f),
                _theme.ButtonDisabled,
                _theme.Text,
                _theme.ControlCornerRadius,
                0));

            GameObject viewportGo = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(go.transform, false);
            RectTransform viewport = viewportGo.GetComponent<RectTransform>();
            Stretch(viewport, 10f, 4f, 10f, 4f);
            input.textViewport = viewport;

            TextMeshProUGUI text = CreateText("Text", viewportGo.transform, string.Empty, _theme.BaseFontSize, TextAnchor.MiddleLeft);
            text.margin = Vector4.zero;
            Stretch(text.rectTransform);
            input.textComponent = text;

            TextMeshProUGUI placeholderText = CreateText("Placeholder", viewportGo.transform, placeholder, _theme.BaseFontSize, TextAnchor.MiddleLeft);
            placeholderText.color = _theme.MutedText;
            Stretch(placeholderText.rectTransform);
            input.placeholder = placeholderText;

            if (onChanged != null)
                input.onValueChanged.AddListener(onChanged);

            return input;
        }

        public HeliosTimeSeriesGraph CreateTimeSeriesGraph(string name, Transform parent, Color background)
        {
            GameObject container = CreateSurface(name, parent, background, _theme.CardCornerRadius);
            GameObject graphGo = new GameObject("Graph", typeof(RectTransform), typeof(CanvasRenderer), typeof(HeliosTimeSeriesGraph));
            graphGo.transform.SetParent(container.transform, false);
            HeliosTimeSeriesGraph graph = graphGo.GetComponent<HeliosTimeSeriesGraph>();
            Stretch(graph.rectTransform, 8f, 8f, 8f, 8f);
            return graph;
        }

        public GameObject CreateMetricCard(string name, Transform parent, string title, out TextMeshProUGUI value)
        {
            GameObject card = CreateSurface(name, parent, _theme.Input, _theme.CardCornerRadius);
            AddBorder(card, _theme.Border);
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 9, 9);
            layout.spacing = _theme.SpaceXs;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI titleText = CreateText("Title", card.transform, title, _theme.CaptionFontSize);
            titleText.color = _theme.MutedText;
            titleText.fontStyle = FontStyles.UpperCase;
            AddLayout(titleText.gameObject, 18f);

            value = CreateText("Value", card.transform, "--", _theme.TitleFontSize);
            value.fontStyle = FontStyles.Bold;
            AddLayout(value.gameObject, 28f);
            return card;
        }

        public Image CreateFillBar(string name, Transform parent, Color background, Color fillColor)
        {
            GameObject bar = CreateSurface(name, parent, background, 999f);
            GameObject fillGo = CreateSurface("Fill", bar.transform, fillColor, 999f);
            Image fill = fillGo.GetComponent<Image>();
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            return fill;
        }

        public ScrollRect CreateScrollView(string name, Transform parent, out RectTransform content)
        {
            GameObject viewport = CreateSurface(name, parent, _theme.Navigation, _theme.CardCornerRadius);
            AddBorder(viewport, _theme.Border);
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport.transform, false);
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = _theme.SpaceXs;
            layout.padding = new RectOffset(8, 12, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.verticalScrollbar = CreateScrollbar("Scrollbar", viewport.transform);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = -6f;
            return scroll;
        }

        public HeliosSwitchControl CreateSwitch(string name, Transform parent, bool isOn, string label, UnityAction onClick)
        {
            GameObject root = CreateSurface(name, parent, Color.clear, 999f);
            Button button = root.AddComponent<Button>();
            Image background = root.GetComponent<Image>();
            background.raycastTarget = true;
            button.targetGraphic = background;
            button.transition = Selectable.Transition.None;
            if (onClick != null)
                button.onClick.AddListener(onClick);

            GameObject trackGo = CreateSurface("Track", root.transform, _theme.Button, 999f);
            RectTransform trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(1f, 0.5f);
            trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(44f, 24f);
            trackRect.anchoredPosition = new Vector2(-4f, 0f);

            GameObject knobGo = CreateSurface("Knob", trackGo.transform, Color.white, 999f);
            RectTransform knobRect = knobGo.GetComponent<RectTransform>();
            knobRect.sizeDelta = new Vector2(18f, 18f);

            TextMeshProUGUI valueLabel = CreateText("Label", root.transform, label, _theme.BaseFontSize, TextAnchor.MiddleLeft);
            Stretch(valueLabel.rectTransform, 8f, 0f, 54f, 0f);

            HeliosSwitchControl control = new HeliosSwitchControl(button, trackGo.GetComponent<Image>(), knobGo.GetComponent<Image>(), valueLabel, _theme);
            control.SetValue(isOn, label);
            return control;
        }

        public TextMeshProUGUI CreateChip(string name, Transform parent, string label, Color color)
        {
            GameObject chip = CreateSurface(name, parent, new Color(color.r, color.g, color.b, 0.14f), 999f);
            TextMeshProUGUI text = CreateText("Label", chip.transform, label, _theme.CaptionFontSize, TextAnchor.MiddleCenter);
            text.color = color;
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform, 9f, 0f, 9f, 0f);
            return text;
        }

        public TextMeshProUGUI CreateSectionHeader(string name, Transform parent, string label)
        {
            TextMeshProUGUI text = CreateText(name, parent, label, _theme.SectionFontSize);
            text.color = _theme.Accent;
            text.fontStyle = FontStyles.Bold;
            AddLayout(text.gameObject, 28f);
            return text;
        }

        public GameObject CreateDivider(string name, Transform parent)
        {
            GameObject divider = CreateSurface(name, parent, _theme.Border, 0f);
            AddLayout(divider, 1f);
            return divider;
        }

        public TextMeshProUGUI CreateEmptyState(string name, Transform parent, string message)
        {
            GameObject card = CreateSurface(name, parent, _theme.Input, _theme.CardCornerRadius);
            AddBorder(card, _theme.Border);
            TextMeshProUGUI text = CreateText("Message", card.transform, message, _theme.BaseFontSize, TextAnchor.MiddleCenter);
            text.color = _theme.MutedText;
            Stretch(text.rectTransform, 16f, 16f, 16f, 16f);
            AddLayout(card, 96f);
            return text;
        }

        public void AddLayout(GameObject go, float preferredHeight = -1f, float minHeight = -1f)
        {
            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null)
                layout = go.AddComponent<LayoutElement>();

            if (preferredHeight >= 0f)
                layout.preferredHeight = preferredHeight;
            if (minHeight >= 0f)
                layout.minHeight = minHeight;
        }

        public void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        public static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
                return value;

            return value.Substring(0, Math.Max(0, max - 3)) + "...";
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 0L)
                return "Unavailable";

            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        public static string FormatValue(long value)
        {
            return value < 0L ? "Unavailable" : value.ToString();
        }

        public static TextMeshProUGUI GetButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<TextMeshProUGUI>() : null;
        }

        public void AddBorder(GameObject go, Color color)
        {
            Outline outline = go.GetComponent<Outline>();
            if (outline == null)
                outline = go.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }

        public void AddShadow(GameObject go)
        {
            Shadow shadow = go.GetComponent<Shadow>();
            if (shadow == null)
                shadow = go.AddComponent<Shadow>();
            shadow.effectColor = _theme.Shadow;
            shadow.effectDistance = new Vector2(0f, -_theme.ShadowOffset);
            shadow.useGraphicAlpha = true;
        }

        private Scrollbar CreateScrollbar(string name, Transform parent)
        {
            GameObject root = CreateSurface(name, parent, Color.clear, 999f);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            Anchor(rootRect, new Vector2(1f, 0f), Vector2.one, new Vector2(-8f, 8f), new Vector2(-2f, -8f));

            Scrollbar scrollbar = root.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            GameObject handleGo = CreateSurface("Handle", root.transform, new Color(_theme.MutedText.r, _theme.MutedText.g, _theme.MutedText.b, 0.34f), 999f);
            RectTransform handleRect = handleGo.GetComponent<RectTransform>();
            Stretch(handleRect);
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleGo.GetComponent<Image>();
            scrollbar.transition = Selectable.Transition.ColorTint;
            scrollbar.colors = CreateColorBlock(new HeliosButtonStyle(
                handleGo.GetComponent<Image>().color,
                new Color(_theme.MutedText.r, _theme.MutedText.g, _theme.MutedText.b, 0.54f),
                new Color(_theme.MutedText.r, _theme.MutedText.g, _theme.MutedText.b, 0.72f),
                Color.clear,
                _theme.Text,
                999f,
                0));
            return scrollbar;
        }

        private static ColorBlock CreateColorBlock(HeliosButtonStyle style)
        {
            return new ColorBlock
            {
                normalColor = style.Normal,
                highlightedColor = style.Hover,
                pressedColor = style.Pressed,
                selectedColor = style.Hover,
                disabledColor = style.Disabled,
                colorMultiplier = 1f,
                fadeDuration = 0f
            };
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Left;
            }
        }

        private static TMP_FontAsset ResolveFontAsset(Font font, out bool generatedAtRuntime)
        {
            EnsureRuntimeTmpSettings();

            TMP_FontAsset generated = CreateFontAsset(font);
            generatedAtRuntime = generated != null;
            if (generated != null)
                return generated;

            TMP_FontAsset bundled = Resources.Load<TMP_FontAsset>("HeliosDebugger/Fonts/Inter-Regular SDF");
            if (bundled != null)
                return bundled;

            TMP_FontAsset tmpDefault = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (tmpDefault != null)
                return tmpDefault;

            return GetDefaultFontAsset();
        }

        private static void EnsureRuntimeTmpSettings()
        {
            try
            {
                if (TMP_Settings.instance != null)
                    return;
            }
            catch (Exception)
            {
            }

            FieldInfo instanceField = typeof(TMP_Settings).GetField(
                "s_Instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (instanceField == null)
                return;

            TMP_Settings currentSettings = instanceField.GetValue(null) as TMP_Settings;
            if (currentSettings != null)
                return;

            _runtimeTmpSettings = ScriptableObject.CreateInstance<TMP_Settings>();
            _runtimeTmpSettings.name = "HeliosDebugger_RuntimeTMPSettings";
            _runtimeTmpSettings.hideFlags = HideFlags.HideAndDontSave;
            instanceField.SetValue(null, _runtimeTmpSettings);
        }

        private static TMP_FontAsset CreateFontAsset(Font font)
        {
            if (font == null)
                return null;

            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                    font,
                    64,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                asset.name = "HeliosInterRuntime";
                asset.hideFlags = HideFlags.HideAndDontSave;
                return asset;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static TMP_FontAsset GetDefaultFontAsset()
        {
            try
            {
                return TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
