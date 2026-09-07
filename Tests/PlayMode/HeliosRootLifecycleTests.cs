using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HeliosDebugger.Tests
{
    public sealed class HeliosRootLifecycleTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var root in Object.FindObjectsByType<HeliosDebuggerRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(root.gameObject);
            yield return null;
            Helios.Shutdown();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Helios.Shutdown();
            foreach (var root in Object.FindObjectsByType<HeliosDebuggerRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.Destroy(root.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DelayedDestructionOfOldRootDoesNotShutDownTheNewGeneration()
        {
            var oldRoot = new GameObject("Old Helios").AddComponent<HeliosDebuggerRoot>();
            var oldService = oldRoot.Service;
            Helios.Shutdown();
            Assert.That(oldRoot.gameObject.activeSelf, Is.False);
            var newRoot = new GameObject("New Helios").AddComponent<HeliosDebuggerRoot>();
            var newService = newRoot.Service;
            yield return null;
            Assert.That(oldRoot == null, Is.True);
            Assert.That(oldService.IsDisposed, Is.True);
            Assert.That(Helios.Service, Is.SameAs(newService));
            Assert.That(newService.IsDisposed, Is.False);
            Assert.That(newRoot.gameObject.activeInHierarchy, Is.True);
        }
    }
}
