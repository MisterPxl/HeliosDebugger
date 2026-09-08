using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Astra.Helios.Tests
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Tests", "HeliosDebugger.PlayMode.Tests")]
    public sealed class HeliosLogQueryTests
    {
        [UnityTest]
        public IEnumerator FiltersAndCollapsesAcrossFullBuffer()
        {
            HeliosLogStore store = new HeliosLogStore(256);
            try
            {
                for (int i = 0; i < 2; i++)
                    Debug.Log("helios-query-duplicate");
                Debug.LogWarning("helios-query-warning");
                yield return null;
                store.FlushPending();

                HeliosLogQuery query = new HeliosLogQuery(store);
                HeliosLogFilter filter = new HeliosLogFilter
                {
                    Search = "helios-query-duplicate",
                    Levels = HeliosLogLevelMask.Log,
                    CollapseDuplicates = true
                };

                System.Collections.Generic.IReadOnlyList<HeliosLogViewEntry> result = query.Execute(filter);
                Assert.AreEqual(1, result.Count);
                Assert.AreEqual(2, result[0].Count);
            }
            finally
            {
                store.Dispose();
            }
        }
    }
}
