using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public sealed class HeliosWidgetFactory
    {
        private readonly Font _font;

        public HeliosWidgetFactory()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
                _font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        }

        public GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        public Text CreateText(string name, Transform parent, string text, int fontSize = 14, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text label = go.GetComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public Button CreateButton(string name, Transform parent, string label, UnityAction onClick)
        {
            GameObject go = CreatePanel(name, parent, new Color(0.16f, 0.2f, 0.26f, 0.96f));
            Button button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            button.onClick.AddListener(onClick);

            Text text = CreateText("Label", go.transform, label, 14, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button;
        }

        public InputField CreateInput(string name, Transform parent, string placeholder, UnityAction<string> onChanged)
        {
            GameObject go = CreatePanel(name, parent, new Color(0.07f, 0.09f, 0.12f, 0.98f));
            InputField input = go.AddComponent<InputField>();

            Text text = CreateText("Text", go.transform, string.Empty, 14, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, 8f, 4f, 8f, 4f);
            input.textComponent = text;

            Text placeholderText = CreateText("Placeholder", go.transform, placeholder, 14, TextAnchor.MiddleLeft);
            placeholderText.color = new Color(1f, 1f, 1f, 0.42f);
            Stretch(placeholderText.rectTransform, 8f, 4f, 8f, 4f);
            input.placeholder = placeholderText;

            if (onChanged != null)
                input.onValueChanged.AddListener(onChanged);

            return input;
        }

        public ScrollRect CreateScrollView(string name, Transform parent, out RectTransform content)
        {
            GameObject viewport = CreatePanel(name, parent, new Color(0.04f, 0.05f, 0.07f, 0.96f));
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
    }
}
