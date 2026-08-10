using System;
using System.Collections.Generic;

namespace HeliosDebugger.DOTween
{
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
    }

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
                   Contains(tween.TweenType, search);
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

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
}
