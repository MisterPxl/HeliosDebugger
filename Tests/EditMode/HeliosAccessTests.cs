using System;
using NUnit.Framework;

namespace Astra.Helios.Tests
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Tests", "HeliosDebugger.EditMode.Tests")]
    public sealed class HeliosAccessTests
    {
        [TestCase("lock")]
        [TestCase("expire")]
        [TestCase("replace")]
        public void RevokedAccessClosesVisibleDebugger(string reason)
        {
            var service = HeliosService.CreateDefault();
            try
            {
                var policy = new MutablePolicy();
                service.Access.SetPolicy(policy);
                service.Show();
                Assert.IsTrue(service.TryUnlock("test"));
                Assert.IsTrue(service.IsVisible);
                Assert.IsTrue(service.CanInteractWithDebugger);

                if (reason == "lock")
                    service.Access.Lock();
                else if (reason == "replace")
                    service.Access.SetPolicy(new HeliosDenyAllAccessPolicy());
                else
                    policy.Lock(); // Simulates expiry without a policy change notification.

                Assert.IsFalse(service.CanInteractWithDebugger);
                if (reason != "expire")
                    Assert.IsFalse(service.IsVisible, "Explicit revocation must close immediately.");
                service.Tick(0.01f);
                Assert.IsFalse(service.IsVisible);
            }
            finally { service.Dispose(); }
        }

        private sealed class MutablePolicy : IHeliosChallengeAccessPolicy
        {
            private bool _unlocked;
            public HeliosAccessDecision Evaluate(HeliosAccessRequest request) =>
                _unlocked ? HeliosAccessDecision.Allow : HeliosAccessDecision.Challenge;
            public bool TryUnlock(string credential) { _unlocked = true; return true; }
            public void Lock() { _unlocked = false; }
        }

        [Test]
        public void PinPolicyUnlocksWithConfiguredPin()
        {
            HeliosPinAccessPolicy.CreateCredentials("3056", out string salt, out string hash);
            HeliosPinAccessPolicy policy = new HeliosPinAccessPolicy(salt, hash, TimeSpan.FromMinutes(1d));

            Assert.AreEqual(
                HeliosAccessDecision.Challenge,
                policy.Evaluate(new HeliosAccessRequest(HeliosAccessOperation.OpenDebugger)));
            Assert.IsFalse(policy.TryUnlock("0000"));
            Assert.IsTrue(policy.TryUnlock("3056"));
            Assert.AreEqual(
                HeliosAccessDecision.Allow,
                policy.Evaluate(new HeliosAccessRequest(HeliosAccessOperation.OpenDebugger)));
        }

        [Test]
        public void LockEndsActiveSession()
        {
            HeliosPinAccessPolicy.CreateCredentials("1234", out string salt, out string hash);
            HeliosPinAccessPolicy policy = new HeliosPinAccessPolicy(salt, hash, TimeSpan.FromMinutes(1d));
            Assert.IsTrue(policy.TryUnlock("1234"));

            policy.Lock();

            Assert.AreEqual(
                HeliosAccessDecision.Challenge,
                policy.Evaluate(new HeliosAccessRequest(HeliosAccessOperation.OpenDebugger)));
        }

        [Test]
        public void EmptyPinCannotBeConfigured()
        {
            Assert.Throws<ArgumentException>(() =>
                HeliosPinAccessPolicy.CreateCredentials(string.Empty, out _, out _));
        }

        [Test]
        public void RepeatedFailuresThrottleFurtherAttempts()
        {
            HeliosPinAccessPolicy.CreateCredentials("9876", out string salt, out string hash);
            HeliosPinAccessPolicy policy = new HeliosPinAccessPolicy(salt, hash, TimeSpan.FromMinutes(1d));

            for (int i = 0; i < HeliosPinAccessPolicy.FreeAttempts + 1; i++)
                Assert.IsFalse(policy.TryUnlock("0000"));

            Assert.IsTrue(policy.IsThrottled);
            Assert.IsFalse(policy.TryUnlock("9876"), "The correct PIN must be rejected while throttled.");
        }

        [Test]
        public void TruncatedCredentialsAreRejected()
        {
            HeliosPinAccessPolicy.CreateCredentials("2468", out string salt, out _);
            string shortHash = Convert.ToBase64String(new byte[8]);

            Assert.Throws<ArgumentException>(() =>
                new HeliosPinAccessPolicy(salt, shortHash, TimeSpan.FromMinutes(1d)));
        }

        [Test]
        public void UnlockResumesChallengedOperation()
        {
            HeliosPinAccessPolicy.CreateCredentials("4321", out string salt, out string hash);
            HeliosAccessController controller = new HeliosAccessController(
                new HeliosPinAccessPolicy(salt, hash, TimeSpan.FromMinutes(1d)));
            bool resumed = false;

            controller.Request(
                new HeliosAccessRequest(HeliosAccessOperation.SubmitBugReport),
                () => resumed = true);

            Assert.IsTrue(controller.TryUnlock("4321"));
            Assert.IsTrue(resumed);
        }
    }
}
