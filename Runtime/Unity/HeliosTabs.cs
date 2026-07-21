using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public sealed class HeliosConsoleTab : HeliosTabBase
    {
        private RectTransform _list;
        private string _search = string.Empty;
        private bool _paused;
        private int _lastCount = -1;

        public override string Title => "Console";
        public override int Order => 0;

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            GameObject controls = widgets.CreatePanel("ConsoleControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = 6f;
            widgets.AddLayout(controls, 40f);

            widgets.CreateButton("Clear", controls.transform, "Clear", () =>
            {
                Context.Service.Logs.Clear();
                _lastCount = -1;
                RebuildList();
            });
            widgets.CreateButton("Pause", controls.transform, "Pause", () => _paused = !_paused);
            InputField search = widgets.CreateInput("Search", controls.transform, "Search logs", value =>
            {
                _search = value ?? string.Empty;
                _lastCount = -1;
                RebuildList();
            });
            widgets.AddLayout(search.gameObject, 36f);

            ScrollRect scroll = widgets.CreateScrollView("ConsoleList", parent, out _list);
            widgets.AddLayout(scroll.gameObject, 760f, 320f);
        }

        public override void Refresh()
        {
            if (_paused || _list == null)
                return;

            int count = Context.Service.Logs.Snapshot().Count;
            if (count != _lastCount)
                RebuildList();
        }

        private void RebuildList()
        {
            if (_list == null)
                return;

            Widgets.Clear(_list);
            IReadOnlyList<HeliosLogEntry> logs = Context.Service.Logs.Snapshot();
            int shown = 0;
            int start = Mathf.Max(0, logs.Count - 80);

            for (int i = start; i < logs.Count; i++)
            {
                HeliosLogEntry entry = logs[i];
                if (!MatchesSearch(entry))
                    continue;

                Text text = Widgets.CreateText(
                    $"Log_{entry.Sequence}",
                    _list,
                    Format(entry),
                    13,
                    TextAnchor.UpperLeft);
                text.color = ColorFor(entry.Level);
                Widgets.AddLayout(text.gameObject, -1f, 28f);
                shown++;
            }

            if (shown == 0)
                Widgets.CreateText("Empty", _list, "No logs match the current filter.", 14);

            _lastCount = logs.Count;
        }

        private bool MatchesSearch(HeliosLogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            return entry.Message.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.Category.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.Level.ToString().IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Format(HeliosLogEntry entry)
        {
            string category = string.IsNullOrEmpty(entry.Category) ? string.Empty : $"[{entry.Category}]";
            string stack = string.IsNullOrEmpty(entry.StackTrace) ? string.Empty : $"\n{HeliosWidgetFactory.Truncate(entry.StackTrace, 500)}";
            return $"[{entry.Timestamp:HH:mm:ss.fff}][{entry.Level}]{category} {HeliosWidgetFactory.Truncate(entry.Message, 900)}{stack}";
        }

        private static Color ColorFor(HeliosLogLevel level)
        {
            switch (level)
            {
                case HeliosLogLevel.Warning: return new Color(1f, 0.78f, 0.32f);
                case HeliosLogLevel.Error:
                case HeliosLogLevel.Exception:
                case HeliosLogLevel.Assert: return new Color(1f, 0.38f, 0.32f);
                case HeliosLogLevel.Log:
                default: return Color.white;
            }
        }
    }

    public sealed class HeliosProfilerTab : HeliosTabBase
    {
        private readonly List<float> _frameTimes = new List<float>();
        private HeliosTimeSeriesGraph _graph;
        private Text _fpsValue;
        private Text _frameValue;
        private Text _scaleValue;
        private Text _managedValue;
        private Text _usedValue;
        private Text _reservedValue;
        private Text _gcValue;
        private Text _drawCallsValue;
        private Text _status;
        private Image _managedFill;
        private Image _reservedFill;
        private Button _cleanButton;
        private float _lastRefresh;
        private bool _cleaning;

        public override string Title => "Profiler";
        public override int Order => 10;

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            GameObject controls = widgets.CreatePanel("ProfilerControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = 6f;
            widgets.AddLayout(controls, 40f);
            widgets.CreateButton("Reset", controls.transform, "Reset", ResetHistory);
            widgets.CreateButton("GCCollect", controls.transform, "GC Collect", CollectGarbage);
            _cleanButton = widgets.CreateButton("Clean", controls.transform, "Clean", CleanUnusedAssets);

            _graph = widgets.CreateTimeSeriesGraph("FrameGraph", parent, new Color(0.04f, 0.05f, 0.07f, 0.96f));
            _graph.Configure(
                33.33f,
                16.67f,
                33.33f,
                new Color(0.2f, 0.74f, 1f, 0.95f),
                new Color(0.78f, 0.82f, 0.16f, 0.95f),
                new Color(1f, 0.44f, 0.22f, 0.95f),
                new Color(1f, 1f, 1f, 0.16f));
            widgets.AddLayout(_graph.transform.parent.gameObject, 260f, 180f);

            GameObject primaryMetrics = widgets.CreatePanel("PrimaryMetrics", parent, new Color(0f, 0f, 0f, 0f));
            HorizontalLayoutGroup primaryLayout = primaryMetrics.AddComponent<HorizontalLayoutGroup>();
            primaryLayout.spacing = 8f;
            primaryLayout.childControlWidth = true;
            primaryLayout.childForceExpandWidth = true;
            widgets.AddLayout(primaryMetrics, 78f);
            SetFlexibleWidth(widgets.CreateMetricCard("FpsCard", primaryMetrics.transform, "FPS", out _fpsValue));
            SetFlexibleWidth(widgets.CreateMetricCard("FrameCard", primaryMetrics.transform, "Frame Time", out _frameValue));
            SetFlexibleWidth(widgets.CreateMetricCard("ScaleCard", primaryMetrics.transform, "Graph Scale", out _scaleValue));

            GameObject memoryMetrics = widgets.CreatePanel("MemoryMetrics", parent, new Color(0f, 0f, 0f, 0f));
            HorizontalLayoutGroup memoryLayout = memoryMetrics.AddComponent<HorizontalLayoutGroup>();
            memoryLayout.spacing = 8f;
            memoryLayout.childControlWidth = true;
            memoryLayout.childForceExpandWidth = true;
            widgets.AddLayout(memoryMetrics, 126f);
            SetFlexibleWidth(CreateMemoryCard(widgets, "ManagedCard", memoryMetrics.transform, "Managed Memory", out _managedValue, out _managedFill));
            SetFlexibleWidth(CreateMemoryCard(widgets, "ReservedCard", memoryMetrics.transform, "Memory Usage", out _usedValue, out _reservedFill));

            GameObject detailMetrics = widgets.CreatePanel("ProfilerDetails", parent, new Color(0f, 0f, 0f, 0f));
            HorizontalLayoutGroup detailLayout = detailMetrics.AddComponent<HorizontalLayoutGroup>();
            detailLayout.spacing = 8f;
            detailLayout.childControlWidth = true;
            detailLayout.childForceExpandWidth = true;
            widgets.AddLayout(detailMetrics, 78f);
            SetFlexibleWidth(widgets.CreateMetricCard("ReservedValueCard", detailMetrics.transform, "Reserved", out _reservedValue));
            SetFlexibleWidth(widgets.CreateMetricCard("GcCard", detailMetrics.transform, "GC / Frame", out _gcValue));
            SetFlexibleWidth(widgets.CreateMetricCard("DrawCallsCard", detailMetrics.transform, "Draw Calls", out _drawCallsValue));

            _status = widgets.CreateText("ProfilerStatus", parent, "Profiler warming up...", 14);
            _status.color = new Color(1f, 1f, 1f, 0.72f);
            widgets.AddLayout(_status.gameObject, 34f);
        }

        public override void Refresh()
        {
            if (_lastRefresh > 0f && Time.unscaledTime - _lastRefresh < Context.Service.Settings.ProfilerRefreshInterval)
                return;

            _lastRefresh = Time.unscaledTime;
            HeliosProfilerSample latest = Context.Service.Profiler.Latest;
            if (latest == null || _graph == null)
                return;

            IReadOnlyList<HeliosProfilerSample> history = Context.Service.Profiler.History;
            _frameTimes.Clear();
            for (int i = 0; i < history.Count; i++)
                _frameTimes.Add(history[i].FrameMs);
            _graph.SetValues(_frameTimes, Context.Service.Settings.ProfilerHistoryCapacity);

            Color frameColor = ColorForFrame(latest.FrameMs);
            _fpsValue.text = latest.Fps.ToString("F1");
            _fpsValue.color = frameColor;
            _frameValue.text = $"{latest.FrameMs:F2} ms";
            _frameValue.color = frameColor;
            _scaleValue.text = $"0 - {_graph.CurrentMaxValue:F1} ms";

            _managedValue.text = HeliosWidgetFactory.FormatBytes(latest.ManagedMemory);
            _usedValue.text = HeliosWidgetFactory.FormatBytes(latest.UsedMemory);
            _reservedValue.text = HeliosWidgetFactory.FormatBytes(latest.ReservedMemory);
            _gcValue.text = HeliosWidgetFactory.FormatBytes(latest.GcAllocated);
            _drawCallsValue.text = HeliosWidgetFactory.FormatValue(latest.DrawCalls);

            SetFill(_managedFill, latest.ManagedMemory, latest.UsedMemory);
            SetFill(_reservedFill, latest.UsedMemory, latest.ReservedMemory);

            if (!_cleaning && _status != null && _status.text == "Profiler warming up...")
                _status.text = "Ready.";
        }

        private void ResetHistory()
        {
            Context.Service.Profiler.Reset();
            _lastRefresh = 0f;
            _frameTimes.Clear();
            if (_graph != null)
                _graph.SetValues(_frameTimes, Context.Service.Settings.ProfilerHistoryCapacity);
            if (_status != null)
                _status.text = "History reset.";
        }

        private void CollectGarbage()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            _lastRefresh = 0f;
            if (_status != null)
                _status.text = "GC collection requested.";
        }

        private void CleanUnusedAssets()
        {
            if (_cleaning || Context.Root == null)
                return;

            Context.Root.Run(CleanRoutine());
        }

        private IEnumerator CleanRoutine()
        {
            _cleaning = true;
            if (_cleanButton != null)
                _cleanButton.interactable = false;
            if (_status != null)
                _status.text = "Cleaning unused assets...";

            AsyncOperation operation = Resources.UnloadUnusedAssets();
            while (operation != null && !operation.isDone)
                yield return null;

            GC.Collect();
            _cleaning = false;
            if (_cleanButton != null)
                _cleanButton.interactable = true;
            if (_status != null)
                _status.text = "Clean complete.";
            _lastRefresh = 0f;
        }

        private GameObject CreateMemoryCard(HeliosWidgetFactory widgets, string name, Transform parent, string title, out Text value, out Image fill)
        {
            GameObject card = widgets.CreatePanel(name, parent, new Color(0.07f, 0.09f, 0.12f, 0.96f));
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            Text titleText = widgets.CreateText("Title", card.transform, title, 13);
            titleText.color = new Color(1f, 1f, 1f, 0.58f);
            widgets.AddLayout(titleText.gameObject, 22f);

            value = widgets.CreateText("Value", card.transform, "--", 20);
            widgets.AddLayout(value.gameObject, 30f);

            fill = widgets.CreateFillBar(
                "UsageBar",
                card.transform,
                new Color(1f, 1f, 1f, 0.12f),
                new Color(0.2f, 0.74f, 1f, 0.92f));
            widgets.AddLayout(fill.transform.parent.gameObject, 18f);
            return card;
        }

        private static void SetFill(Image fill, long value, long max)
        {
            if (fill == null)
                return;

            float ratio = 0f;
            if (value >= 0L && max > 0L)
                ratio = Mathf.Clamp01(value / (float)max);

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMax = new Vector2(ratio, 1f);
            fillRect.offsetMax = Vector2.zero;
        }

        private static void SetFlexibleWidth(GameObject go)
        {
            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null)
                layout = go.AddComponent<LayoutElement>();

            layout.flexibleWidth = 1f;
        }

        private static Color ColorForFrame(float frameMs)
        {
            if (frameMs >= 33.33f)
                return new Color(1f, 0.44f, 0.22f);
            if (frameMs >= 16.67f)
                return new Color(0.78f, 0.82f, 0.16f);
            return new Color(0.2f, 0.74f, 1f);
        }
    }

    public sealed class HeliosOptionsTab : HeliosTabBase, IHeliosTabOpenHandler
    {
        private const int ForceRebuildRevision = -1;

        private readonly List<OptionValueBinding> _valueBindings = new List<OptionValueBinding>();
        private RectTransform _content;
        private string _search = string.Empty;
        private int _lastRevision = ForceRebuildRevision;
        private bool _refreshRegistryOnNextRefresh;

        public override string Title => "Options";
        public override int Order => 20;

        public override void Initialize(HeliosContext context)
        {
            if (Context != null)
            {
                Context.Service.Options.Changed -= OnOptionsChanged;
                Context.Service.ActionsChanged -= OnOptionsChanged;
            }

            base.Initialize(context);
            Context.Service.Options.Changed += OnOptionsChanged;
            Context.Service.ActionsChanged += OnOptionsChanged;
        }

        public void OnOpened()
        {
            _refreshRegistryOnNextRefresh = true;
            _lastRevision = ForceRebuildRevision;
        }

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            GameObject controls = widgets.CreatePanel("OptionsControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = 6f;
            widgets.AddLayout(controls, 40f);
            InputField search = widgets.CreateInput("Search", controls.transform, "Search options", value =>
            {
                _search = value ?? string.Empty;
                _lastRevision = ForceRebuildRevision;
                Rebuild();
            });
            widgets.AddLayout(search.gameObject, 36f);

            ScrollRect scroll = widgets.CreateScrollView("OptionsList", parent, out _content);
            widgets.AddLayout(scroll.gameObject, 760f, 320f);
            _lastRevision = ForceRebuildRevision;
        }

        public override void Refresh()
        {
            if (_refreshRegistryOnNextRefresh)
                RefreshOptionsRegistry();

            int revision = Context.Service.Options.Revision;
            if (revision != _lastRevision)
                Rebuild();
            else
                RefreshOptionValues();
        }

        public override void Dispose()
        {
            if (Context != null)
            {
                Context.Service.Options.Changed -= OnOptionsChanged;
                Context.Service.ActionsChanged -= OnOptionsChanged;
            }
        }

        private void RefreshOptionsRegistry()
        {
            if (Context == null)
            {
                _refreshRegistryOnNextRefresh = true;
                _lastRevision = ForceRebuildRevision;
                return;
            }

            Context.Service.Options.Refresh();
            _refreshRegistryOnNextRefresh = false;
            _lastRevision = ForceRebuildRevision;
        }

        private void OnOptionsChanged()
        {
            _lastRevision = ForceRebuildRevision;
        }

        private void Rebuild()
        {
            if (_content == null)
                return;

            _valueBindings.Clear();
            Widgets.Clear(_content);
            List<OptionTabEntry> entries = new List<OptionTabEntry>();
            IReadOnlyList<IHeliosValueOption> options = Context.Service.Options.Options;
            for (int i = 0; i < options.Count; i++)
            {
                IHeliosValueOption option = options[i];
                if (Matches(option.DisplayName, option.Category))
                    entries.Add(new OptionTabEntry(option));
            }

            IReadOnlyList<IHeliosActionOption> reflected = Context.Service.Options.Actions;
            for (int i = 0; i < reflected.Count; i++)
            {
                IHeliosActionOption action = reflected[i];
                if (Matches(action.DisplayName, action.Category))
                    entries.Add(new OptionTabEntry(action));
            }

            IReadOnlyList<HeliosActionDefinition> actions = Context.Service.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                HeliosActionDefinition action = actions[i];
                if (Matches(action.DisplayName, action.Category))
                    entries.Add(new OptionTabEntry(action));
            }

            entries.Sort(CompareEntries);
            string currentCategory = null;
            for (int i = 0; i < entries.Count; i++)
            {
                OptionTabEntry entry = entries[i];
                AddCategory(ref currentCategory, entry.Category);
                if (entry.Option != null)
                    AddOption(entry.Option);
                else
                    AddAction(entry.Action);
            }

            _lastRevision = Context.Service.Options.Revision;
        }

        private void RefreshOptionValues()
        {
            for (int i = 0; i < _valueBindings.Count; i++)
                _valueBindings[i].Refresh();
        }

        private void AddCategory(ref string currentCategory, string category)
        {
            string next = string.IsNullOrEmpty(category) ? "General" : category;
            if (currentCategory == next)
                return;

            currentCategory = next;
            Text label = Widgets.CreateText($"Category_{next}", _content, next, 16);
            label.color = new Color(0.68f, 0.86f, 1f);
            Widgets.AddLayout(label.gameObject, 30f);
        }

        private void AddOption(IHeliosValueOption option)
        {
            GameObject row = Widgets.CreatePanel($"Option_{option.DisplayName}", _content, new Color(0.08f, 0.1f, 0.13f, 0.96f));
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(6, 6, 4, 4);
            Widgets.AddLayout(row, 44f);

            Text label = Widgets.CreateText("Label", row.transform, option.DisplayName, 14);
            Widgets.AddLayout(label.gameObject, -1f, 32f);
            InputField stringInput = null;
            if (!option.IsReadOnly && option.ValueKind == HeliosOptionValueKind.String)
            {
                stringInput = Widgets.CreateInput("Value", row.transform, option.GetDisplayValue(), null);
                stringInput.text = option.GetDisplayValue();
                stringInput.onEndEdit.AddListener(text =>
                {
                    option.TrySetFromString(text);
                    if (stringInput != null)
                        stringInput.text = option.GetDisplayValue();
                });
                Widgets.AddLayout(stringInput.gameObject, -1f, 32f);
            }
            else
            {
                Text value = Widgets.CreateText("Value", row.transform, option.GetDisplayValue(), 14, TextAnchor.MiddleRight);
                Widgets.AddLayout(value.gameObject, -1f, 32f);
                _valueBindings.Add(new OptionValueBinding(option, value));
            }

            if (option.IsReadOnly)
                return;

            if (option.ValueKind == HeliosOptionValueKind.Boolean)
            {
                Widgets.CreateButton("Toggle", row.transform, "Toggle", () =>
                {
                    option.ToggleBoolean();
                    RefreshOptionValues();
                });
            }
            else if (option.ValueKind == HeliosOptionValueKind.Enum)
            {
                Widgets.CreateButton("Cycle", row.transform, "Next", () =>
                {
                    option.CycleEnum();
                    RefreshOptionValues();
                });
            }
            else if (option.ValueKind == HeliosOptionValueKind.Integer || option.ValueKind == HeliosOptionValueKind.Float)
            {
                Widgets.CreateButton("Minus", row.transform, "-", () =>
                {
                    option.Adjust(-1f);
                    RefreshOptionValues();
                });
                Widgets.CreateButton("Plus", row.transform, "+", () =>
                {
                    option.Adjust(1f);
                    RefreshOptionValues();
                });
            }

            Widgets.CreateButton("Reset", row.transform, "Reset", () =>
            {
                option.Reset();
                if (stringInput != null)
                    stringInput.text = option.GetDisplayValue();
                RefreshOptionValues();
            });
        }

        private void AddAction(IHeliosActionOption action)
        {
            if (action.Parameters.Count == 0)
            {
                Button button = Widgets.CreateButton($"Action_{action.DisplayName}", _content, action.Pin ? $"Pinned: {action.DisplayName}" : action.DisplayName, () =>
                {
                    HeliosActionResult result = action.Invoke();
                    if (!result.Success)
                        UnityEngine.Debug.LogWarning($"Helios action failed: {result.Message}");
                });
                Widgets.AddLayout(button.gameObject, 40f);
                return;
            }

            GameObject card = Widgets.CreatePanel($"Action_{action.DisplayName}", _content, new Color(0.08f, 0.1f, 0.13f, 0.96f));
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            Widgets.AddLayout(card, Mathf.Max(96f, 84f + action.Parameters.Count * 42f));

            Text title = Widgets.CreateText("Title", card.transform, action.Pin ? $"Pinned: {action.DisplayName}" : action.DisplayName, 14);
            title.color = new Color(0.9f, 0.96f, 1f);
            Widgets.AddLayout(title.gameObject, 24f);

            List<Func<string>> valueReaders = new List<Func<string>>(action.Parameters.Count);
            for (int i = 0; i < action.Parameters.Count; i++)
            {
                HeliosActionParameter parameter = action.Parameters[i];
                AddActionParameterControl(card.transform, parameter, valueReaders);
            }

            Button run = Widgets.CreateButton("Run", card.transform, "Run", () =>
            {
                List<string> values = new List<string>(valueReaders.Count);
                for (int i = 0; i < valueReaders.Count; i++)
                    values.Add(valueReaders[i]());

                HeliosActionResult result = action.Invoke(values);
                if (!result.Success)
                    UnityEngine.Debug.LogWarning($"Helios action failed: {result.Message}");
            });
            Widgets.AddLayout(run.gameObject, 36f);
        }

        private void AddActionParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            GameObject row = CreateActionParameterRow(parent, parameter);

            switch (parameter.ValueKind)
            {
                case HeliosOptionValueKind.Boolean:
                    AddBooleanParameterControl(row.transform, parameter, valueReaders);
                    break;
                case HeliosOptionValueKind.Enum:
                    AddEnumParameterControl(row.transform, parameter, valueReaders);
                    break;
                case HeliosOptionValueKind.Integer:
                case HeliosOptionValueKind.Float:
                    AddNumericParameterControl(row.transform, parameter, valueReaders);
                    break;
                case HeliosOptionValueKind.String:
                default:
                    AddTextParameterControl(row.transform, parameter, valueReaders);
                    break;
            }
        }

        private GameObject CreateActionParameterRow(Transform parent, HeliosActionParameter parameter)
        {
            GameObject row = Widgets.CreatePanel($"Param_{parameter.Name}", parent, new Color(0f, 0f, 0f, 0f));
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = true;
            Widgets.AddLayout(row, 36f);

            Text label = Widgets.CreateText("Label", row.transform, FormatParameterLabel(parameter), 13);
            Widgets.AddLayout(label.gameObject, -1f, 30f);
            return row;
        }

        private void AddTextParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            InputField input = Widgets.CreateInput("Value", parent, parameter.DefaultText, null);
            input.text = parameter.DefaultText;
            Widgets.AddLayout(input.gameObject, -1f, 30f);
            valueReaders.Add(() => input != null ? input.text : string.Empty);
        }

        private void AddBooleanParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            bool value = bool.TryParse(parameter.DefaultText, out bool parsed) && parsed;
            Button button = Widgets.CreateButton("BooleanValue", parent, value ? "true" : "false", null);
            Text buttonText = button.GetComponentInChildren<Text>();
            button.onClick.AddListener(() =>
            {
                value = !value;
                if (buttonText != null)
                    buttonText.text = value ? "true" : "false";
            });
            Widgets.AddLayout(button.gameObject, -1f, 30f);
            valueReaders.Add(() => value ? "true" : "false");
        }

        private void AddEnumParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            string[] names = Enum.GetNames(parameter.ParameterType);
            int index = Array.IndexOf(names, parameter.DefaultText);
            if (index < 0)
                index = 0;

            Button button = Widgets.CreateButton("EnumValue", parent, names.Length == 0 ? string.Empty : names[index], null);
            Text buttonText = button.GetComponentInChildren<Text>();
            button.onClick.AddListener(() =>
            {
                if (names.Length == 0)
                    return;

                index = (index + 1) % names.Length;
                if (buttonText != null)
                    buttonText.text = names[index];
            });
            Widgets.AddLayout(button.gameObject, -1f, 30f);
            valueReaders.Add(() => names.Length == 0 ? string.Empty : names[index]);
        }

        private void AddNumericParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            if (parameter.Range == null)
            {
                AddFreeNumericParameterControl(parent, parameter, valueReaders);
                return;
            }

            float value = ParseParameterFloat(parameter.DefaultText);
            value = ClampParameterValue(parameter, value);

            Button minus = Widgets.CreateButton("Minus", parent, "-", null);
            Widgets.AddLayout(minus.gameObject, 34f, 30f);

            InputField input = Widgets.CreateInput("Value", parent, FormatParameterNumber(parameter, value), null);
            input.text = FormatParameterNumber(parameter, value);
            Widgets.AddLayout(input.gameObject, -1f, 30f);

            Button plus = Widgets.CreateButton("Plus", parent, "+", null);
            Widgets.AddLayout(plus.gameObject, 34f, 30f);

            minus.onClick.AddListener(() =>
            {
                value = ClampParameterValue(parameter, ParseParameterFloat(input.text) - GetParameterStep(parameter));
                input.text = FormatParameterNumber(parameter, value);
            });

            plus.onClick.AddListener(() =>
            {
                value = ClampParameterValue(parameter, ParseParameterFloat(input.text) + GetParameterStep(parameter));
                input.text = FormatParameterNumber(parameter, value);
            });

            input.onEndEdit.AddListener(text =>
            {
                value = ClampParameterValue(parameter, ParseParameterFloat(text));
                input.text = FormatParameterNumber(parameter, value);
            });

            valueReaders.Add(() =>
            {
                value = ClampParameterValue(parameter, ParseParameterFloat(input.text));
                string formatted = FormatParameterNumber(parameter, value);
                input.text = formatted;
                return formatted;
            });
        }

        private void AddFreeNumericParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            float value = ParseParameterFloat(parameter.DefaultText);
            InputField input = Widgets.CreateInput("Value", parent, FormatParameterNumber(parameter, value), null);
            input.text = FormatParameterNumber(parameter, value);
            Widgets.AddLayout(input.gameObject, -1f, 30f);

            input.onEndEdit.AddListener(text =>
            {
                value = ParseParameterFloat(text);
                input.text = FormatParameterNumber(parameter, value);
            });

            valueReaders.Add(() =>
            {
                value = ParseParameterFloat(input.text);
                string formatted = FormatParameterNumber(parameter, value);
                input.text = formatted;
                return formatted;
            });
        }

        private static string FormatParameterLabel(HeliosActionParameter parameter)
        {
            string typeName;
            switch (parameter.ValueKind)
            {
                case HeliosOptionValueKind.Boolean:
                    typeName = "Boolean";
                    break;
                case HeliosOptionValueKind.Enum:
                    typeName = $"Enum/{parameter.ParameterType.Name}";
                    break;
                case HeliosOptionValueKind.Integer:
                case HeliosOptionValueKind.Float:
                    typeName = parameter.Range == null ? parameter.ParameterType.Name : $"Range {parameter.Range.Min:g}-{parameter.Range.Max:g}";
                    break;
                case HeliosOptionValueKind.String:
                    typeName = "String";
                    break;
                default:
                    typeName = parameter.ParameterType.Name;
                    break;
            }

            return $"{parameter.Name} ({typeName})";
        }

        private static float ParseParameterFloat(string text)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }

        private static float ClampParameterValue(HeliosActionParameter parameter, float value)
        {
            if (parameter.Range != null)
                value = Mathf.Clamp(value, parameter.Range.Min, parameter.Range.Max);

            return parameter.ValueKind == HeliosOptionValueKind.Integer ? Mathf.Round(value) : value;
        }

        private static float GetParameterStep(HeliosActionParameter parameter)
        {
            float step = parameter.Range != null ? parameter.Range.Step : 1f;
            return Mathf.Approximately(step, 0f) ? 1f : Mathf.Abs(step);
        }

        private static string FormatParameterNumber(HeliosActionParameter parameter, float value)
        {
            if (parameter.ValueKind == HeliosOptionValueKind.Integer)
                return Mathf.RoundToInt(value).ToString(CultureInfo.InvariantCulture);

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static int CompareEntries(OptionTabEntry left, OptionTabEntry right)
        {
            int category = string.Compare(NormalizeCategory(left.Category), NormalizeCategory(right.Category), StringComparison.Ordinal);
            if (category != 0)
                return category;
            if (left.Order != right.Order)
                return left.Order.CompareTo(right.Order);
            int name = string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
            if (name != 0)
                return name;
            return left.IsAction.CompareTo(right.IsAction);
        }

        private bool Matches(string displayName, string category)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            return displayName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (!string.IsNullOrEmpty(category) && category.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrEmpty(category) ? "General" : category;
        }

        private sealed class OptionValueBinding
        {
            private readonly IHeliosValueOption _option;
            private readonly Text _text;

            public OptionValueBinding(IHeliosValueOption option, Text text)
            {
                _option = option;
                _text = text;
            }

            public void Refresh()
            {
                if (_text != null)
                    _text.text = _option.GetDisplayValue();
            }
        }

        private sealed class OptionTabEntry
        {
            public OptionTabEntry(IHeliosValueOption option)
            {
                Option = option;
            }

            public OptionTabEntry(IHeliosActionOption action)
            {
                Action = action;
            }

            public IHeliosValueOption Option { get; }
            public IHeliosActionOption Action { get; }
            public bool IsAction => Action != null;
            public string Category => Option != null ? Option.Category : Action.Category;
            public string DisplayName => Option != null ? Option.DisplayName : Action.DisplayName;
            public int Order => Option != null ? Option.Order : Action.Order;
        }
    }

    public sealed class HeliosSystemInfoTab : HeliosTabBase
    {
        private Text _info;
        private float _lastRefresh;

        public override string Title => "System";
        public override int Order => 30;

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            _info = widgets.CreateText("SystemInfo", parent, string.Empty, 14, TextAnchor.UpperLeft);
        }

        public override void Refresh()
        {
            if (_info == null || Time.unscaledTime - _lastRefresh < 1f)
                return;

            _lastRefresh = Time.unscaledTime;
            _info.text = Context.Service.SystemInfo.ExportText();
        }
    }

    public sealed class HeliosBugReporterTab : HeliosTabBase
    {
        private InputField _description;
        private Text _status;
        private HeliosBugReport _lastReport;

        public override string Title => "Bug Reporter";
        public override int Order => 40;

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            _description = widgets.CreateInput("Description", parent, "Describe the issue, reproduction steps, expected result...", null);
            _description.lineType = InputField.LineType.MultiLineNewline;
            widgets.AddLayout(_description.gameObject, 160f);

            GameObject controls = widgets.CreatePanel("ReportControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = 6f;
            widgets.AddLayout(controls, 44f);

            widgets.CreateButton("Build", controls.transform, "Build Report", BuildReport);
            widgets.CreateButton("Local", controls.transform, "Local", () => Submit(HeliosReportTransportKind.LocalExport));
            widgets.CreateButton("Webhook", controls.transform, "Webhook", () => Submit(HeliosReportTransportKind.Webhook));
            widgets.CreateButton("Share", controls.transform, "Share", () => Submit(HeliosReportTransportKind.NativeShare));

            _status = widgets.CreateText("Status", parent, "No report built yet.", 14);
            widgets.AddLayout(_status.gameObject, 60f);
        }

        private void BuildReport()
        {
            _status.text = "Building report...";
            Context.Root.Run(Context.Service.Reporting.BuildReport(_description.text, Context.Service.Settings, OnReportBuilt));
        }

        private void Submit(HeliosReportTransportKind kind)
        {
            if (_lastReport == null)
            {
                BuildAndSubmit(kind);
                return;
            }

            IHeliosReportTransport transport = Context.Service.Reporting.GetTransport(kind);
            if (transport == null)
            {
                _status.text = $"Transport {kind} unavailable.";
                return;
            }

            _status.text = $"Submitting via {transport.DisplayName}...";
            Context.Root.Run(transport.Submit(_lastReport, Context.Service.Settings, OnReportSubmitted));
        }

        private void BuildAndSubmit(HeliosReportTransportKind kind)
        {
            _status.text = "Building report before submit...";
            Context.Root.Run(BuildAndSubmitRoutine(kind));
        }

        private IEnumerator BuildAndSubmitRoutine(HeliosReportTransportKind kind)
        {
            HeliosBugReport built = null;
            HeliosReportResult buildResult = null;
            yield return Context.Service.Reporting.BuildReport(_description.text, Context.Service.Settings, (report, result) =>
            {
                built = report;
                buildResult = result;
            });

            if (buildResult == null || !buildResult.Success)
            {
                _status.text = buildResult != null ? buildResult.Message : "Report build failed.";
                yield break;
            }

            _lastReport = built;
            Submit(kind);
        }

        private void OnReportBuilt(HeliosBugReport report, HeliosReportResult result)
        {
            _lastReport = report;
            _status.text = result.Message;
        }

        private void OnReportSubmitted(HeliosReportResult result)
        {
            _status.text = result.Success ? $"{result.Message}\n{result.Path}" : result.Message;
        }
    }
}
