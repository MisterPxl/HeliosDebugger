using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.Helios.Integrations.DOTween
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public sealed class HeliosDOTweenTabProvider : IHeliosTabProvider
    {
        public IHeliosTab CreateTab()
        {
            return new HeliosDOTweenTab();
        }
    }

    internal static class HeliosDOTweenBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterProvider()
        {
            Helios.RegisterTabProvider(new HeliosDOTweenTabProvider());
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    [HeliosTypeIdentity("HeliosDebugger.DOTween.HeliosDOTweenTab")]
    public sealed class HeliosDOTweenTab : HeliosTabBase, IHeliosTabIcon
    {
        // Documented behaviour: snapshots refresh at most four times per second.
        private const float RefreshInterval = 0.25f;
        private const float KillConfirmationDuration = 3f;

        private readonly IDOTweenMonitor _monitor;
        private readonly Func<float> _timeProvider;
        private readonly DOTweenRefreshThrottle _throttle;
        private readonly DOTweenKillAllConfirmation _killConfirmation;
        private readonly List<RowBinding> _rows = new List<RowBinding>();
        private readonly List<DOTweenTweenSnapshot> _visibleTweens = new List<DOTweenTweenSnapshot>();

        private DOTweenMonitorSnapshot _snapshot;
        private ScrollRect _scroll;
        private RectTransform _listContent;
        private TextMeshProUGUI _activeValue;
        private TextMeshProUGUI _playingValue;
        private TextMeshProUGUI _pausedValue;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _killAllLabel;
        private string _search = string.Empty;
        private int _filterRevision;
        private int _renderedFilterRevision = -1;
        private bool _killButtonArmed;
        private bool _hasPendingScrollAnchor;
        private long _pendingScrollAnchorHandle;
        private float _pendingScrollAnchorViewportY;

        public HeliosDOTweenTab()
            : this(new DOTweenMonitor(), () => Time.unscaledTime)
        {
        }

        public HeliosDOTweenTab(IDOTweenMonitor monitor, Func<float> timeProvider)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _throttle = new DOTweenRefreshThrottle(RefreshInterval);
            _killConfirmation = new DOTweenKillAllConfirmation(KillConfirmationDuration);
            _snapshot = new DOTweenMonitorSnapshot(
                false,
                0,
                0,
                0,
                Array.Empty<DOTweenTweenSnapshot>());
        }

        public override string Title => "Tweens";
        public override int Order => 15;
        public Sprite Icon => HeliosIcons.Get(HeliosIcons.Tweens);

        protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
        {
            _rows.Clear();
            _visibleTweens.Clear();
            _renderedFilterRevision = -1;
            _killConfirmation.Reset();
            _killButtonArmed = false;
            _hasPendingScrollAnchor = false;

            GameObject metrics = widgets.CreatePanel("TweenMetrics", parent, Color.clear);
            HorizontalLayoutGroup metricLayout = metrics.AddComponent<HorizontalLayoutGroup>();
            metricLayout.spacing = 8f;
            metricLayout.childControlWidth = true;
            metricLayout.childForceExpandWidth = true;
            widgets.AddLayout(metrics, 78f);
            SetFlexibleWidth(widgets.CreateMetricCard("ActiveCard", metrics.transform, "Active", out _activeValue));
            SetFlexibleWidth(widgets.CreateMetricCard("PlayingCard", metrics.transform, "Playing", out _playingValue));
            SetFlexibleWidth(widgets.CreateMetricCard("PausedCard", metrics.transform, "Paused", out _pausedValue));

            GameObject controls = widgets.CreatePanel("TweenControls", parent, Color.clear);
            HorizontalLayoutGroup controlLayout = controls.AddComponent<HorizontalLayoutGroup>();
            controlLayout.spacing = 6f;
            controlLayout.childControlWidth = true;
            controlLayout.childForceExpandWidth = false;
            widgets.AddLayout(controls, 40f);

            TMP_InputField search = widgets.CreateInput("Search", controls.transform, "Search ID, target or type", OnSearchChanged);
            search.SetTextWithoutNotify(_search);
            SetFlexibleWidth(search.gameObject);
            Button pauseAll = widgets.CreateButton("PauseAll", controls.transform, "Pause All", PauseAll);
            SetFixedWidth(pauseAll.gameObject, 110f);
            Button playAll = widgets.CreateButton("PlayAll", controls.transform, "Play All", PlayAll);
            SetFixedWidth(playAll.gameObject, 110f);
            Button killAll = widgets.CreateButton("KillAll", controls.transform, "Kill All", RequestKillAll);
            SetFixedWidth(killAll.gameObject, 150f);
            _killAllLabel = HeliosWidgetFactory.GetButtonLabel(killAll);

            _status = widgets.CreateText("TweenStatus", parent, "DOTween status unavailable.", 13);
            _status.color = widgets.Theme.MutedText;
            widgets.AddLayout(_status.gameObject, 34f);

            _scroll = widgets.CreateScrollView("TweenList", parent, out _listContent);
            widgets.AddLayout(_scroll.gameObject, 640f, 260f);
            _throttle.Reset();
        }

        public override void Refresh()
        {
            RestorePendingScrollAnchor();
            float now = _timeProvider();
            RefreshKillConfirmation(now);
            if (!_throttle.ShouldCapture(now))
                return;

            _snapshot = _monitor.Capture();
            UpdateSummary();
            ReconcileRows();
        }

        public override void Dispose()
        {
            _rows.Clear();
            _visibleTweens.Clear();
            _scroll = null;
            _listContent = null;
            _activeValue = null;
            _playingValue = null;
            _pausedValue = null;
            _status = null;
            _killAllLabel = null;
            _hasPendingScrollAnchor = false;
        }

        private void OnSearchChanged(string value)
        {
            _search = value ?? string.Empty;
            _filterRevision++;
            ReconcileRows();
            UpdateStatus();
        }

        private void PauseAll()
        {
            _monitor.PauseAll();
            ForceRefresh();
        }

        private void PlayAll()
        {
            _monitor.PlayAll();
            ForceRefresh();
        }

        private void RequestKillAll()
        {
            float now = _timeProvider();
            if (_killConfirmation.Request(now))
            {
                _monitor.KillAll();
                ForceRefresh();
            }

            RefreshKillConfirmation(now);
        }

        private void ForceRefresh()
        {
            _throttle.Reset();
            Refresh();
        }

        private void RefreshKillConfirmation(float now)
        {
            bool armed = _killConfirmation.IsArmed(now);
            if (_killAllLabel == null || armed == _killButtonArmed)
                return;

            _killButtonArmed = armed;
            _killAllLabel.text = armed ? "Confirm Kill All" : "Kill All";
        }

        private void UpdateSummary()
        {
            if (_activeValue != null)
                _activeValue.text = _snapshot.ActiveCount.ToString(CultureInfo.InvariantCulture);
            if (_playingValue != null)
                _playingValue.text = _snapshot.PlayingCount.ToString(CultureInfo.InvariantCulture);
            if (_pausedValue != null)
                _pausedValue.text = _snapshot.PausedCount.ToString(CultureInfo.InvariantCulture);
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (_status == null)
                return;

            if (!_snapshot.IsInitialized)
            {
                _status.text = "DOTween is not initialized.";
                return;
            }

            if (_snapshot.ActiveCount == 0)
            {
                _status.text = "No active tween. Auto-killed completed tweens are not retained by DOTween.";
                return;
            }

            _status.text = $"{_visibleTweens.Count} shown / {_snapshot.ActiveCount} active";
        }

        private void ReconcileRows()
        {
            if (_listContent == null)
                return;

            _visibleTweens.Clear();
            IReadOnlyList<DOTweenTweenSnapshot> tweens = _snapshot.Tweens;
            for (int i = 0; i < tweens.Count; i++)
            {
                DOTweenTweenSnapshot tween = tweens[i];
                if (DOTweenTweenFilter.Matches(tween, _search))
                    _visibleTweens.Add(tween);
            }

            bool rebuild = _renderedFilterRevision != _filterRevision || _rows.Count != _visibleTweens.Count;
            if (!rebuild)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (_rows[i].Snapshot.Handle != _visibleTweens[i].Handle ||
                        GetSectionKey(_rows[i].Snapshot) != GetSectionKey(_visibleTweens[i]))
                    {
                        rebuild = true;
                        break;
                    }
                }
            }

            if (rebuild)
                RebuildRows();
            else
                RefreshRows();

            _renderedFilterRevision = _filterRevision;
            UpdateStatus();
        }

        private void RebuildRows()
        {
            Widgets.Clear(_listContent);
            _rows.Clear();

            int playingCount = CountSection(0);
            int pausedCount = CountSection(1);
            int otherCount = CountSection(2);
            int currentSection = -1;
            for (int i = 0; i < _visibleTweens.Count; i++)
            {
                int section = GetSectionKey(_visibleTweens[i]);
                if (section != currentSection)
                {
                    currentSection = section;
                    CreateSectionHeader(section, GetSectionCount(section, playingCount, pausedCount, otherCount));
                }

                _rows.Add(CreateRow(_visibleTweens[i]));
            }
        }

        private void RefreshRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                RowBinding row = _rows[i];
                row.Snapshot = _visibleTweens[i];
                UpdateRow(row);
            }
        }

        private RowBinding CreateRow(DOTweenTweenSnapshot snapshot)
        {
            GameObject card = Widgets.CreateSurface($"Tween_{snapshot.Handle}", _listContent, Widgets.Theme.Row, Widgets.Theme.CardCornerRadius);
            Widgets.AddBorder(card, Widgets.Theme.Border);
            VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(8, 8, 5, 5);
            cardLayout.spacing = 4f;
            cardLayout.childControlWidth = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;
            Widgets.AddLayout(card, 88f);

            GameObject header = Widgets.CreatePanel("Header", card.transform, Color.clear);
            HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 6f;
            headerLayout.childControlWidth = true;
            headerLayout.childForceExpandWidth = false;
            Widgets.AddLayout(header, 36f);

            TextMeshProUGUI title = Widgets.CreateText("Title", header.transform, string.Empty, 13);
            SetFlexibleWidth(title.gameObject);
            Button playPause = Widgets.CreateButton("PlayPause", header.transform, "Pause", null);
            SetFixedWidth(playPause.gameObject, 80f);
            TextMeshProUGUI playPauseLabel = HeliosWidgetFactory.GetButtonLabel(playPause);
            Button complete = Widgets.CreateButton("Complete", header.transform, "Complete", null);
            SetFixedWidth(complete.gameObject, 96f);
            Button kill = Widgets.CreateButton("Kill", header.transform, "Kill", null);
            SetFixedWidth(kill.gameObject, 64f);

            GameObject progress = Widgets.CreatePanel("Progress", card.transform, Widgets.Theme.Navigation);
            GameObject fillObject = Widgets.CreatePanel("Fill", progress.transform, Widgets.Theme.Accent);
            Image fill = fillObject.GetComponent<Image>();
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            TextMeshProUGUI detail = Widgets.CreateText("Detail", progress.transform, string.Empty, 11, TextAnchor.MiddleCenter);
            HeliosWidgetFactory.Stretch(detail.rectTransform);
            Widgets.AddLayout(progress, 28f);

            RowBinding row = new RowBinding(
                snapshot,
                card.GetComponent<RectTransform>(),
                title,
                detail,
                fill,
                playPauseLabel);
            playPause.onClick.AddListener(() =>
            {
                CaptureScrollAnchor(row);
                if (row.Snapshot.IsPlaying)
                    _monitor.Pause(row.Snapshot.Handle);
                else
                    _monitor.Play(row.Snapshot.Handle);
                ForceRefresh();
            });
            complete.onClick.AddListener(() =>
            {
                _monitor.Complete(row.Snapshot.Handle);
                ForceRefresh();
            });
            kill.onClick.AddListener(() =>
            {
                _monitor.Kill(row.Snapshot.Handle);
                ForceRefresh();
            });
            UpdateRow(row);
            return row;
        }

        private void CreateSectionHeader(int section, int count)
        {
            string title;
            Color color;
            switch (section)
            {
                case 0:
                    title = "Playing";
                    color = Widgets.Theme.Accent;
                    break;
                case 1:
                    title = "Paused";
                    color = Widgets.Theme.Warning;
                    break;
                default:
                    title = "Other";
                    color = Widgets.Theme.MutedText;
                    break;
            }

            TextMeshProUGUI header = Widgets.CreateText(
                $"Section_{title}",
                _listContent,
                $"{title} ({count})",
                15);
            header.color = color;
            Widgets.AddLayout(header.gameObject, 30f);
        }

        private int CountSection(int section)
        {
            int count = 0;
            for (int i = 0; i < _visibleTweens.Count; i++)
            {
                if (GetSectionKey(_visibleTweens[i]) == section)
                    count++;
            }

            return count;
        }

        private static int GetSectionCount(
            int section,
            int playingCount,
            int pausedCount,
            int otherCount)
        {
            if (section == 0)
                return playingCount;
            if (section == 1)
                return pausedCount;
            return otherCount;
        }

        private static int GetSectionKey(DOTweenTweenSnapshot tween)
        {
            if (tween.IsPlaying)
                return 0;
            if (tween.IsPaused)
                return 1;
            return 2;
        }

        private void CaptureScrollAnchor(RowBinding row)
        {
            if (_scroll == null || _scroll.viewport == null || row.Root == null)
                return;

            Canvas.ForceUpdateCanvases();
            Vector3 viewportPosition = _scroll.viewport.InverseTransformPoint(row.Root.position);
            _pendingScrollAnchorHandle = row.Snapshot.Handle;
            _pendingScrollAnchorViewportY = viewportPosition.y;
            _hasPendingScrollAnchor = true;
        }

        private void RestorePendingScrollAnchor()
        {
            if (!_hasPendingScrollAnchor ||
                _scroll == null ||
                _scroll.viewport == null ||
                _listContent == null)
            {
                return;
            }

            RowBinding anchoredRow = null;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Snapshot.Handle == _pendingScrollAnchorHandle)
                {
                    anchoredRow = _rows[i];
                    break;
                }
            }

            _hasPendingScrollAnchor = false;
            if (anchoredRow == null || anchoredRow.Root == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_listContent);
            Canvas.ForceUpdateCanvases();

            Vector3 currentViewportPosition =
                _scroll.viewport.InverseTransformPoint(anchoredRow.Root.position);
            Vector2 anchoredPosition = _listContent.anchoredPosition;
            anchoredPosition.y += _pendingScrollAnchorViewportY - currentViewportPosition.y;
            float maxY = Mathf.Max(0f, _listContent.rect.height - _scroll.viewport.rect.height);
            anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, 0f, maxY);
            _scroll.StopMovement();
            _listContent.anchoredPosition = anchoredPosition;
        }

        private static void UpdateRow(RowBinding row)
        {
            DOTweenTweenSnapshot tween = row.Snapshot;
            row.Title.text = $"{tween.Id}  |  {tween.Target}  |  {tween.TweenType}";
            row.Detail.text =
                $"{FormatTime(tween.Elapsed)} / {FormatTime(tween.Duration)}  " +
                $"{Mathf.Clamp01(tween.Progress):P0}  |  loops {tween.CompletedLoops}";
            row.Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(tween.Progress), 1f);
            row.Fill.rectTransform.offsetMax = Vector2.zero;
            if (row.PlayPauseLabel != null)
                row.PlayPauseLabel.text = tween.IsPlaying ? "Pause" : "Play";
        }

        private static string FormatTime(float seconds)
        {
            if (float.IsInfinity(seconds))
                return "∞";
            if (float.IsNaN(seconds) || seconds < 0f)
                return "--";
            return seconds.ToString("0.00s", CultureInfo.InvariantCulture);
        }

        private static void SetFlexibleWidth(GameObject gameObject)
        {
            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
                layout = gameObject.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1f;
        }

        private static void SetFixedWidth(GameObject gameObject, float width)
        {
            LayoutElement layout = gameObject.GetComponent<LayoutElement>();
            if (layout == null)
                layout = gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }

        private sealed class RowBinding
        {
            public RowBinding(
                DOTweenTweenSnapshot snapshot,
                RectTransform root,
                TextMeshProUGUI title,
                TextMeshProUGUI detail,
                Image fill,
                TextMeshProUGUI playPauseLabel)
            {
                Snapshot = snapshot;
                Root = root;
                Title = title;
                Detail = detail;
                Fill = fill;
                PlayPauseLabel = playPauseLabel;
            }

            public DOTweenTweenSnapshot Snapshot { get; set; }
            public RectTransform Root { get; }
            public TextMeshProUGUI Title { get; }
            public TextMeshProUGUI Detail { get; }
            public Image Fill { get; }
            public TextMeshProUGUI PlayPauseLabel { get; }
        }
    }
}
