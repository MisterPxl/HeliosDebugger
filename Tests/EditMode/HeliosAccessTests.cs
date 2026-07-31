using System;
using NUnit.Framework;

namespace HeliosDebugger.Tests
{
    public sealed class HeliosAccessTests
    {
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
