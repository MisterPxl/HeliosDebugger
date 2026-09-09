using System;
using System.Collections.Generic;

namespace Astra.Helios.Integrations.DOTween
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public sealed class DOTweenTweenSnapshot
    {
        public DOTweenTweenSnapshot(
            long handle,
            bool isActive,
            bool isPlaying,
            bool isPaused,
            string id,
            string target,
            string tweenType,
            float elapsed,
            float duration,
            float progress,
            int completedLoops)
        {
            Handle = handle;
            IsActive = isActive;
            IsPlaying = isPlaying;
            IsPaused = isPaused;
            Id = id ?? string.Empty;
            Target = target ?? string.Empty;
            TweenType = tweenType ?? string.Empty;
            Elapsed = elapsed;
            Duration = duration;
            Progress = progress;
            CompletedLoops = completedLoops;
        }

        public DOTweenTweenSnapshot(
            long handle,
            bool isActive,
            bool isPlaying,
            bool isPaused,
            string id,
            string target,
            string tweenType,
            float elapsed,
            float duration,
            float progress,
            int completedLoops,
            DOTweenTweenSource source)
            : this(handle, isActive, isPlaying, isPaused, id, target, tweenType, elapsed, duration, progress, completedLoops)
        {
            Source = source;
        }

        public long Handle { get; }
        public bool IsActive { get; }
        public bool IsPlaying { get; }
        public bool IsPaused { get; }
        public string Id { get; }
        public string Target { get; }
        public string TweenType { get; }
        public float Elapsed { get; }
        public float Duration { get; }
        public float Progress { get; }
        public int CompletedLoops { get; }

        /// <summary>Authoring source described by a registered provider, or <c>null</c> when none recognizes the tween.</summary>
        public DOTweenTweenSource Source { get; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public sealed class DOTweenMonitorSnapshot
    {
        public DOTweenMonitorSnapshot(
            bool isInitialized,
            int activeCount,
            int playingCount,
            int pausedCount,
            IReadOnlyList<DOTweenTweenSnapshot> tweens)
        {
            IsInitialized = isInitialized;
            ActiveCount = activeCount;
            PlayingCount = playingCount;
            PausedCount = pausedCount;
            Tweens = tweens ?? Array.Empty<DOTweenTweenSnapshot>();
        }

        public bool IsInitialized { get; }
        public int ActiveCount { get; }
        public int PlayingCount { get; }
        public int PausedCount { get; }
        public IReadOnlyList<DOTweenTweenSnapshot> Tweens { get; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public interface IDOTweenMonitor
    {
        DOTweenMonitorSnapshot Capture();
        bool Pause(long handle);
        bool Play(long handle);
        bool Complete(long handle);
        bool Kill(long handle);
        void PauseAll();
        void PlayAll();
        void KillAll();
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public static class DOTweenTweenFilter
    {
        public static bool Matches(DOTweenTweenSnapshot tween, string search)
        {
            if (tween == null)
                return false;
            if (string.IsNullOrWhiteSpace(search))
                return true;

            return Contains(tween.Id, search) ||
                   Contains(tween.Target, search) ||
                   Contains(tween.TweenType, search) ||
                   (tween.Source != null && Contains(tween.Source.Summary, search));
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public sealed class DOTweenRefreshThrottle
    {
        private readonly float _interval;
        private float _nextCaptureTime;
        private bool _hasCaptured;

        public DOTweenRefreshThrottle(float interval)
        {
            _interval = Math.Max(0f, interval);
        }

        public bool ShouldCapture(float now)
        {
            if (_hasCaptured && now < _nextCaptureTime)
                return false;

            _hasCaptured = true;
            _nextCaptureTime = now + _interval;
            return true;
        }

        public void Reset()
        {
            _hasCaptured = false;
            _nextCaptureTime = 0f;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public sealed class DOTweenKillAllConfirmation
    {
        private readonly float _duration;
        private float _expiresAt;
        private bool _armed;

        public DOTweenKillAllConfirmation(float duration)
        {
            _duration = Math.Max(0f, duration);
        }

        public bool Request(float now)
        {
            if (_armed && now <= _expiresAt)
            {
                Reset();
                return true;
            }

            _armed = true;
            _expiresAt = now + _duration;
            return false;
        }

        public bool IsArmed(float now)
        {
            if (_armed && now > _expiresAt)
                Reset();
            return _armed;
        }

        public void Reset()
        {
            _armed = false;
            _expiresAt = 0f;
        }
    }

    /// <summary>
    /// Describes where an observed tween was authored: the owning object, the asset and the
    /// step it came from, plus the owner's current diagnostics. Providers are optional; the
    /// generic integration works without any.
    /// </summary>
    public sealed class DOTweenTweenSource
    {
        public DOTweenTweenSource(string owner, string asset, string step, int diagnosticCount, UnityEngine.Object ownerObject = null)
        {
            Owner = owner ?? string.Empty;
            Asset = asset ?? string.Empty;
            Step = step ?? string.Empty;
            DiagnosticCount = Math.Max(0, diagnosticCount);
            OwnerObject = ownerObject;
            Summary = BuildSummary(Owner, Asset, Step, DiagnosticCount);
        }

        public string Owner { get; }
        public string Asset { get; }
        public string Step { get; }
        public int DiagnosticCount { get; }
        public UnityEngine.Object OwnerObject { get; }
        public string Summary { get; }

        private static string BuildSummary(string owner, string asset, string step, int diagnostics)
        {
            string summary = owner;
            if (!string.IsNullOrEmpty(asset)) summary += " · " + asset;
            if (!string.IsNullOrEmpty(step)) summary += " · " + step;
            if (diagnostics > 0) summary += " · " + diagnostics + (diagnostics == 1 ? " diagnostic" : " diagnostics");
            return summary;
        }
    }

    /// <summary>Resolves the authoring source of a live DOTween tween. Implemented by optional integrations.</summary>
    public interface IDOTweenSourceProvider
    {
        bool TryDescribe(DG.Tweening.Tween tween, out DOTweenTweenSource source);
    }

    /// <summary>
    /// Registry consulted by <see cref="DOTweenMonitor"/> for every captured tween. Registration
    /// returns a handle whose disposal unregisters; the first provider that recognizes a tween wins.
    /// Main thread only.
    /// </summary>
    public static class DOTweenSourceProviders
    {
        private static readonly List<IDOTweenSourceProvider> Providers = new List<IDOTweenSourceProvider>();

        public static int Count => Providers.Count;

        public static IDisposable Register(IDOTweenSourceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!Providers.Contains(provider))
                Providers.Add(provider);
            return new Registration(provider);
        }

        public static bool Unregister(IDOTweenSourceProvider provider) => provider != null && Providers.Remove(provider);

        public static bool IsRegistered(IDOTweenSourceProvider provider) => provider != null && Providers.Contains(provider);

        public static void Clear() => Providers.Clear();

        public static bool TryDescribe(DG.Tweening.Tween tween, out DOTweenTweenSource source)
        {
            source = null;
            if (tween == null) return false;
            for (int i = 0; i < Providers.Count; i++)
            {
                try
                {
                    if (Providers[i].TryDescribe(tween, out source) && source != null)
                        return true;
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning("[Helios] DOTween source provider " + Providers[i].GetType().Name + " failed: " + exception.Message);
                }
            }
            source = null;
            return false;
        }

        private sealed class Registration : IDisposable
        {
            private IDOTweenSourceProvider _provider;
            public Registration(IDOTweenSourceProvider provider) { _provider = provider; }
            public void Dispose() { if (_provider != null) { Unregister(_provider); _provider = null; } }
        }
    }
}
