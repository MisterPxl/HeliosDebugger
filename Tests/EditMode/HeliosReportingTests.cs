using System;
using System.Collections;
using System.Text;
using NUnit.Framework;

namespace HeliosDebugger.Tests
{
    public sealed class HeliosReportingTests
    {
        [Test]
        public void TransportRegistryAcceptsLocalIdentifiers()
        {
            HeliosTransportRegistry registry = new HeliosTransportRegistry();
            FakeTransport transport = new FakeTransport(new HeliosTransportId("project.slack"));

            registry.Register(transport);

            Assert.IsTrue(registry.TryGet(new HeliosTransportId("project.slack"), out IHeliosReportTransport resolved));
            Assert.AreSame(transport, resolved);
        }

        [Test]
        public void BundleTracksArtifactsAndTotalBytes()
        {
            HeliosReportArtifact first =
                new HeliosReportArtifact("a.txt", "text/plain", Encoding.UTF8.GetBytes("abc"));
            HeliosReportArtifact second =
                new HeliosReportArtifact("b.bin", "application/octet-stream", new byte[7]);
            HeliosReportBundle bundle =
                new HeliosReportBundle("test", DateTime.UtcNow, new[] { first, second });

            Assert.AreEqual(10L, bundle.TotalBytes);
            Assert.IsTrue(bundle.TryGetArtifact("A.TXT", out HeliosReportArtifact resolved));
            Assert.AreSame(first, resolved);
        }

        [Test]
        public void RedactorPreservesLineAndRemovesSecretValue()
        {
            HeliosReportRedactor redactor = new HeliosReportRedactor();

            string value = redactor.Redact("user=alexis&token=secret-value\nnext=true");

            StringAssert.Contains("user=alexis", value);
            StringAssert.Contains("token=<redacted>", value);
            StringAssert.Contains("next=true", value);
            StringAssert.DoesNotContain("secret-value", value);
        }

        private sealed class FakeTransport : IHeliosReportTransport
        {
            public FakeTransport(HeliosTransportId id)
            {
                Id = id;
            }

            public HeliosTransportId Id { get; }
            public string DisplayName => "Fake";
            public bool IsAvailable => true;

            public IEnumerator Submit(
                HeliosReportBundle bundle,
                HeliosReportOperationContext context,
                Action<HeliosReportResult> complete)
            {
                complete?.Invoke(HeliosReportResult.Succeed("Done"));
                yield break;
            }
        }
    }
}
