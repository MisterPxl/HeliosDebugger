using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public sealed class HeliosTimeSeriesGraph : MaskableGraphic
    {
        private readonly List<float> _values = new List<float>();
        private float _minimumMaxValue = 33.33f;
        private float _targetFrameMs = 16.67f;
        private float _warningFrameMs = 33.33f;
        private Color _barColor = new Color(0.28f, 0.78f, 1f, 0.95f);
        private Color _warningColor = new Color(1f, 0.77f, 0.22f, 0.95f);
        private Color _criticalColor = new Color(1f, 0.32f, 0.25f, 0.95f);
        private Color _gridColor = new Color(1f, 1f, 1f, 0.16f);

        public float CurrentMaxValue
        {
            get
            {
                float maxValue = _minimumMaxValue;
                for (int i = 0; i < _values.Count; i++)
                    maxValue = Mathf.Max(maxValue, _values[i]);
                return maxValue;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Configure(
            float minimumMaxValue,
            float targetFrameMs,
            float warningFrameMs,
            Color barColor,
            Color warningColor,
            Color criticalColor,
            Color gridColor)
        {
            _minimumMaxValue = Mathf.Max(1f, minimumMaxValue);
            _targetFrameMs = Mathf.Max(0.1f, targetFrameMs);
            _warningFrameMs = Mathf.Max(_targetFrameMs, warningFrameMs);
            _barColor = barColor;
            _warningColor = warningColor;
            _criticalColor = criticalColor;
            _gridColor = gridColor;
            SetVerticesDirty();
        }

        public void SetValues(IReadOnlyList<float> values, int maxSamples)
        {
            _values.Clear();

            if (values != null)
            {
                int visibleSamples = Mathf.Max(1, maxSamples);
                int start = Mathf.Max(0, values.Count - visibleSamples);
                for (int i = start; i < values.Count; i++)
                    _values.Add(Mathf.Max(0f, values[i]));
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            Rect rect = rectTransform.rect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            float maxValue = CurrentMaxValue;
            AddHorizontalLine(vertexHelper, rect, _targetFrameMs, maxValue, 1f, _gridColor);
            AddHorizontalLine(vertexHelper, rect, _warningFrameMs, maxValue, 1.5f, _gridColor);

            if (_values.Count == 0)
                return;

            float slotWidth = rect.width / _values.Count;
            float spacing = Mathf.Min(2f, slotWidth * 0.22f);
            float barWidth = Mathf.Max(1f, slotWidth - spacing);

            for (int i = 0; i < _values.Count; i++)
            {
                float value = _values[i];
                float normalized = Mathf.Clamp01(value / maxValue);
                float xMin = rect.xMin + i * slotWidth + spacing * 0.5f;
                float xMax = Mathf.Min(rect.xMax, xMin + barWidth);
                float yMax = rect.yMin + Mathf.Max(1f, rect.height * normalized);
                AddQuad(vertexHelper, new Rect(xMin, rect.yMin, xMax - xMin, yMax - rect.yMin), ColorForValue(value));
            }
        }

        private void AddHorizontalLine(VertexHelper vertexHelper, Rect rect, float value, float maxValue, float thickness, Color lineColor)
        {
            if (value <= 0f || value > maxValue)
                return;

            float normalized = Mathf.Clamp01(value / maxValue);
            float y = rect.yMin + rect.height * normalized;
            AddQuad(vertexHelper, new Rect(rect.xMin, y - thickness * 0.5f, rect.width, thickness), lineColor);
        }

        private Color ColorForValue(float value)
        {
            if (value >= _warningFrameMs)
                return _criticalColor;
            if (value >= _targetFrameMs)
                return _warningColor;
            return _barColor;
        }

        private static void AddQuad(VertexHelper vertexHelper, Rect rect, Color quadColor)
        {
            int startIndex = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = quadColor;

            vertex.position = new Vector3(rect.xMin, rect.yMin);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMin, rect.yMax);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMax);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMin);
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}
