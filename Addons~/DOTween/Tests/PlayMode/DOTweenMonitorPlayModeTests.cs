using System.Collections;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DOTweenEngine = DG.Tweening.DOTween;

namespace HeliosDebugger.DOTween.Tests
{
    public sealed class DOTweenMonitorPlayModeTests
    {
        private GameObject _target;

        [SetUp]
        public void SetUp()
        {
            DOTweenEngine.Init(false, true, LogBehaviour.ErrorsOnly);
            DOTweenEngine.KillAll();
            _target = new GameObject("HeliosDOTweenTestTarget");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            DOTweenEngine.KillAll();
            Helios.Shutdown();
            Helios.UnregisterTabProvider<HeliosDOTweenTabProvider>();
            if (_target != null)
                Object.Destroy(_target);
            HeliosDebuggerRoot root = Object.FindAnyObjectByType<HeliosDebuggerRoot>();
            if (root != null)
                Object.Destroy(root.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CapturesAndControlsRealTween()
        {
            Tween tween = _target.transform
                .DOMoveX(10f, 10f)
                .SetId("helios.playmode")
                .SetTarget(_target)
                .SetAutoKill(false);
            yield return null;

            DOTweenMonitor monitor = new DOTweenMonitor();
            DOTweenMonitorSnapshot snapshot = monitor.Capture();
            DOTweenTweenSnapshot captured = Find(snapshot, "helios.playmode");

            Assert.IsNotNull(captured);
            Assert.AreEqual("HeliosDOTweenTestTarget (GameObject)", captured.Target);
            Assert.IsTrue(monitor.Pause(captured.Handle));
            Assert.IsFalse(tween.IsPlaying());
            DOTweenTweenSnapshot paused = Find(monitor.Capture(), "helios.playmode");
            Assert.IsNotNull(paused);
            Assert.AreEqual(captured.Handle, paused.Handle);
            Assert.IsTrue(paused.IsPaused);
            Assert.IsTrue(monitor.Play(paused.Handle));
            Assert.IsTrue(tween.IsPlaying());
            Assert.IsTrue(monitor.Complete(paused.Handle));
            Assert.IsTrue(tween.IsComplete());
            Assert.IsTrue(monitor.Kill(paused.Handle));
            Assert.IsFalse(tween.IsActive());
            Assert.IsFalse(monitor.Pause(paused.Handle));
        }

        [UnityTest]
        public IEnumerator StaleHandleCannotControlRecycledTweenGeneration()
        {
            Tween first = _target.transform
                .DOMoveY(5f, 10f)
                .SetId("recycled.same-metadata")
                .SetTarget(_target)
                .SetRecyclable(true);
            yield return null;

            DOTweenMonitor monitor = new DOTweenMonitor();
            DOTweenTweenSnapshot captured = Find(monitor.Capture(), "recycled.same-metadata");
            Assert.IsNotNull(captured);

            first.Kill();
            Tween second = _target.transform
                .DOMoveY(5f, 10f)
                .SetId("recycled.same-metadata")
                .SetTarget(_target)
                .SetRecyclable(true);

            Assert.IsTrue(object.ReferenceEquals(first, second), "DOTween did not reuse the pooled Tween instance.");
            Assert.IsFalse(monitor.Pause(captured.Handle));
            Assert.IsTrue(second.IsPlaying());
        }

        [UnityTest]
        public IEnumerator ProviderAddsTabBeforeAndAfterRootCreation()
        {
            Helios.Shutdown();
            Helios.UnregisterTabProvider<HeliosDOTweenTabProvider>();
            Assert.IsTrue(Helios.RegisterTabProvider(new HeliosDOTweenTabProvider()));
            Assert.IsFalse(Helios.IsInitialized);

            GameObject rootObject = new GameObject("HeliosDOTweenRoot");
            HeliosDebuggerRoot root = rootObject.AddComponent<HeliosDebuggerRoot>();
            yield return null;

            Assert.IsNotNull(FindTab(root.Service));

            Assert.IsTrue(Helios.UnregisterTabProvider<HeliosDOTweenTabProvider>());
            Assert.IsNull(FindTab(root.Service));
            Assert.IsTrue(Helios.RegisterTabProvider(new HeliosDOTweenTabProvider()));
            Assert.IsNotNull(FindTab(root.Service));
        }

        private static DOTweenTweenSnapshot Find(DOTweenMonitorSnapshot snapshot, string id)
        {
            for (int i = 0; i < snapshot.Tweens.Count; i++)
            {
                if (snapshot.Tweens[i].Id == id)
                    return snapshot.Tweens[i];
            }

            return null;
        }

        private static HeliosDOTweenTab FindTab(HeliosService service)
        {
            for (int i = 0; i < service.Tabs.Count; i++)
            {
                HeliosDOTweenTab tab = service.Tabs[i] as HeliosDOTweenTab;
                if (tab != null)
                    return tab;
            }

            return null;
        }
    }
}
