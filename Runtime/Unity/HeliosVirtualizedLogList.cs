using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public sealed class HeliosVirtualizedLogList : MonoBehaviour
    {
        private sealed class RowView
        {
            public Button Button;
            public RectTransform Rect;
            public Image Background;
            public TextMeshProUGUI Label;
            public Image Severity;
            public int BoundIndex = -1;
            public int BoundVersion = -1;
        }

        private readonly List<RowView> _rows = new List<RowView>();
        private IReadOnlyList<HeliosLogViewEntry> _entries = Array.Empty<HeliosLogViewEntry>();
        private ScrollRect _scroll;
        private RectTransform _content;
        private HeliosWidgetFactory _widgets;
        private Action<HeliosLogViewEntry> _selected;
        private float _rowHeight;
        private Color _altRowColor;
        private int _entriesVersion;
        private int _lastFirstIndex = -1;
        private int _lastBoundVersion = -1;

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
            _altRowColor = Color.Lerp(_widgets.Theme.Row, _widgets.Theme.Input, 0.35f);

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
            _entriesVersion++;
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

        private bool EnsurePool(int count)
        {
            bool grew = false;
            while (_rows.Count < count)
            {
                int poolIndex = _rows.Count;
                Button button = _widgets.CreateButton($"VirtualLog_{poolIndex}", _content, string.Empty, null);
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, _rowHeight - 2f);

                Image marker = _widgets.AddIcon(button.transform, HeliosShapeLibrary.Circle(), _widgets.Theme.Text, 8f);
                marker.name = "Severity";
                RectTransform markerRect = marker.rectTransform;
                markerRect.anchorMin = new Vector2(0f, 0.5f);
                markerRect.anchorMax = new Vector2(0f, 0.5f);
                markerRect.anchoredPosition = new Vector2(14f, 0f);

                RowView view = new RowView
                {
                    Button = button,
                    Rect = rect,
                    Background = button.targetGraphic as Image,
                    Label = button.GetComponentInChildren<TextMeshProUGUI>(),
                    Severity = marker
                };

                if (view.Label != null)
                {
                    view.Label.alignment = TextAlignmentOptions.Left;
                    view.Label.textWrappingMode = TextWrappingModes.NoWrap;
                    HeliosWidgetFactory.Stretch(view.Label.rectTransform, 28f, 0f, 8f, 0f);
                }

                button.onClick.AddListener(() => SelectRow(view));
                _rows.Add(view);
                grew = true;
            }

            return grew;
        }

        private void SelectRow(RowView row)
        {
            if (row.BoundIndex >= 0 && row.BoundIndex < _entries.Count)
                _selected?.Invoke(_entries[row.BoundIndex]);
        }

        private void RefreshVisible()
        {
            if (_scroll == null || _content == null)
                return;

            float viewportHeight = _scroll.viewport.rect.height;
            int desiredPool = Mathf.Max(4, Mathf.CeilToInt(viewportHeight / _rowHeight) + 3);
            bool poolGrew = EnsurePool(desiredPool);

            float offset = Mathf.Max(0f, _content.anchoredPosition.y);
            int first = Mathf.Clamp(Mathf.FloorToInt(offset / _rowHeight), 0, Mathf.Max(0, _entries.Count - 1));

            // Rows are placed at absolute offsets, so nothing on screen changes
            // until the first visible index, the entry list, or the pool changes.
            if (!poolGrew && first == _lastFirstIndex && _entriesVersion == _lastBoundVersion)
                return;

            _lastFirstIndex = first;
            _lastBoundVersion = _entriesVersion;

            for (int i = 0; i < _rows.Count; i++)
            {
                int entryIndex = first + i;
                RowView row = _rows[i];
                if (entryIndex >= _entries.Count)
                {
                    if (row.Button.gameObject.activeSelf)
                        row.Button.gameObject.SetActive(false);
                    row.BoundIndex = -1;
                    continue;
                }

                if (!row.Button.gameObject.activeSelf)
                    row.Button.gameObject.SetActive(true);

                if (row.BoundIndex == entryIndex && row.BoundVersion == _entriesVersion)
                    continue;

                row.BoundIndex = entryIndex;
                row.BoundVersion = _entriesVersion;
                row.Rect.anchoredPosition = new Vector2(0f, -entryIndex * _rowHeight);

                HeliosLogViewEntry viewEntry = _entries[entryIndex];
                HeliosLogEntry entry = viewEntry.Representative;
                Color severityColor = ColorFor(entry.Level);

                if (row.Background != null)
                    row.Background.color = entryIndex % 2 == 0 ? _widgets.Theme.Row : _altRowColor;

                if (row.Label != null)
                {
                    row.Label.text = FormatRow(viewEntry);
                    row.Label.color = severityColor;
                }

                row.Severity.color = severityColor;
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
