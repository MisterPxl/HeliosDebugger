using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosPinnedOptionsOverlay : HeliosOverlayBase
    {
        private GameObject _bar;

        public override string Id => "helios.pinned-options";
        public override int Order => 0;

        public override void Initialize(HeliosOverlayContext context)
        {
            if (Context != null)
            {
                Context.Service.Options.Changed -= Rebuild;
                Context.Service.ActionsChanged -= Rebuild;
            }

            base.Initialize(context);
            Context.Service.Options.Changed += Rebuild;
            Context.Service.ActionsChanged += Rebuild;
        }

        public override void Dispose()
        {
            if (Context == null)
                return;
            Context.Service.Options.Changed -= Rebuild;
            Context.Service.ActionsChanged -= Rebuild;
        }

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            _bar = widgets.CreatePanel("PinnedBar", parent, widgets.Theme.Navigation);
            RectTransform rect = _bar.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(
                rect,
                new Vector2(0.18f, 0f),
                new Vector2(0.82f, 0f),
                new Vector2(0f, 12f),
                new Vector2(0f, 68f));

            HorizontalLayoutGroup layout = _bar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = widgets.Theme.Spacing;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            int count = AddPinnedValues(_bar.transform);
            count += AddPinnedActions(_bar.transform);
            _bar.SetActive(count > 0);
        }

        public override void Refresh()
        {
            if (_bar == null)
                return;

            IReadOnlyList<IHeliosValueOption> options = Context.Service.Options.Options;
            int bindingIndex = 0;
            for (int i = 0; i < options.Count; i++)
            {
                if (!options[i].Pin || bindingIndex >= _bar.transform.childCount)
                    continue;
                Button button = _bar.transform.GetChild(bindingIndex).GetComponent<Button>();
                TextMeshProUGUI label = button != null ? HeliosWidgetFactory.GetButtonLabel(button) : null;
                if (label != null)
                    label.text = $"{options[i].DisplayName}: {options[i].GetDisplayValue()}";
                bindingIndex++;
            }
        }

        private int AddPinnedValues(Transform parent)
        {
            int count = 0;
            IReadOnlyList<IHeliosValueOption> options = Context.Service.Options.Options;
            for (int i = 0; i < options.Count; i++)
            {
                IHeliosValueOption option = options[i];
                if (!option.Pin)
                    continue;

                Button button = Widgets.CreateButton(
                    $"PinnedValue_{option.DisplayName}",
                    parent,
                    $"{option.DisplayName}: {option.GetDisplayValue()}",
                    () => ActivateOption(option));
                Widgets.AddLayout(button.gameObject, 40f);
                button.gameObject.GetComponent<LayoutElement>().preferredWidth = 190f;
                count++;
            }
            return count;
        }

        private int AddPinnedActions(Transform parent)
        {
            int count = 0;
            IReadOnlyList<IHeliosActionOption> actions = Context.Service.Options.Actions;
            for (int i = 0; i < actions.Count; i++)
                count += AddAction(parent, actions[i]);

            IReadOnlyList<HeliosActionDefinition> registered = Context.Service.Actions;
            for (int i = 0; i < registered.Count; i++)
                count += AddAction(parent, registered[i]);
            return count;
        }

        private int AddAction(Transform parent, IHeliosActionOption action)
        {
            if (!action.Pin || action.Parameters.Count > 0)
                return 0;

            Button button = Widgets.CreateButton($"PinnedAction_{action.DisplayName}", parent, action.DisplayName, () =>
            {
                Context.Service.Access.Request(
                    new HeliosAccessRequest(HeliosAccessOperation.ExecutePinnedItem, action.DisplayName),
                    () => action.Invoke());
            });
            Widgets.AddLayout(button.gameObject, 40f);
            button.gameObject.GetComponent<LayoutElement>().preferredWidth = 160f;
            return 1;
        }

        private void ActivateOption(IHeliosValueOption option)
        {
            if (option.IsReadOnly)
                return;

            Context.Service.Access.Request(
                new HeliosAccessRequest(HeliosAccessOperation.ExecutePinnedItem, option.DisplayName),
                () => ActivateOptionAllowed(option));
        }

        private static void ActivateOptionAllowed(IHeliosValueOption option)
        {
            if (option.ValueKind == HeliosOptionValueKind.Boolean)
                option.ToggleBoolean();
            else if (option.ValueKind == HeliosOptionValueKind.Enum)
                option.CycleEnum();
            else
                option.Adjust(1f);
        }

        private void Rebuild()
        {
            Context?.Root?.RebuildOverlays();
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosDockedConsoleOverlay : HeliosOverlayBase
    {
        private readonly HeliosLogFilter _filter = new HeliosLogFilter();
        private readonly System.Text.StringBuilder _builder = new System.Text.StringBuilder(256);
        private HeliosLogQuery _query;
        private TextMeshProUGUI _text;
        private int _lastRevision = -1;

        public override string Id => "helios.docked-console";
        public override int Order => 10;

        public override void Initialize(HeliosOverlayContext context)
        {
            base.Initialize(context);
            _query = new HeliosLogQuery(context.Service.Logs);
        }

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            Button panel = widgets.CreateButton("DockedConsole", parent, string.Empty, () =>
            {
                Context.Service.OpenTab(typeof(HeliosConsoleTab));
                Context.Root.RebuildActiveTab();
            }, HeliosButtonStyle.Ghost(widgets.Theme));
            widgets.AddBorder(panel.gameObject, widgets.Theme.Border);
            RectTransform rect = panel.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(
                rect,
                new Vector2(0f, 0f),
                new Vector2(0.42f, 0f),
                new Vector2(12f, 12f),
                new Vector2(0f, 132f));
            _text = HeliosWidgetFactory.GetButtonLabel(panel);
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.fontSize = 12;
            _text.color = widgets.Theme.MutedText;
            HeliosWidgetFactory.Stretch(_text.rectTransform, 12f, 10f, 12f, 10f);
        }

        public override void Refresh()
        {
            int revision = Context.Service.Logs.Revision;
            if (_text == null || revision == _lastRevision)
                return;

            IReadOnlyList<HeliosLogViewEntry> entries = _query.Execute(_filter);
            int start = Mathf.Max(0, entries.Count - 4);
            _builder.Length = 0;
            for (int i = start; i < entries.Count; i++)
            {
                HeliosLogEntry entry = entries[i].Representative;
                _builder.Append('[').Append(entry.Level).Append("] ")
                    .AppendLine(HeliosWidgetFactory.Truncate(entry.Message, 100));
            }
            _text.text = _builder.ToString();
            _lastRevision = revision;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosDockedProfilerOverlay : HeliosOverlayBase
    {
        private TextMeshProUGUI _text;
        private HeliosProfilerSample _lastSample;
        private float _nextRefreshTime;

        public override string Id => "helios.docked-profiler";
        public override int Order => 20;

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            Button panel = widgets.CreateButton("DockedProfiler", parent, string.Empty, () =>
            {
                Context.Service.OpenTab(typeof(HeliosProfilerTab));
                Context.Root.RebuildActiveTab();
            }, HeliosButtonStyle.Ghost(widgets.Theme));
            widgets.AddBorder(panel.gameObject, widgets.Theme.Border);
            RectTransform rect = panel.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(
                rect,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-252f, -104f),
                new Vector2(-12f, -12f));
            _text = HeliosWidgetFactory.GetButtonLabel(panel);
            _text.alignment = TextAlignmentOptions.Left;
            _text.color = widgets.Theme.Text;
            HeliosWidgetFactory.Stretch(_text.rectTransform, 12f, 8f, 12f, 8f);
        }

        public override void Refresh()
        {
            if (_text == null || Time.unscaledTime < _nextRefreshTime)
                return;

            HeliosProfilerSample sample = Context.Service.Profiler.Latest;
            if (sample == null || ReferenceEquals(sample, _lastSample))
                return;

            _lastSample = sample;
            _nextRefreshTime = Time.unscaledTime + Context.Service.Settings.ProfilerRefreshInterval;
            _text.text =
                $"FPS {sample.Fps:F1}\nFrame {sample.FrameMs:F2} ms\n" +
                $"Memory {HeliosWidgetFactory.FormatBytes(sample.UsedMemory)}\n" +
                $"GC {HeliosWidgetFactory.FormatBytes(sample.GcAllocated)}";
        }
    }
}
