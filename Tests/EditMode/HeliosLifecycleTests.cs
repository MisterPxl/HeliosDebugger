using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace Astra.Helios.Tests
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Tests", "HeliosDebugger.EditMode.Tests")]
    public sealed class HeliosLifecycleTests
    {
        [SetUp] public void SetUp() => ResetSession();
        [TearDown] public void TearDown() => ResetSession();

        [Test]
        public void PassiveSubscriptionAndLazyInitializationNotifyOncePerGeneration()
        {
            var initialized = new List<HeliosService>();
            var stopped = new List<HeliosService>();
            Helios.Initialized += initialized.Add;
            Helios.ShuttingDown += service =>
            {
                Assert.That(Helios.TryGetService(out _), Is.False);
                Assert.That(service.IsDisposed, Is.False, "Detach runs before disposal.");
                stopped.Add(service);
            };
            Assert.That(Helios.TryGetService(out _), Is.False);
            var first = Helios.Service;
            Helios.Initialize();
            Assert.That(initialized.Count, Is.EqualTo(1));
            Helios.Shutdown();
            Helios.Shutdown();
            Assert.That(stopped, Is.EqualTo(new[] { first }));
            Assert.That(first.IsDisposed, Is.True);
            Helios.Initialize();
            Assert.That(initialized.Count, Is.EqualTo(2));
            Assert.That(Helios.Service, Is.Not.SameAs(first));
            Assert.That(Helios.Shutdown(first), Is.False);
            Assert.That(Helios.IsInitialized, Is.True);
        }

        [Test]
        public void LifecycleReentryIsRejectedWithoutLosingTheActiveGeneration()
        {
            Helios.Initialized += service => Assert.Throws<InvalidOperationException>(() => Helios.Shutdown());
            Helios.ShuttingDown += service => Assert.Throws<InvalidOperationException>(() => Helios.Initialize());
            Helios.Initialize();
            Assert.That(Helios.IsInitialized, Is.True);
            Helios.Shutdown();
            Assert.That(Helios.IsInitialized, Is.False);
        }

        [Test]
        public void ActionRemovalPreservesAnotherOwnersReplacement()
        {
            var service = Helios.Service;
            var original = new HeliosActionDefinition("id", "old", "", "", 0, () => { });
            var replacement = new HeliosActionDefinition("ID", "new", "", "", 0, () => { });
            service.RegisterAction(original);
            service.RegisterAction(replacement);
            Assert.That(service.UnregisterAction(original), Is.False);
            Assert.That(service.Actions, Does.Contain(replacement));
            Assert.That(service.UnregisterAction(replacement), Is.True);
            Assert.That(service.Actions, Is.Empty);
        }

        [Test]
        public void SessionResetRemovesObserversFromThePreviousPlaySession()
        {
            int count = 0;
            Helios.Initialized += service => count++;
            Helios.Initialize();
            ResetSession();
            Helios.Initialize();
            Assert.That(count, Is.EqualTo(1));
        }

        private static void ResetSession() => typeof(Helios).GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    }
}
