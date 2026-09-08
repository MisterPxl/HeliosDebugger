using System;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.Helios.Integrations.DOTween.Tests
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween.Tests", "HeliosDebugger.DOTween.EditMode.Tests")]
    public sealed class HeliosDOTweenTabTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void FilterMatchesIdTargetAndTypeIgnoringCase()
        {
            DOTweenTweenSnapshot tween = CreateTween(1L, true);

            Assert.IsTrue(DOTweenTweenFilter.Matches(tween, "PLAYER"));
            Assert.IsTrue(DOTweenTweenFilter.Matches(tween, "move"));
            Assert.IsTrue(DOTweenTweenFilter.Matches(tween, "tweener"));
            Assert.IsFalse(DOTweenTweenFilter.Matches(tween, "missing"));
        }

        [Test]
        public void ListSeparatesPlayingAndPausedTweens()
        {
            DOTweenMonitorSnapshot snapshot = new DOTweenMonitorSnapshot(
                true,
                2,
                1,
                1,
                new List<DOTweenTweenSnapshot>
                {
                    CreateTween(1L, true),
                    CreateTween(2L, false)
                });

            BuildTab(new FakeMonitor(snapshot), () => 0f);

            Assert.IsNotNull(_root.transform.Find("TweenList/Content/Section_Playing"));
            Assert.IsNotNull(_root.transform.Find("TweenList/Content/Section_Paused"));
            Assert.IsNotNull(_root.transform.Find("TweenList/Content/Tween_1"));
            Assert.IsNotNull(_root.transform.Find("TweenList/Content/Tween_2"));
        }

        [Test]
        public void RefreshIsThrottledToQuarterSecond()
        {
            FakeMonitor monitor = new FakeMonitor();
            float now = 0f;
            HeliosDOTweenTab tab = BuildTab(monitor, () => now);
            Assert.AreEqual(1, monitor.CaptureCount);

            now = 0.1f;
            tab.Refresh();
            Assert.AreEqual(1, monitor.CaptureCount);

            now = 0.25f;
            tab.Refresh();
            Assert.AreEqual(2, monitor.CaptureCount);
        }

        [Test]
        public void RowAndGlobalCommandsUseMonitor()
        {
            FakeMonitor monitor = new FakeMonitor();
            HeliosDOTweenTab tab = BuildTab(monitor, () => 0f);

            FindButton("TweenList/Content/Tween_1/Header/PlayPause").onClick.Invoke();
            FindButton("TweenList/Content/Tween_1/Header/Complete").onClick.Invoke();
            FindButton("TweenList/Content/Tween_1/Header/Kill").onClick.Invoke();
            FindButton("TweenControls/PauseAll").onClick.Invoke();
            FindButton("TweenControls/PlayAll").onClick.Invoke();

            Assert.AreEqual(1L, monitor.LastPausedHandle);
            Assert.AreEqual(1L, monitor.LastCompletedHandle);
            Assert.AreEqual(1L, monitor.LastKilledHandle);
            Assert.AreEqual(1, monitor.PauseAllCount);
            Assert.AreEqual(1, monitor.PlayAllCount);
        }

        [Test]
        public void KillAllRequiresSecondClickBeforeExpiry()
        {
            FakeMonitor monitor = new FakeMonitor();
            float now = 10f;
            BuildTab(monitor, () => now);
            Button killAll = FindButton("TweenControls/KillAll");

            killAll.onClick.Invoke();
            Assert.AreEqual(0, monitor.KillAllCount);

            now = 12f;
            killAll.onClick.Invoke();
            Assert.AreEqual(1, monitor.KillAllCount);

            now = 20f;
            killAll.onClick.Invoke();
            now = 24f;
            killAll.onClick.Invoke();
            Assert.AreEqual(1, monitor.KillAllCount);
        }

        [Test]
        public void RebuildRestoresRowsAndResetsKillConfirmation()
        {
            FakeMonitor monitor = new FakeMonitor();
            HeliosDOTweenTab tab = BuildTab(monitor, () => 5f);
            FindButton("TweenControls/KillAll").onClick.Invoke();

            UnityEngine.Object.DestroyImmediate(_root);
            _root = new GameObject("DOTweenTabTestsRebuilt", typeof(RectTransform));
            tab.Build(new HeliosWidgetFactory(), _root.transform);

            Assert.IsNotNull(_root.transform.Find("TweenList/Content/Tween_1"));
            Button killAll = FindButton("TweenControls/KillAll");
            Assert.AreEqual("Kill All", killAll.GetComponentInChildren<TextMeshProUGUI>().text);
            killAll.onClick.Invoke();
            Assert.AreEqual(0, monitor.KillAllCount);
        }

        [Test]
        public void FormattingStateIsExposedBySnapshot()
        {
            DOTweenTweenSnapshot tween = CreateTween(3L, false);

            Assert.AreEqual("player.move", tween.Id);
            Assert.AreEqual("Player (GameObject)", tween.Target);
            Assert.AreEqual(1.25f, tween.Elapsed);
            Assert.AreEqual(2.5f, tween.Duration);
            Assert.AreEqual(0.5f, tween.Progress);
            Assert.AreEqual(2, tween.CompletedLoops);
            Assert.IsTrue(tween.IsPaused);
        }

        private HeliosDOTweenTab BuildTab(FakeMonitor monitor, Func<float> timeProvider)
        {
            _root = new GameObject("DOTweenTabTests", typeof(RectTransform));
            HeliosDOTweenTab tab = new HeliosDOTweenTab(monitor, timeProvider);
            HeliosWidgetFactory widgets = new HeliosWidgetFactory();
            tab.Build(widgets, _root.transform);
            return tab;
        }

        private Button FindButton(string path)
        {
            Transform transform = _root.transform.Find(path);
            Assert.IsNotNull(transform, path);
            return transform.GetComponent<Button>();
        }

        private static DOTweenTweenSnapshot CreateTween(long handle, bool playing)
        {
            return new DOTweenTweenSnapshot(
                handle,
                true,
                playing,
                !playing,
                "player.move",
                "Player (GameObject)",
                "TweenerCore",
                1.25f,
                2.5f,
                0.5f,
                2);
        }

        private sealed class FakeMonitor : IDOTweenMonitor
        {
            private readonly DOTweenMonitorSnapshot _snapshot;

            public FakeMonitor()
                : this(new DOTweenMonitorSnapshot(
                    true,
                    1,
                    1,
                    0,
                    new List<DOTweenTweenSnapshot> { CreateTween(1L, true) }))
            {
            }

            public FakeMonitor(DOTweenMonitorSnapshot snapshot)
            {
                _snapshot = snapshot;
            }

            public int CaptureCount { get; private set; }
            public long LastPausedHandle { get; private set; }
            public long LastCompletedHandle { get; private set; }
            public long LastKilledHandle { get; private set; }
            public int PauseAllCount { get; private set; }
            public int PlayAllCount { get; private set; }
            public int KillAllCount { get; private set; }

            public DOTweenMonitorSnapshot Capture()
            {
                CaptureCount++;
                return _snapshot;
            }

            public bool Pause(long handle)
            {
                LastPausedHandle = handle;
                return true;
            }

            public bool Play(long handle)
            {
                return true;
            }

            public bool Complete(long handle)
            {
                LastCompletedHandle = handle;
                return true;
            }

            public bool Kill(long handle)
            {
                LastKilledHandle = handle;
                return true;
            }

            public void PauseAll()
            {
                PauseAllCount++;
            }

            public void PlayAll()
            {
                PlayAllCount++;
            }

            public void KillAll()
            {
                KillAllCount++;
            }
        }
    }
}
