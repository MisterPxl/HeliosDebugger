using NUnit.Framework;
using UnityEngine;

namespace Astra.Helios.Tests
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Tests", "HeliosDebugger.EditMode.Tests")]
    public sealed class HeliosTabProviderTests
    {
        [SetUp]
        public void SetUp()
        {
            Helios.Shutdown();
            Helios.UnregisterTabProvider<CountingTabProvider>();
            CountingTab.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Helios.Shutdown();
            Helios.UnregisterTabProvider<CountingTabProvider>();
            HeliosDebuggerRoot root = Object.FindAnyObjectByType<HeliosDebuggerRoot>();
            if (root != null)
                Object.DestroyImmediate(root.gameObject);
        }

        [Test]
        public void RegisterProviderDoesNotInitializeHelios()
        {
            Assert.IsTrue(Helios.RegisterTabProvider(new CountingTabProvider()));

            Assert.IsFalse(Helios.IsInitialized);
            Assert.AreEqual(0, CountingTab.CreatedCount);
        }

        [Test]
        public void ProvidersAreDeduplicatedByConcreteType()
        {
            Assert.IsTrue(Helios.RegisterTabProvider(new CountingTabProvider()));
            Assert.IsFalse(Helios.RegisterTabProvider(new CountingTabProvider()));

            Helios.Initialize();

            Assert.AreEqual(1, CountingTab.CreatedCount);
            Assert.AreEqual(1, CountTabs<CountingTab>(Helios.Service));
        }

        [Test]
        public void ProviderRegisteredAfterRootAddsTabImmediately()
        {
            Helios.Initialize();
            GameObject rootObject = new GameObject("HeliosTabProviderTestRoot");
            HeliosDebuggerRoot root = rootObject.AddComponent<HeliosDebuggerRoot>();
            Helios.Service.AttachRoot(root);

            Assert.IsTrue(Helios.RegisterTabProvider(new CountingTabProvider()));

            Assert.AreEqual(1, CountingTab.CreatedCount);
            Assert.AreEqual(1, CountingTab.InitializeCount);
            Assert.AreEqual(1, CountTabs<CountingTab>(Helios.Service));
        }

        [Test]
        public void UnregisterDisposesProviderTabExactlyOnce()
        {
            Helios.RegisterTabProvider(new CountingTabProvider());
            Helios.Initialize();

            Assert.IsTrue(Helios.UnregisterTabProvider<CountingTabProvider>());
            Helios.Shutdown();

            Assert.AreEqual(1, CountingTab.DisposeCount);
        }

        [Test]
        public void UnregisterTabDisposesDirectTabExactlyOnce()
        {
            Assert.IsTrue(Helios.RegisterTab(new CountingTab()));

            Assert.IsTrue(Helios.UnregisterTab<CountingTab>());
            Helios.Shutdown();

            Assert.AreEqual(1, CountingTab.DisposeCount);
        }

        [Test]
        public void ShutdownDisposesTabsAndKeepsProvidersForReinitialize()
        {
            Helios.RegisterTabProvider(new CountingTabProvider());
            Helios.Initialize();

            Helios.Shutdown();
            Helios.Initialize();

            Assert.AreEqual(2, CountingTab.CreatedCount);
            Assert.AreEqual(1, CountingTab.DisposeCount);
            Assert.AreEqual(1, CountTabs<CountingTab>(Helios.Service));
        }

        private static int CountTabs<TTab>(HeliosService service) where TTab : IHeliosTab
        {
            int count = 0;
            for (int i = 0; i < service.Tabs.Count; i++)
            {
                if (service.Tabs[i] is TTab)
                    count++;
            }

            return count;
        }

        private sealed class CountingTabProvider : IHeliosTabProvider
        {
            public IHeliosTab CreateTab()
            {
                return new CountingTab();
            }
        }

        private sealed class CountingTab : HeliosTabBase
        {
            public CountingTab()
            {
                CreatedCount++;
            }

            public static int CreatedCount { get; private set; }
            public static int InitializeCount { get; private set; }
            public static int DisposeCount { get; private set; }
            public override string Title => "Counting";
            public override int Order => 1000;

            public static void Reset()
            {
                CreatedCount = 0;
                InitializeCount = 0;
                DisposeCount = 0;
            }

            public override void Initialize(HeliosContext context)
            {
                base.Initialize(context);
                InitializeCount++;
            }

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
