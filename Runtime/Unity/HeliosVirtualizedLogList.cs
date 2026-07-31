using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public sealed class HeliosVirtualizedLogList : MonoBehaviour
    {
        private readonly List<Button> _rows = new List<Button>();
        private IReadOnlyList<HeliosLogViewEntry> _entries = Array.Empty<HeliosLogViewEntry>();
        private ScrollRect _scroll;
        private RectTransform _content;
        private HeliosWidgetFactory _widgets;
        private Action<HeliosLogViewEntry> _selected;
        private float _rowHeight;

        public void Initialize(
            ScrollRect scroll,
            RectTransform content,
            HeliosWidgetFactory widgets,
            float rowHeight,
            Action<HeliosLogViewEntry> selected)
        {
            _scroll = scroll;
            _content = content;
            _widgets = widgets;
            _rowHeight = Mathf.Max(28f, rowHeight);
            _selected = selected;

            VerticalLayoutGroup layout = _content.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.enabled = false;
                Destroy(layout);
            }
            ContentSizeFitter fitter = _content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
                Destroy(fitter);
            }

            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _scroll.onValueChanged.AddListener(OnScrollChanged);
            EnsurePool(24);
        }

        public void SetEntries(IReadOnlyList<HeliosLogViewEntry> entries, bool stickToBottom)
        {
            _entries = entries ?? Array.Empty<HeliosLogViewEntry>();
            _content.sizeDelta = new Vector2(0f, _entries.Count * _rowHeight);
            if (stickToBottom)
                _scroll.verticalNormalizedPosition = 0f;
            RefreshVisible();
        }

        private void OnDestroy()
        {
            if (_scroll != null)
                _scroll.onValueChanged.RemoveListener(OnScrollChanged);
        }

        private void LateUpdate()
        {
            RefreshVisible();
        }

        private void OnScrollChanged(Vector2 value)
        {
            RefreshVisible();
        }

        private void EnsurePool(int count)
        {
            while (_rows.Count < count)
            {
                int poolIndex = _rows.Count;
                Button row = _widgets.CreateButton($"VirtualLog_{poolIndex}", _content, string.Empty, null);
                RectTransform rect = row.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, _rowHeight - 2f);
                row.onClick.AddListener(() => SelectRow(row));
                Image marker = _widgets.AddIcon(row.transform, HeliosShapeLibrary.Circle(), _widgets.Theme.Text, 8f);
                marker.name = "Severity";
                RectTransform markerRect = marker.rectTransform;
                markerRect.anchorMin = new Vector2(0f, 0.5f);
                markerRect.anchorMax = new Vector2(0f, 0.5f);
                markerRect.anchoredPosition = new Vector2(14f, 0f);
                _rows.Add(row);
            }
        }

        private void SelectRow(Button row)
        {
            int entryIndex = row.transform.GetSiblingIndex();
            TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null && int.TryParse(label.gameObject.name, out int boundIndex))
                entryIndex = boundIndex;

            if (entryIndex >= 0 && entryIndex < _entries.Count)
                _selected?.Invoke(_entries[entryIndex]);
        }

        private void RefreshVisible()
        {
            if (_scroll == null || _content == null)
                return;

            float viewportHeight = _scroll.viewport.rect.height;
            int desiredPool = Mathf.Max(4, Mathf.CeilToInt(viewportHeight / _rowHeight) + 3);
            EnsurePool(desiredPool);

            float offset = Mathf.Max(0f, _content.anchoredPosition.y);
            int first = Mathf.Clamp(Mathf.FloorToInt(offset / _rowHeight), 0, Mathf.Max(0, _entries.Count - 1));
            for (int i = 0; i < _rows.Count; i++)
            {
                int entryIndex = first + i;
                Button row = _rows[i];
                if (entryIndex >= _entries.Count)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                row.gameObject.SetActive(true);
                RectTransform rect = row.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(0f, -entryIndex * _rowHeight);

                HeliosLogViewEntry viewEntry = _entries[entryIndex];
                HeliosLogEntry entry = viewEntry.Representative;
                Image rowBackground = row.targetGraphic as Image;
                if (rowBackground != null)
                    rowBackground.color = entryIndex % 2 == 0 ? _widgets.Theme.Row : Color.Lerp(_widgets.Theme.Row, _widgets.Theme.Input, 0.35f);

                TextMeshProUGUI label = row.GetComponentInChildren<TextMeshProUGUI>();
                label.gameObject.name = entryIndex.ToString();
                label.alignment = TextAlignmentOptions.Left;
                label.text = FormatRow(viewEntry);
                label.color = ColorFor(entry.Level);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                HeliosWidgetFactory.Stretch(label.rectTransform, 28f, 0f, 8f, 0f);

                Transform marker = row.transform.Find("Severity");
                if (marker != null)
                    marker.GetComponent<Image>().color = ColorFor(entry.Level);
            }
        }

        private static string FormatRow(HeliosLogViewEntry viewEntry)
        {
            HeliosLogEntry entry = viewEntry.Representative;
            string count = viewEntry.Count > 1 ? $" x{viewEntry.Count}" : string.Empty;
            return $"<mspace=0.58em>{entry.Timestamp:HH:mm:ss.fff}</mspace>  <mspace=0.72em>{entry.Level}</mspace>{count}  {HeliosWidgetFactory.Truncate(entry.Message, 240)}";
        }

        private Color ColorFor(HeliosLogLevel level)
        {
            switch (level)
            {
                case HeliosLogLevel.Warning:
                    return _widgets.Theme.Warning;
                case HeliosLogLevel.Error:
                case HeliosLogLevel.Exception:
                case HeliosLogLevel.Assert:
                    return _widgets.Theme.Error;
                default:
                    return _widgets.Theme.Text;
            }
        }
    }
}
