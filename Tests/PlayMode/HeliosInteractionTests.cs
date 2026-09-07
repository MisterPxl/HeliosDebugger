using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HeliosDebugger.Tests
{
    public sealed class HeliosInteractionTests
    {
        private HeliosDebuggerRoot _root;
        private TestScreenshotProvider _screenshots;
        private RecordingTransport _transport;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            foreach (var existing in UnityEngine.Object.FindObjectsByType<HeliosDebuggerRoot>(FindObjectsSortMode.None))
                UnityEngine.Object.Destroy(existing.gameObject);
            yield return null;
            Helios.Shutdown();
            _root = new GameObject("HeliosInteractionTests").AddComponent<HeliosDebuggerRoot>();
            _root.Service.Show();
            _screenshots = new TestScreenshotProvider();
            typeof(HeliosReportBuilder).GetField("_screenshotProvider", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_root.Service.Reporting.Builder, _screenshots);
            _transport = new RecordingTransport();
            _root.Service.Reporting.RegisterTransport(_transport);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);
            yield return null;
            Helios.Shutdown();
        }

        [UnityTest]
        public IEnumerator NumericEditingSurvivesRefreshAndCommitsOnEndEdit()
        {
            int value = 10;
            _root.Service.Options.AddOption(HeliosOptionDefinition.Create("TestNumber", () => value, x => value = x, "Tests"));
            _root.Service.OpenTab(typeof(HeliosOptionsTab));
            yield return null;
            TMP_InputField input = NumberControl<TMP_InputField>("Value");
            input.ActivateInputField();
            yield return null;
            Assert.IsTrue(input.isFocused);
            input.text = "123";
            _root.Service.ActiveTab.Refresh();
            Assert.AreEqual("123", input.text);
            Assert.AreEqual(10, value, "Editing must not commit until end edit.");
            input.DeactivateInputField();
            yield return null;
            Assert.AreEqual(123, value);
            value = 456;
            _root.Service.ActiveTab.Refresh();
            Assert.AreEqual("456", input.text, "Unfocused fields still follow runtime changes.");
        }

        [UnityTest]
        public IEnumerator LockClosesPanelAndRejectsStaleActionCallbacks()
        {
            int executed = 0;
            _root.Service.RegisterAction(new HeliosActionDefinition("tests.action", "TestAction", "Tests", "", 0, () => executed++));
            _root.Service.OpenTab(typeof(HeliosOptionsTab));
            yield return null;
            Button button = Button("Action_TestAction");
            var policy = new MutablePolicy();
            policy.TryUnlock("test");
            _root.Service.Access.SetPolicy(policy);
            _root.Service.Access.Lock();
            Assert.IsFalse(_root.Service.IsVisible);
            Assert.IsFalse(button.gameObject.activeInHierarchy);
            button.onClick.Invoke();
            Assert.AreEqual(0, executed);

            _root.Service.Show();
            Assert.IsTrue(_root.Service.TryUnlock("test"));
            yield return null;
            Button("Action_TestAction").onClick.Invoke();
            Assert.AreEqual(1, executed, "Actions remain usable after a new authorized session.");
        }

        [UnityTest]
        public IEnumerator ExpiredAccessRejectsValueEditsBeforeNextTick()
        {
            int value = 10;
            _root.Service.Options.AddOption(HeliosOptionDefinition.Create("TestNumber", () => value, x => value = x, "Tests"));
            _root.Service.OpenTab(typeof(HeliosOptionsTab));
            yield return null;
            TMP_InputField input = NumberControl<TMP_InputField>("Value");
            Button plus = NumberControl<Button>("Plus");
            var policy = new MutablePolicy();
            policy.TryUnlock("test");
            _root.Service.Access.SetPolicy(policy);
            policy.Lock(); // Expiry has no Changed event and must still reject the callback.
            input.onEndEdit.Invoke("999");
            plus.onClick.Invoke();
            Assert.AreEqual(10, value);
            yield return null;
            Assert.IsFalse(_root.Service.IsVisible);
        }

        [UnityTest]
        public IEnumerator GetterFailuresDoNotBreakRenderingOrRecovery()
        {
            var options = new IntermittentOptions();
            _root.Service.Options.RegisterInstance(options);
            _root.Service.OpenTab(typeof(HeliosOptionsTab));
            yield return null;
            Assert.DoesNotThrow(() => _root.Service.ActiveTab.Refresh());
            TMP_InputField number = _root.GetComponentsInChildren<TMP_InputField>()
                .Single(x => x.transform.parent.parent.name == "Option_Number");
            Assert.AreEqual("<unavailable>", number.text);
            options.Ready = true;
            _root.Service.ActiveTab.Refresh();
            Assert.AreEqual("10", number.text);
            options.Ready = false;
            Assert.DoesNotThrow(() => _root.Service.ActiveTab.Refresh());
            Assert.AreEqual("<unavailable>", number.text);
        }

        [UnityTest]
        public IEnumerator EditedDescriptionIsRebuiltBeforeSubmission()
        {
            _root.Service.OpenTab(typeof(HeliosBugReporterTab));
            yield return null;
            Input("Description").text = "old description";
            Button("Build").onClick.Invoke();
            yield return WaitFor(() => CachedReport() != null);
            HeliosReportBundle previous = CachedReport();
            Input("Description").text = "new description";
            Button("Default").onClick.Invoke();
            yield return WaitFor(() => _transport.Submitted != null);
            Assert.AreNotSame(previous, _transport.Submitted);
            AssertDescription(_transport.Submitted, "new description");
        }

        [UnityTest]
        public IEnumerator EditingDuringBuildDiscardsOldOperationAndPreservesDraftAcrossTabs()
        {
            _root.Service.OpenTab(typeof(HeliosBugReporterTab));
            yield return null;
            _screenshots.Block = true;
            Input("Description").text = "old description";
            Button("Build").onClick.Invoke();
            yield return WaitFor(() => _screenshots.Started);
            Input("Description").text = "current description";
            _root.Service.OpenTab(typeof(HeliosConsoleTab));
            _root.Service.OpenTab(typeof(HeliosBugReporterTab));
            yield return null;
            Assert.AreEqual("current description", Input("Description").text);
            _screenshots.Block = false;
            Button("Build").onClick.Invoke();
            yield return WaitFor(() => CachedReport() != null);
            AssertDescription(CachedReport(), "current description");
            yield return null;
            AssertDescription(CachedReport(), "current description");
        }

        private T NumberControl<T>(string name) where T : Component =>
            _root.GetComponentsInChildren<T>().First(x => x.gameObject.name == name &&
                x.transform.parent.parent.name == "Option_TestNumber");
        private TMP_InputField Input(string name) =>
            _root.GetComponentsInChildren<TMP_InputField>().First(x => x.gameObject.name == name);
        private Button Button(string name) =>
            _root.GetComponentsInChildren<Button>().First(x => x.gameObject.name == name);
        private HeliosReportBundle CachedReport() =>
            (HeliosReportBundle)typeof(HeliosBugReporterTab).GetField("_lastReport", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_root.Service.Tabs.OfType<HeliosBugReporterTab>().Single());

        private static IEnumerator WaitFor(Func<bool> condition)
        {
            for (int i = 0; i < 60 && !condition(); i++)
                yield return null;
            Assert.IsTrue(condition(), "Operation did not complete within 60 frames.");
        }

        private static void AssertDescription(HeliosReportBundle report, string expected)
        {
            Assert.IsTrue(report.TryGetArtifact("description.txt", out HeliosReportArtifact description));
            Assert.AreEqual(expected, Encoding.UTF8.GetString(description.GetContentCopy()));
        }

        private sealed class MutablePolicy : IHeliosChallengeAccessPolicy
        {
            private bool _unlocked;
            public HeliosAccessDecision Evaluate(HeliosAccessRequest request) =>
                _unlocked ? HeliosAccessDecision.Allow : HeliosAccessDecision.Challenge;
            public bool TryUnlock(string credential) { _unlocked = true; return true; }
            public void Lock() { _unlocked = false; }
        }

        private sealed class IntermittentOptions
        {
            public bool Ready;
            [HeliosOption] public int Number { get => Read(10); set { } }
            [HeliosOption] public bool Flag { get => Read(true); set { } }
            [HeliosOption] public Color Color { get => Read(UnityEngine.Color.red); set { } }
            private T Read<T>(T value) => Ready ? value : throw new InvalidOperationException("Unavailable");
        }

        private sealed class TestScreenshotProvider : IHeliosScreenshotProvider
        {
            public bool Started;
            public bool Block;
            public IEnumerator Capture(HeliosReportOperationContext context, Action<HeliosReportArtifact, Exception> complete)
            {
                Started = true;
                while (Block && !context.IsCancellationRequested)
                    yield return null;
                complete(null, null);
            }
        }

        private sealed class RecordingTransport : IHeliosReportTransport
        {
            public HeliosReportBundle Submitted;
            public HeliosTransportId Id => HeliosTransportId.LocalExport;
            public string DisplayName => "Test transport";
            public bool IsAvailable => true;
            public IEnumerator Submit(HeliosReportBundle bundle, HeliosReportOperationContext context, Action<HeliosReportResult> complete)
            {
                Submitted = bundle;
                complete(HeliosReportResult.Succeed("Recorded in memory"));
                yield break;
            }
        }
    }
}
