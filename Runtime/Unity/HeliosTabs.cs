using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliosDebugger
{
    public sealed class HeliosConsoleTab : HeliosTabBase, IHeliosTabIcon
    {
        private readonly HeliosLogFilter _filter = new HeliosLogFilter();
        private HeliosLogQuery _query;
        private HeliosVirtualizedLogList _list;
        private ScrollRect _scroll;
        private TextMeshProUGUI _detail;
        private TextMeshProUGUI _status;
        private bool _paused;
        private int _lastCount = -1;
        private HeliosLogViewEntry _selected;

        public override string Title => "Console";
        public override int Order => 0;
        public Sprite Icon => HeliosIcons.Get(HeliosIcons.Console);

        public override void Initialize(HeliosContext context)
        {
            base.Initialize(context);
            _query = new HeliosLogQuery(context.Service.Logs);
        }

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            GameObject controls = widgets.CreatePanel("ConsoleControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = widgets.Theme.Spacing;
            widgets.AddLayout(controls, 40f);

            widgets.CreateButton("Clear", controls.transform, "Clear", () =>
            {
                Context.Service.Logs.Clear();
                _lastCount = -1;
                RebuildList(false);
            }, HeliosButtonStyle.Danger(widgets.Theme));
            Button pause = widgets.CreateButton("Pause", controls.transform, "Pause", null);
            TextMeshProUGUI pauseLabel = HeliosWidgetFactory.GetButtonLabel(pause);
            pause.onClick.AddListener(() =>
            {
                _paused = !_paused;
                pauseLabel.text = _paused ? "Resume" : "Pause";
            });
            TMP_InputField search = widgets.CreateInput("Search", controls.transform, "Search logs", value =>
            {
                _filter.Search = value ?? string.Empty;
                _lastCount = -1;
                RebuildList(false);
            });
            widgets.AddLayout(search.gameObject, 36f);

            GameObject filters = widgets.CreatePanel("ConsoleFilters", parent, new Color(0f, 0f, 0f, 0f));
            filters.AddComponent<HorizontalLayoutGroup>().spacing = widgets.Theme.Spacing;
            widgets.AddLayout(filters, 38f);
            CreateLevelButton(filters.transform, "Log", HeliosLogLevelMask.Log);
            CreateLevelButton(filters.transform, "Warn", HeliosLogLevelMask.Warning);
            CreateLevelButton(filters.transform, "Error", HeliosLogLevelMask.Error);
            CreateLevelButton(filters.transform, "Exception", HeliosLogLevelMask.Exception);
            CreateLevelButton(filters.transform, "Assert", HeliosLogLevelMask.Assert);

            Button collapse = widgets.CreateButton("Collapse", filters.transform, "Collapse: Off", null);
            TextMeshProUGUI collapseLabel = HeliosWidgetFactory.GetButtonLabel(collapse);
            collapse.onClick.AddListener(() =>
            {
                _filter.CollapseDuplicates = !_filter.CollapseDuplicates;
                collapseLabel.text = _filter.CollapseDuplicates ? "Collapse: On" : "Collapse: Off";
                RebuildList(false);
            });
            widgets.CreateButton("Copy", filters.transform, "Copy", CopyVisible);
            widgets.CreateButton("Export", filters.transform, "Export", ExportVisible);

            _scroll = widgets.CreateScrollView("ConsoleList", parent, out RectTransform content);
            widgets.AddLayout(_scroll.gameObject, 480f, 260f);
            _list = _scroll.gameObject.AddComponent<HeliosVirtualizedLogList>();
            _list.Initialize(_scroll, content, widgets, widgets.Theme.RowHeight, SelectEntry);

            _detail = widgets.CreateText("LogDetail", parent, "Select a log entry to inspect its full message and stack trace.", 13, TextAnchor.UpperLeft);
            _detail.color = widgets.Theme.MutedText;
            widgets.AddLayout(_detail.gameObject, 150f, 100f);

            GameObject detailControls = widgets.CreatePanel("DetailControls", parent, new Color(0f, 0f, 0f, 0f));
            detailControls.AddComponent<HorizontalLayoutGroup>().spacing = widgets.Theme.Spacing;
            widgets.AddLayout(detailControls, 36f);
            widgets.CreateButton("CopyMessage", detailControls.transform, "Copy Message", () =>
            {
                if (_selected != null)
                    GUIUtility.systemCopyBuffer = _selected.Representative.Message;
            });
            widgets.CreateButton("CopyStack", detailControls.transform, "Copy Stack", () =>
            {
                if (_selected != null)
                    GUIUtility.systemCopyBuffer = _selected.Representative.StackTrace;
            });
            _status = widgets.CreateText("ConsoleStatus", detailControls.transform, string.Empty, 12);
            widgets.AddLayout(_status.gameObject, -1f, 28f);
            RebuildList(true);
        }

        public override void Refresh()
        {
            if (_paused || _list == null || _query == null)
                return;

            int count = Context.Service.Logs.Snapshot().Count;
            if (count != _lastCount)
                RebuildList(true);
        }

        private void RebuildList(bool preserveBottom)
        {
            if (_list == null || _query == null)
                return;

            bool stickToBottom = preserveBottom && (_lastCount < 0 || _scroll.verticalNormalizedPosition <= 0.02f);
            IReadOnlyList<HeliosLogViewEntry> entries = _query.Execute(_filter);
            _list.SetEntries(entries, stickToBottom);
            _lastCount = Context.Service.Logs.Snapshot().Count;
            if (_status != null)
                _status.text = $"{entries.Count} visible / {_lastCount} captured";
        }

        private void CreateLevelButton(Transform parent, string label, HeliosLogLevelMask level)
        {
            Button button = Widgets.CreateButton($"Filter_{level}", parent, $"✓ {label}", null, HeliosButtonStyle.Ghost(Widgets.Theme));
            TextMeshProUGUI text = HeliosWidgetFactory.GetButtonLabel(button);
            button.onClick.AddListener(() =>
            {
                bool enabled = (_filter.Levels & level) != 0;
                _filter.Levels = enabled ? _filter.Levels & ~level : _filter.Levels | level;
                text.text = enabled ? label : $"✓ {label}";
                text.color = enabled ? Widgets.Theme.MutedText : Widgets.Theme.Accent;
                RebuildList(false);
            });
            text.color = Widgets.Theme.Accent;
        }

        private void SelectEntry(HeliosLogViewEntry entry)
        {
            _selected = entry;
            HeliosLogEntry log = entry.Representative;
            string count = entry.Count > 1 ? $"\nOccurrences: {entry.Count}" : string.Empty;
            _detail.text = $"[{log.Timestamp:O}] [{log.Level}] {log.Message}{count}\n\n{log.StackTrace}";
        }

        private void CopyVisible()
        {
            if (_query == null)
                return;
            GUIUtility.systemCopyBuffer = _query.Export(_filter);
            if (_status != null)
                _status.text = "Visible logs copied.";
        }

        private void ExportVisible()
        {
            if (_query == null)
                return;

            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "HeliosLogs");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(path, _query.Export(_filter));
                _status.text = path;
            }
            catch (Exception exception)
            {
                _status.text = $"Export failed: {exception.Message}";
            }
        }
    }

    public sealed class HeliosProfilerTab : HeliosTabBase, IHeliosTabIcon
    {
        private readonly List<float> _frameTimes = new List<float>();
        private HeliosTimeSeriesGraph _graph;
        private TextMeshProUGUI _fpsValue;
        private TextMeshProUGUI _frameValue;
        private TextMeshProUGUI _scaleValue;
        private TextMeshProUGUI _managedValue;
        private TextMeshProUGUI _usedValue;
        private TextMeshProUGUI _reservedValue;
        private TextMeshProUGUI _gcValue;
        private TextMeshProUGUI _drawCallsValue;
        private TextMeshProUGUI _scriptsValue;
        private TextMeshProUGUI _status;
        private Image _managedFill;
        private Image _reservedFill;
        private Button _cleanButton;
        private float _lastRefresh;
        private bool _cleaning;

        public override string Title => "Profiler";
        public override int Order => 10;
        public Sprite Icon => HeliosIcons.Get(HeliosIcons.Profiler);

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            GameObject controls = widgets.CreatePanel("ProfilerControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = widgets.Theme.Spacing;
            widgets.AddLayout(controls, 40f);
            widgets.CreateButton("Reset", controls.transform, "Reset", ResetHistory);
            widgets.CreateButton("GCCollect", controls.transform, "GC Collect", CollectGarbage);
            _cleanButton = widgets.CreateButton("Clean", controls.transform, "Clean", CleanUnusedAssets);

            _graph = widgets.CreateTimeSeriesGraph("FrameGraph", parent, widgets.Theme.Navigation);
            Color gridColor = widgets.Theme.MutedText;
            gridColor.a = 0.2f;
            _graph.Configure(
                33.33f,
                16.67f,
                33.33f,
                widgets.Theme.Accent,
                widgets.Theme.Warning,
                widgets.Theme.Error,
                gridColor);
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
            SetFlexibleWidth(widgets.CreateMetricCard("ScriptsCard", detailMetrics.transform, "Scripts", out _scriptsValue));

            _status = widgets.CreateText("ProfilerStatus", parent, "Profiler warming up...", 14);
            _status.color = widgets.Theme.MutedText;
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
            _scriptsValue.text = latest.ScriptsTime < 0L ? "Unavailable" : $"{latest.ScriptsTime / 1000000f:F2} ms";

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

        private GameObject CreateMemoryCard(HeliosWidgetFactory widgets, string name, Transform parent, string title, out TextMeshProUGUI value, out Image fill)
        {
            GameObject card = widgets.CreateSurface(name, parent, widgets.Theme.Input, widgets.Theme.CardCornerRadius);
            widgets.AddBorder(card, widgets.Theme.Border);
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 9, 9);
            layout.spacing = widgets.Theme.SpaceXs;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;

            TextMeshProUGUI titleText = widgets.CreateText("Title", card.transform, title, widgets.Theme.CaptionFontSize);
            titleText.color = widgets.Theme.MutedText;
            titleText.fontStyle = FontStyles.UpperCase;
            widgets.AddLayout(titleText.gameObject, 18f);

            value = widgets.CreateText("Value", card.transform, "--", widgets.Theme.TitleFontSize);
            value.fontStyle = FontStyles.Bold;
            widgets.AddLayout(value.gameObject, 28f);

            Color barBackground = widgets.Theme.MutedText;
            barBackground.a = 0.12f;
            fill = widgets.CreateFillBar(
                "UsageBar",
                card.transform,
                barBackground,
                widgets.Theme.Accent);
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

        private Color ColorForFrame(float frameMs)
        {
            if (frameMs >= 33.33f)
                return Widgets.Theme.Error;
            if (frameMs >= 16.67f)
                return Widgets.Theme.Warning;
            return Widgets.Theme.Accent;
        }
    }

    public sealed class HeliosOptionsTab : HeliosTabBase, IHeliosTabOpenHandler, IHeliosTabIcon
    {
        private const int ForceRebuildRevision = -1;

        private readonly List<Action> _refreshBindings = new List<Action>();
        private RectTransform _content;
        private string _search = string.Empty;
        private int _lastRevision = ForceRebuildRevision;
        private bool _refreshRegistryOnNextRefresh;

        public override string Title => "Options";
        public override int Order => 20;
        public Sprite Icon => HeliosIcons.Get(HeliosIcons.Options);

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
            controls.AddComponent<HorizontalLayoutGroup>().spacing = widgets.Theme.Spacing;
            widgets.AddLayout(controls, 40f);
            TMP_InputField search = widgets.CreateInput("Search", controls.transform, "Search options", value =>
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

            _refreshBindings.Clear();
            Widgets.Clear(_content);
            List<OptionTabEntry> entries = new List<OptionTabEntry>();
            IReadOnlyList<IHeliosValueOption> options = Context.Service.Options.Options;
            for (int i = 0; i < options.Count; i++)
            {
                IHeliosValueOption option = options[i];
                if (Matches(option.DisplayName, option.Category, option.Description))
                    entries.Add(new OptionTabEntry(option));
            }

            IReadOnlyList<IHeliosActionOption> reflected = Context.Service.Options.Actions;
            for (int i = 0; i < reflected.Count; i++)
            {
                IHeliosActionOption action = reflected[i];
                if (Matches(action.DisplayName, action.Category, action.Description))
                    entries.Add(new OptionTabEntry(action));
            }

            IReadOnlyList<HeliosActionDefinition> actions = Context.Service.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                HeliosActionDefinition action = actions[i];
                if (Matches(action.DisplayName, action.Category, action.Description))
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
            for (int i = 0; i < _refreshBindings.Count; i++)
                _refreshBindings[i]();
        }

        private void AddCategory(ref string currentCategory, string category)
        {
            string next = string.IsNullOrEmpty(category) ? "General" : category;
            if (currentCategory == next)
                return;

            currentCategory = next;
            Widgets.CreateSectionHeader($"Category_{next}", _content, next);
        }

        private void AddOption(IHeliosValueOption option)
        {
            bool hasDescription = !string.IsNullOrWhiteSpace(option.Description);
            GameObject card = Widgets.CreateSurface($"Option_{option.DisplayName}", _content, Widgets.Theme.Row, Widgets.Theme.CardCornerRadius);
            Widgets.AddBorder(card, Widgets.Theme.Border);
            VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(10, 10, 7, 7);
            cardLayout.spacing = 4f;
            cardLayout.childControlWidth = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            Widgets.AddLayout(card, hasDescription ? 70f : 48f);

            GameObject row = Widgets.CreatePanel("Controls", card.transform, Color.clear);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            Widgets.AddLayout(row, 38f);

            TextMeshProUGUI label = Widgets.CreateText("Label", row.transform, option.DisplayName, 14);
            label.fontStyle = FontStyles.Bold;
            Widgets.AddLayout(label.gameObject, -1f, 32f);

            IHeliosOptionControlBuilder builder = FindControlBuilder(option);
            HeliosOptionControlContext controlContext = new HeliosOptionControlContext(
                Widgets,
                row.transform,
                refresh => _refreshBindings.Add(refresh));
            builder.Build(controlContext, option);

            if (!option.IsReadOnly)
            {
                Widgets.CreateButton("Reset", row.transform, "Reset", () =>
                {
                    option.Reset();
                    RefreshOptionValues();
                });
            }

            if (hasDescription)
            {
                TextMeshProUGUI description = Widgets.CreateText("Description", card.transform, option.Description, 12);
                description.color = Widgets.Theme.MutedText;
                Widgets.AddLayout(description.gameObject, 22f);
            }
        }

        private IHeliosOptionControlBuilder FindControlBuilder(IHeliosValueOption option)
        {
            IReadOnlyList<IHeliosOptionControlBuilder> builders = Context.Service.OptionControlBuilders;
            for (int i = 0; i < builders.Count; i++)
            {
                if (builders[i].CanBuild(option))
                    return builders[i];
            }

            throw new InvalidOperationException($"No Helios option control can render {option.ValueType.FullName}.");
        }

        private void AddAction(IHeliosActionOption action)
        {
            if (action.Parameters.Count == 0)
            {
                string label = string.IsNullOrWhiteSpace(action.Description)
                    ? action.DisplayName
                    : $"{action.DisplayName}\n{action.Description}";
                Button button = Widgets.CreateButton($"Action_{action.DisplayName}", _content, label, () =>
                {
                    HeliosActionResult result = action.Invoke();
                    if (!result.Success)
                        UnityEngine.Debug.LogWarning($"Helios action failed: {result.Message}");
                });
                Widgets.AddLayout(button.gameObject, string.IsNullOrWhiteSpace(action.Description) ? 40f : 58f);
                return;
            }

            GameObject card = Widgets.CreateSurface($"Action_{action.DisplayName}", _content, Widgets.Theme.Row, Widgets.Theme.CardCornerRadius);
            Widgets.AddBorder(card, Widgets.Theme.Border);
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            float descriptionHeight = string.IsNullOrWhiteSpace(action.Description) ? 0f : 24f;
            Widgets.AddLayout(card, Mathf.Max(96f, 84f + descriptionHeight + action.Parameters.Count * 42f));

            TextMeshProUGUI title = Widgets.CreateText("Title", card.transform, action.DisplayName, 14);
            title.color = Widgets.Theme.Text;
            Widgets.AddLayout(title.gameObject, 24f);
            if (!string.IsNullOrWhiteSpace(action.Description))
            {
                TextMeshProUGUI description = Widgets.CreateText("Description", card.transform, action.Description, 12);
                description.color = Widgets.Theme.MutedText;
                Widgets.AddLayout(description.gameObject, 22f);
            }

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

            TextMeshProUGUI label = Widgets.CreateText("Label", row.transform, FormatParameterLabel(parameter), 13);
            Widgets.AddLayout(label.gameObject, -1f, 30f);
            return row;
        }

        private void AddTextParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            TMP_InputField input = Widgets.CreateInput("Value", parent, parameter.DefaultText, null);
            input.text = parameter.DefaultText;
            Widgets.AddLayout(input.gameObject, -1f, 30f);
            valueReaders.Add(() => input != null ? input.text : string.Empty);
        }

        private void AddBooleanParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            bool value = bool.TryParse(parameter.DefaultText, out bool parsed) && parsed;
            HeliosSwitchControl control = Widgets.CreateSwitch("BooleanValue", parent, value, value ? "true" : "false", null);
            control.Button.onClick.AddListener(() =>
            {
                value = !value;
                control.SetValue(value, value ? "true" : "false");
            });
            Widgets.AddLayout(control.Button.gameObject, -1f, 30f);
            valueReaders.Add(() => value ? "true" : "false");
        }

        private void AddEnumParameterControl(Transform parent, HeliosActionParameter parameter, List<Func<string>> valueReaders)
        {
            string[] names = Enum.GetNames(parameter.ParameterType);
            int index = Array.IndexOf(names, parameter.DefaultText);
            if (index < 0)
                index = 0;

            Button button = Widgets.CreateButton("EnumValue", parent, names.Length == 0 ? string.Empty : names[index], null);
            TextMeshProUGUI buttonText = HeliosWidgetFactory.GetButtonLabel(button);
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

            TMP_InputField input = Widgets.CreateInput("Value", parent, FormatParameterNumber(parameter, value), null);
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
            TMP_InputField input = Widgets.CreateInput("Value", parent, FormatParameterNumber(parameter, value), null);
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

        private bool Matches(string displayName, string category, string description)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            return displayName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (!string.IsNullOrEmpty(category) && category.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrEmpty(description) && description.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrEmpty(category) ? "General" : category;
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

    public sealed class HeliosSystemInfoTab : HeliosTabBase, IHeliosTabOpenHandler, IHeliosTabIcon
    {
        private TextMeshProUGUI _info;
        private float _lastRefresh;
        private bool _accessGranted;

        public override string Title => "System";
        public override int Order => 30;
        public Sprite Icon => HeliosIcons.Get(HeliosIcons.System);

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            _info = widgets.CreateText("SystemInfo", parent, "Access required.", 14, TextAnchor.UpperLeft);
        }

        public void OnOpened()
        {
            _accessGranted = false;
            Context.Service.Access.Request(
                new HeliosAccessRequest(HeliosAccessOperation.ViewSensitiveSystemInfo, "system"),
                () =>
                {
                    _accessGranted = true;
                    _lastRefresh = 0f;
                    Refresh();
                });
        }

        public override void Refresh()
        {
            if (!_accessGranted || _info == null || Time.unscaledTime - _lastRefresh < 1f)
                return;

            _lastRefresh = Time.unscaledTime;
            _info.text = Context.Service.SystemInfo.ExportText();
        }
    }

    public sealed class HeliosBugReporterTab : HeliosTabBase, IHeliosTabIcon
    {
        private TMP_InputField _description;
        private TextMeshProUGUI _status;
        private HeliosReportBundle _lastReport;
        private HeliosReportCancellationSource _cancellation;

        public override string Title => "Bug Reporter";
        public override int Order => 40;
        public Sprite Icon => HeliosIcons.Get(HeliosIcons.BugReporter);

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            _description = widgets.CreateInput("Description", parent, "Describe the issue, reproduction steps, expected result...", null);
            _description.lineType = TMP_InputField.LineType.MultiLineNewline;
            widgets.AddLayout(_description.gameObject, 160f);

            GameObject controls = widgets.CreatePanel("ReportControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = widgets.Theme.Spacing;
            widgets.AddLayout(controls, 44f);

            widgets.CreateButton("Build", controls.transform, "Build Report", BuildReport);
            widgets.CreateButton("Default", controls.transform, "Submit Default", () => Submit(GetDefaultTransportId()));
            widgets.CreateButton("Cancel", controls.transform, "Cancel", Cancel);
            IReadOnlyList<IHeliosReportTransport> transports =
                Context.Service.Reporting.Transports.GetAvailableTransports();
            for (int i = 0; i < transports.Count; i++)
            {
                IHeliosReportTransport transport = transports[i];
                widgets.CreateButton(
                    $"Transport_{transport.Id.Value}",
                    controls.transform,
                    transport.DisplayName,
                    () => Submit(transport.Id));
            }

            _status = widgets.CreateText("Status", parent, "No report built yet.", 14);
            widgets.AddLayout(_status.gameObject, 60f);
        }

        private void BuildReport()
        {
            Context.Service.Access.Request(
                new HeliosAccessRequest(HeliosAccessOperation.ViewSensitiveSystemInfo, "bug-report"),
                BuildReportAllowed);
        }

        private void BuildReportAllowed()
        {
            if (_status == null || _description == null)
                return;
            _cancellation = new HeliosReportCancellationSource();
            _status.text = "Building report...";
            Context.Root.Run(Context.Service.Reporting.BuildReport(
                _description.text,
                Context.Service.Settings.MaxReportBytes,
                _cancellation.CreateContext(OnProgress),
                OnReportBuilt));
        }

        private void Submit(HeliosTransportId id)
        {
            Context.Service.Access.Request(
                new HeliosAccessRequest(HeliosAccessOperation.SubmitBugReport, id.Value),
                () => SubmitAllowed(id));
        }

        private void SubmitAllowed(HeliosTransportId id)
        {
            if (_status == null)
                return;
            if (_lastReport == null)
            {
                Context.Service.Access.Request(
                    new HeliosAccessRequest(HeliosAccessOperation.ViewSensitiveSystemInfo, "bug-report"),
                    () => BuildAndSubmit(id));
                return;
            }

            IHeliosReportTransport transport = Context.Service.Reporting.GetTransport(id);
            if (transport == null)
            {
                _status.text = $"Transport {id.Value} unavailable.";
                return;
            }

            _cancellation = new HeliosReportCancellationSource();
            _status.text = $"Submitting via {transport.DisplayName}...";
            Context.Root.Run(Context.Service.Reporting.Submit(
                id,
                _lastReport,
                _cancellation.CreateContext(OnProgress),
                OnReportSubmitted));
        }

        private void BuildAndSubmit(HeliosTransportId id)
        {
            if (_status == null || _description == null)
                return;
            _cancellation = new HeliosReportCancellationSource();
            _status.text = "Building report before submit...";
            Context.Root.Run(BuildAndSubmitRoutine(id));
        }

        private IEnumerator BuildAndSubmitRoutine(HeliosTransportId id)
        {
            HeliosReportBundle built = null;
            HeliosReportResult buildResult = null;
            HeliosReportOperationContext operationContext = _cancellation.CreateContext(OnProgress);
            yield return Context.Service.Reporting.BuildReport(
                _description.text,
                Context.Service.Settings.MaxReportBytes,
                operationContext,
                (report, result) =>
            {
                built = report;
                buildResult = result;
            });

            if (buildResult == null || !buildResult.Success)
            {
                if (_status != null)
                    _status.text = buildResult != null ? buildResult.Message : "Report build failed.";
                yield break;
            }

            _lastReport = built;
            if (_cancellation.IsCancellationRequested)
                yield break;

            yield return Context.Service.Reporting.Submit(
                id,
                _lastReport,
                operationContext,
                OnReportSubmitted);
        }

        private void OnReportBuilt(HeliosReportBundle report, HeliosReportResult result)
        {
            _lastReport = report;
            if (_status != null)
                _status.text = result != null ? result.Message : "Report build failed.";
        }

        private void OnReportSubmitted(HeliosReportResult result)
        {
            if (_status == null)
                return;
            if (result == null)
            {
                _status.text = "Report submission did not complete.";
                return;
            }

            _status.text = result.Success && !string.IsNullOrEmpty(result.Location)
                ? $"{result.Message}\n{result.Location}"
                : result.Message;
        }

        private void OnProgress(HeliosReportProgress progress)
        {
            if (_status != null)
                _status.text = $"{progress.Message} {progress.Normalized:P0}";
        }

        private void Cancel()
        {
            _cancellation?.Cancel();
            if (_status != null)
                _status.text = "Cancelling...";
        }

        private HeliosTransportId GetDefaultTransportId()
        {
            try
            {
                return new HeliosTransportId(Context.Service.Settings.DefaultReportTransportId);
            }
            catch (ArgumentException)
            {
                return HeliosTransportId.LocalExport;
            }
        }

        public override void Dispose()
        {
            _cancellation?.Cancel();
            _status = null;
            _description = null;
            _lastReport = null;
        }
    }
}
