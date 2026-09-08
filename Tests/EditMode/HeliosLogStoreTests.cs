using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Astra.Helios.Tests
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Tests", "HeliosDebugger.EditMode.Tests")]
    public sealed class HeliosLogStoreTests
    {
        [TestCase(32, 32)]
        [TestCase(128, 32)]
        public void PendingBufferIsBoundedAndKeepsNewestMessages(int initialCapacity, int finalCapacity)
        {
            using (var logs = new HeliosLogStore(initialCapacity))
            {
                MethodInfo receive = typeof(HeliosLogStore).GetMethod("OnLogReceived", BindingFlags.NonPublic | BindingFlags.Instance);
                for (int i = 0; i < 10000; i++)
                    receive.Invoke(logs, new object[] { i.ToString(), "", LogType.Log });

                logs.SetCapacity(finalCapacity);
                var pending = (ICollection)typeof(HeliosLogStore)
                    .GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(logs);
                Assert.AreEqual(finalCapacity, pending.Count);
                var entries = logs.Snapshot();
                Assert.AreEqual(finalCapacity, entries.Count);
                Assert.AreEqual((10000 - finalCapacity).ToString(), entries[0].Message);
                Assert.AreEqual("9999", entries[entries.Count - 1].Message);
            }
        }
    }
}
