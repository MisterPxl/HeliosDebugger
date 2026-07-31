using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public sealed class HeliosWidgetFactory
    {
        private readonly Font _font;
        private readonly HeliosThemeProfile _theme;

        public HeliosWidgetFactory(HeliosThemeProfile theme = null)
        {
            _theme = theme != null ? theme : HeliosThemeProfile.CreateRuntimeDefault();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont("Arial", _theme.BaseFontSize);
        }

        public HeliosThemeProfile Theme => _theme;

        public GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public Text CreateText(string name, Transform parent, string text, int fontSize = 0, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text label = go.GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize > 0 ? fontSize : _theme.BaseFontSize;
            label.alignment = alignment;
            label.color = _theme.Text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public Button CreateButton(string name, Transform parent, string label, UnityAction onClick)
        {
            GameObject go = CreatePanel(name, parent, _theme.Button);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            if (onClick != null)
                button.onClick.AddListener(onClick);

            Text text = CreateText("Label", go.transform, label, _theme.BaseFontSize, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        public InputField CreateInput(string name, Transform parent, string placeholder, UnityAction<string> onChanged)
        {
            GameObject go = CreatePanel(name, parent, _theme.Input);
            InputField input = go.AddComponent<InputField>();

            Text text = CreateText("Text", go.transform, string.Empty, _theme.BaseFontSize, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 8f, 4f, 8f, 4f);
            input.textComponent = text;

            Text placeholderText = CreateText("Placeholder", go.transform, placeholder, _theme.BaseFontSize, TextAnchor.MiddleLeft);
            placeholderText.color = _theme.MutedText;
            Stretch(placeholderText.rectTransform, 8f, 4f, 8f, 4f);
            input.placeholder = placeholderText;

            if (onChanged != null)
                input.onValueChanged.AddListener(onChanged);

            return input;
        }

        public HeliosTimeSeriesGraph CreateTimeSeriesGraph(string name, Transform parent, Color background)
        {
            GameObject container = CreatePanel(name, parent, background);
            GameObject graphGo = new GameObject("Graph", typeof(RectTransform), typeof(CanvasRenderer), typeof(HeliosTimeSeriesGraph));
            graphGo.transform.SetParent(container.transform, false);
            HeliosTimeSeriesGraph graph = graphGo.GetComponent<HeliosTimeSeriesGraph>();
            Stretch(graph.rectTransform, 8f, 8f, 8f, 8f);
            return graph;
        }

        public GameObject CreateMetricCard(string name, Transform parent, string title, out Text value)
        {
            GameObject card = CreatePanel(name, parent, _theme.Input);
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            Text titleText = CreateText("Title", card.transform, title, 13);
            titleText.color = _theme.MutedText;
            AddLayout(titleText.gameObject, 22f);

            value = CreateText("Value", card.transform, "--", 20);
            AddLayout(value.gameObject, 30f);
            return card;
        }

        public Image CreateFillBar(string name, Transform parent, Color background, Color fillColor)
        {
            GameObject bar = CreatePanel(name, parent, background);
            GameObject fillGo = CreatePanel("Fill", bar.transform, fillColor);
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
            GameObject viewport = CreatePanel(name, parent, _theme.Navigation);
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
            layout.spacing = 4f;
            layout.padding = new RectOffset(8, 8, 8, 8);
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
            return scroll;
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
    }
}
