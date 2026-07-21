using System;
using System.Collections;
using System.Collections.Generic;
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
        private Text _summary;
        private Text _history;
        private float _lastRefresh;

        public override string Title => "Profiler";
        public override int Order => 10;

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            GameObject controls = widgets.CreatePanel("ProfilerControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = 6f;
            widgets.AddLayout(controls, 40f);
            widgets.CreateButton("Reset", controls.transform, "Reset", () => Context.Service.Profiler.Reset());

            _summary = widgets.CreateText("Summary", parent, "Profiler warming up...", 18);
            widgets.AddLayout(_summary.gameObject, 90f);
            _history = widgets.CreateText("History", parent, string.Empty, 13, TextAnchor.UpperLeft);
        }

        public override void Refresh()
        {
            if (Time.unscaledTime - _lastRefresh < Context.Service.Settings.ProfilerRefreshInterval)
                return;

            _lastRefresh = Time.unscaledTime;
            HeliosProfilerSample latest = Context.Service.Profiler.Latest;
            if (latest == null || _summary == null)
                return;

            _summary.text =
                $"FPS: {latest.Fps:F1}\nFrame: {latest.FrameMs:F2} ms\nMemory: {FormatBytes(latest.TotalMemory)}\nGC/frame: {FormatBytes(latest.GcAllocated)}\nDraw calls: {FormatUnavailable(latest.DrawCalls)}";

            IReadOnlyList<HeliosProfilerSample> history = Context.Service.Profiler.History;
            var builder = new StringBuilder(1024);
            int start = Mathf.Max(0, history.Count - 24);
            for (int i = start; i < history.Count; i++)
            {
                HeliosProfilerSample sample = history[i];
                int bar = Mathf.Clamp(Mathf.RoundToInt(sample.FrameMs / 2f), 1, 40);
                builder.Append(sample.FrameMs.ToString("F1")).Append("ms ");
                for (int j = 0; j < bar; j++)
                    builder.Append('|');
                builder.AppendLine();
            }

            _history.text = builder.ToString();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0L) return "unavailable";
            return $"{bytes / (1024f * 1024f):F1} MB";
        }

        private static string FormatUnavailable(long value)
        {
            return value < 0L ? "unavailable" : value.ToString();
        }
    }

    public sealed class HeliosOptionsTab : HeliosTabBase
    {
        private RectTransform _content;
        private string _search = string.Empty;
        private int _lastFingerprint;

        public override string Title => "Options";
        public override int Order => 20;

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            GameObject controls = widgets.CreatePanel("OptionsControls", parent, new Color(0f, 0f, 0f, 0f));
            controls.AddComponent<HorizontalLayoutGroup>().spacing = 6f;
            widgets.AddLayout(controls, 40f);
            widgets.CreateButton("Refresh", controls.transform, "Refresh", () =>
            {
                Context.Service.Options.Refresh();
                _lastFingerprint = 0;
                Rebuild();
            });
            InputField search = widgets.CreateInput("Search", controls.transform, "Search options", value =>
            {
                _search = value ?? string.Empty;
                _lastFingerprint = 0;
                Rebuild();
            });
            widgets.AddLayout(search.gameObject, 36f);

            ScrollRect scroll = widgets.CreateScrollView("OptionsList", parent, out _content);
            widgets.AddLayout(scroll.gameObject, 760f, 320f);
        }

        public override void Refresh()
        {
            int fingerprint = Context.Service.Options.Options.Count * 31 +
                              Context.Service.Options.Actions.Count * 17 +
                              Context.Service.Actions.Count;
            if (fingerprint != _lastFingerprint)
                Rebuild();
        }

        private void Rebuild()
        {
            if (_content == null)
                return;

            Widgets.Clear(_content);
            string currentCategory = null;
            IReadOnlyList<HeliosOptionMember> options = Context.Service.Options.Options;
            for (int i = 0; i < options.Count; i++)
            {
                HeliosOptionMember option = options[i];
                if (!Matches(option.DisplayName, option.Category))
                    continue;

                AddCategory(ref currentCategory, option.Category);
                AddOption(option);
            }

            IReadOnlyList<HeliosReflectedAction> reflected = Context.Service.Options.Actions;
            for (int i = 0; i < reflected.Count; i++)
            {
                HeliosReflectedAction action = reflected[i];
                if (!Matches(action.DisplayName, action.Category))
                    continue;

                AddCategory(ref currentCategory, action.Category);
                AddReflectedAction(action);
            }

            IReadOnlyList<HeliosActionDefinition> actions = Context.Service.Actions;
            for (int i = 0; i < actions.Count; i++)
            {
                HeliosActionDefinition action = actions[i];
                if (!Matches(action.DisplayName, action.Category))
                    continue;

                AddCategory(ref currentCategory, action.Category);
                AddRuntimeAction(action);
            }

            _lastFingerprint = options.Count * 31 + reflected.Count * 17 + actions.Count;
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

        private void AddOption(HeliosOptionMember option)
        {
            GameObject row = Widgets.CreatePanel($"Option_{option.DisplayName}", _content, new Color(0.08f, 0.1f, 0.13f, 0.96f));
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(6, 6, 4, 4);
            Widgets.AddLayout(row, 44f);

            Text label = Widgets.CreateText("Label", row.transform, option.DisplayName, 14);
            Widgets.AddLayout(label.gameObject, -1f, 32f);
            Text value = Widgets.CreateText("Value", row.transform, option.GetDisplayValue(), 14, TextAnchor.MiddleRight);
            Widgets.AddLayout(value.gameObject, -1f, 32f);

            if (option.IsReadOnly)
                return;

            if (option.ValueKind == HeliosOptionValueKind.Boolean)
            {
                Widgets.CreateButton("Toggle", row.transform, "Toggle", () =>
                {
                    option.ToggleBoolean();
                    Rebuild();
                });
            }
            else if (option.ValueKind == HeliosOptionValueKind.Enum)
            {
                Widgets.CreateButton("Cycle", row.transform, "Next", () =>
                {
                    option.CycleEnum();
                    Rebuild();
                });
            }
            else if (option.ValueKind == HeliosOptionValueKind.Integer || option.ValueKind == HeliosOptionValueKind.Float)
            {
                Widgets.CreateButton("Minus", row.transform, "-", () =>
                {
                    option.Adjust(-1f);
                    Rebuild();
                });
                Widgets.CreateButton("Plus", row.transform, "+", () =>
                {
                    option.Adjust(1f);
                    Rebuild();
                });
            }

            Widgets.CreateButton("Reset", row.transform, "Reset", () =>
            {
                option.Reset();
                Rebuild();
            });
        }

        private void AddReflectedAction(HeliosReflectedAction action)
        {
            Button button = Widgets.CreateButton($"Action_{action.DisplayName}", _content, action.Pin ? $"Pinned: {action.DisplayName}" : action.DisplayName, () =>
            {
                HeliosActionResult result = action.Invoke();
                if (!result.Success)
                    UnityEngine.Debug.LogWarning($"Helios action failed: {result.Message}");
            });
            Widgets.AddLayout(button.gameObject, 40f);
        }

        private void AddRuntimeAction(HeliosActionDefinition action)
        {
            Button button = Widgets.CreateButton($"RuntimeAction_{action.DisplayName}", _content, action.DisplayName, () =>
            {
                HeliosActionResult result = action.Invoke();
                if (!result.Success)
                    UnityEngine.Debug.LogWarning($"Helios action failed: {result.Message}");
            });
            Widgets.AddLayout(button.gameObject, 40f);
        }

        private bool Matches(string displayName, string category)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            return displayName.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   (!string.IsNullOrEmpty(category) && category.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
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
