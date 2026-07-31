using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeliosDebugger.Tests
{
    public sealed class HeliosTabProviderPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Helios.UnregisterTabProvider<PlayModeTabProvider>();
            Helios.Shutdown();
            HeliosDebuggerRoot existing = Object.FindAnyObjectByType<HeliosDebuggerRoot>();
            if (existing != null)
                Object.Destroy(existing.gameObject);
            yield return null;
            PlayModeTab.DisposeCount = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Helios.UnregisterTabProvider<PlayModeTabProvider>();
            Helios.Shutdown();
            HeliosDebuggerRoot root = Object.FindAnyObjectByType<HeliosDebuggerRoot>();
            if (root != null)
                Object.Destroy(root.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RegistrationRebuildsButtonsAndSurvivesRebootstrap()
        {
            GameObject firstRootObject = new GameObject("HeliosProviderRoot");
            HeliosDebuggerRoot firstRoot = firstRootObject.AddComponent<HeliosDebuggerRoot>();
            Assert.IsTrue(Helios.RegisterTabProvider(new PlayModeTabProvider()));
            yield return null;

            Assert.IsNotNull(FindTab(firstRoot.Service));
            Assert.IsNotNull(firstRoot.transform.Find("HeliosCanvas/Panel/Tabs/Tab_Provider"));

            Object.Destroy(firstRootObject);
            yield return null;
            Assert.AreEqual(1, PlayModeTab.DisposeCount);

            GameObject secondRootObject = new GameObject("HeliosProviderRootRecreated");
            HeliosDebuggerRoot secondRoot = secondRootObject.AddComponent<HeliosDebuggerRoot>();
            yield return null;

            Assert.IsNotNull(FindTab(secondRoot.Service));
            Assert.IsNotNull(secondRoot.transform.Find("HeliosCanvas/Panel/Tabs/Tab_Provider"));
        }

        private static PlayModeTab FindTab(HeliosService service)
        {
            for (int i = 0; i < service.Tabs.Count; i++)
            {
                PlayModeTab tab = service.Tabs[i] as PlayModeTab;
                if (tab != null)
                    return tab;
            }

            return null;
        }

        private sealed class PlayModeTabProvider : IHeliosTabProvider
        {
            public IHeliosTab CreateTab()
            {
                return new PlayModeTab();
            }
        }

        private sealed class PlayModeTab : HeliosTabBase
        {
            public static int DisposeCount { get; set; }
            public override string Title => "Provider";
            public override int Order => 1000;

            public override void Dispose()
            {
                DisposeCount++;
            }

            protected override void BuildContent(HeliosWidgetFactory widgets, Transform parent)
            {
            }
        }
    }
}
