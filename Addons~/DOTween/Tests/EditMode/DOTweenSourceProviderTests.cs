using System;
using DG.Tweening;
using NUnit.Framework;

namespace Astra.Helios.Integrations.DOTween.Tests
{
    public sealed class DOTweenSourceProviderTests
    {
        [TearDown]
        public void TearDown() => DOTweenSourceProviders.Clear();

        [Test]
        public void RegistrationHandleUnregistersAndDuplicatesAreIgnored()
        {
            var provider = new FixedProvider(new DOTweenTweenSource("Player 'Intro'", "IntroSequence", "step 1 Move", 2));
            using (DOTweenSourceProviders.Register(provider))
            {
                DOTweenSourceProviders.Register(provider);
                Assert.That(DOTweenSourceProviders.Count, Is.EqualTo(1));
                Assert.That(DOTweenSourceProviders.IsRegistered(provider), Is.True);
            }
            Assert.That(DOTweenSourceProviders.Count, Is.Zero);
            Assert.That(DOTweenSourceProviders.TryDescribe(null, out _), Is.False);
        }

        [Test]
        public void SummaryFilterAndSnapshotCompatibility()
        {
            var source = new DOTweenTweenSource("Player 'Intro'", "IntroSequence", "step 1 Move", 1);
            Assert.That(source.Summary, Is.EqualTo("Player 'Intro' · IntroSequence · step 1 Move · 1 diagnostic"));
            var withSource = new DOTweenTweenSnapshot(1L, true, true, false, "intro", "Cube", "Sequence", 0f, 1f, 0f, 0, source);
            var legacy = new DOTweenTweenSnapshot(2L, true, true, false, "intro", "Cube", "Sequence", 0f, 1f, 0f, 0);
            Assert.That(legacy.Source, Is.Null);
            Assert.That(DOTweenTweenFilter.Matches(withSource, "introsequence"), Is.True);
            Assert.That(DOTweenTweenFilter.Matches(legacy, "introsequence"), Is.False);
            Assert.That(DOTweenTweenFilter.Matches(withSource, "cube"), Is.True);
        }

        [Test]
        public void FailingProviderIsSkippedAndFirstMatchWins()
        {
            DOTweenSourceProviders.Register(new ThrowingProvider());
            DOTweenSourceProviders.Register(new FixedProvider(new DOTweenTweenSource("first", null, null, 0)));
            DOTweenSourceProviders.Register(new FixedProvider(new DOTweenTweenSource("second", null, null, 0)));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("ThrowingProvider failed"));
            Assert.That(DOTweenSourceProviders.TryDescribe(DOTweenProbeTween(), out var source), Is.True);
            Assert.That(source.Owner, Is.EqualTo("first"));
        }

        private static Tween DOTweenProbeTween() => DG.Tweening.DOTween.Sequence();

        private sealed class FixedProvider : IDOTweenSourceProvider
        {
            private readonly DOTweenTweenSource _source;
            public FixedProvider(DOTweenTweenSource source) { _source = source; }
            public bool TryDescribe(Tween tween, out DOTweenTweenSource source) { source = _source; return true; }
        }

        private sealed class ThrowingProvider : IDOTweenSourceProvider
        {
            public bool TryDescribe(Tween tween, out DOTweenTweenSource source) => throw new InvalidOperationException("boom");
        }
    }
}
