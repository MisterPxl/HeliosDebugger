using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using UnityEngine;
using DOTweenEngine = DG.Tweening.DOTween;

namespace Astra.Helios.Integrations.DOTween
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.DOTween", "HeliosDebugger.DOTween.Runtime")]
    public sealed class DOTweenMonitor : IDOTweenMonitor
    {
        private const int UnsetIntId = -999;
        private static readonly FieldInfo StringIdField =
            typeof(Tween).GetField("stringId", BindingFlags.Instance | BindingFlags.Public);
        private static readonly FieldInfo IntIdField =
            typeof(Tween).GetField("intId", BindingFlags.Instance | BindingFlags.Public);

        private readonly List<TweenHandle> _handles = new List<TweenHandle>();
        private readonly HashSet<Tween> _seen = new HashSet<Tween>();
        private readonly HashSet<Tween> _paused = new HashSet<Tween>();
        private readonly HashSet<long> _activeHandles = new HashSet<long>();
        private readonly List<Tween> _playingTweens = new List<Tween>();
        private readonly List<Tween> _pausedTweens = new List<Tween>();
        private long _nextHandle = 1L;

        public DOTweenMonitorSnapshot Capture()
        {
            if (DOTweenEngine.instance == null)
                return new DOTweenMonitorSnapshot(false, 0, 0, 0, Array.Empty<DOTweenTweenSnapshot>());

            try
            {
                DOTweenEngine.PlayingTweens(_playingTweens);
                DOTweenEngine.PausedTweens(_pausedTweens);
                int playingCount = DOTweenEngine.TotalPlayingTweens();
                int pausedCount = CountActive(_pausedTweens);
                int activeCount = DOTweenEngine.TotalActiveTweens();

                _seen.Clear();
                _paused.Clear();
                _activeHandles.Clear();
                AddActiveTweens(_pausedTweens, _paused);

                List<DOTweenTweenSnapshot> snapshots = new List<DOTweenTweenSnapshot>(
                    Math.Max(0, playingCount + pausedCount));
                CaptureTweens(_playingTweens, snapshots);
                CaptureTweens(_pausedTweens, snapshots);
                RemoveInactiveHandles();

                return new DOTweenMonitorSnapshot(
                    true,
                    activeCount,
                    playingCount,
                    pausedCount,
                    snapshots.ToArray());
            }
            finally
            {
                _seen.Clear();
                _paused.Clear();
                _activeHandles.Clear();
                _playingTweens.Clear();
                _pausedTweens.Clear();
            }
        }

        public bool Pause(long handle)
        {
            return Execute(handle, tween => tween.Pause());
        }

        public bool Play(long handle)
        {
            return Execute(handle, tween => tween.Play());
        }

        public bool Complete(long handle)
        {
            return Execute(handle, tween => tween.Complete());
        }

        public bool Kill(long handle)
        {
            return Execute(handle, tween => tween.Kill());
        }

        public void PauseAll()
        {
            if (DOTweenEngine.instance != null)
                DOTweenEngine.PauseAll();
        }

        public void PlayAll()
        {
            if (DOTweenEngine.instance != null)
                DOTweenEngine.PlayAll();
        }

        public void KillAll()
        {
            if (DOTweenEngine.instance != null)
                DOTweenEngine.KillAll();
        }

        private void CaptureTweens(List<Tween> tweens, List<DOTweenTweenSnapshot> snapshots)
        {
            if (tweens == null)
                return;

            for (int i = 0; i < tweens.Count; i++)
            {
                Tween tween = tweens[i];
                if (tween == null || !tween.IsActive() || !_seen.Add(tween))
                    continue;

                object target = tween.target;
                float duration = tween.Duration(true);
                float elapsed = tween.Elapsed(true);
                float progress = tween.ElapsedPercentage(false);
                string idText = FormatTweenId(tween);
                string targetText = FormatTarget(target);
                string tweenType = tween.GetType().Name;
                TweenHandle handle = GetOrCreateHandle(tween, idText, targetText, tweenType, duration);
                _activeHandles.Add(handle.Id);
                DOTweenSourceProviders.TryDescribe(tween, out DOTweenTweenSource source);
                snapshots.Add(new DOTweenTweenSnapshot(
                    handle.Id,
                    true,
                    tween.IsPlaying(),
                    _paused.Contains(tween),
                    idText,
                    targetText,
                    tweenType,
                    elapsed,
                    duration,
                    progress,
                    tween.CompletedLoops(),
                    source));
            }
        }

        private TweenHandle GetOrCreateHandle(
            Tween tween,
            string id,
            string target,
            string tweenType,
            float duration)
        {
            for (int i = 0; i < _handles.Count; i++)
            {
                Tween existing = _handles[i].Reference.Target as Tween;
                if (ReferenceEquals(existing, tween))
                {
                    if (!_handles[i].HasGenerationGuard(tween))
                    {
                        _handles.RemoveAt(i);
                        break;
                    }

                    _handles[i].UpdateFingerprint(id, target, tweenType, duration);
                    return _handles[i];
                }
            }

            TweenHandle handle = new TweenHandle(_nextHandle++, tween, id, target, tweenType, duration);
            _handles.Add(handle);
            return handle;
        }

        private bool Execute(long handleId, Action<Tween> action)
        {
            for (int i = 0; i < _handles.Count; i++)
            {
                TweenHandle handle = _handles[i];
                if (handle.Id != handleId)
                    continue;

                Tween tween = handle.Reference.Target as Tween;
                if (tween == null ||
                    !tween.IsActive() ||
                    !handle.HasGenerationGuard(tween) ||
                    !handle.MatchesFingerprint(tween))
                {
                    handle.ReleaseGenerationGuard(tween);
                    _handles.RemoveAt(i);
                    return false;
                }

                action(tween);
                return true;
            }

            return false;
        }

        private void RemoveInactiveHandles()
        {
            for (int i = _handles.Count - 1; i >= 0; i--)
            {
                Tween tween = _handles[i].Reference.Target as Tween;
                if (tween == null || !tween.IsActive() || !_activeHandles.Contains(_handles[i].Id))
                {
                    _handles[i].ReleaseGenerationGuard(tween);
                    _handles.RemoveAt(i);
                }
            }
        }

        private static int CountActive(List<Tween> tweens)
        {
            if (tweens == null)
                return 0;

            int count = 0;
            for (int i = 0; i < tweens.Count; i++)
            {
                Tween tween = tweens[i];
                if (tween != null && tween.IsActive())
                    count++;
            }

            return count;
        }

        private static void AddActiveTweens(List<Tween> source, HashSet<Tween> destination)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                Tween tween = source[i];
                if (tween != null && tween.IsActive())
                    destination.Add(tween);
            }
        }

        private static string FormatTarget(object target)
        {
            if (target == null)
                return "<none>";

            try
            {
                if (target is UnityEngine.Object unityTarget)
                {
                    if (unityTarget == null)
                        return "<destroyed>";
                    return $"{unityTarget.name} ({unityTarget.GetType().Name})";
                }

                return target.ToString();
            }
            catch (Exception)
            {
                return "<unavailable>";
            }
        }

        private static string FormatTweenId(Tween tween)
        {
            string stringId = StringIdField?.GetValue(tween) as string;
            if (!string.IsNullOrEmpty(stringId))
                return stringId;

            object intIdValue = IntIdField?.GetValue(tween);
            if (intIdValue is int intId && intId != UnsetIntId)
                return intId.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return FormatValue(tween.id, "<none>");
        }

        private static string FormatValue(object value, string nullValue)
        {
            if (value == null)
                return nullValue;

            try
            {
                return value.ToString();
            }
            catch (Exception)
            {
                return "<unavailable>";
            }
        }

        private sealed class TweenHandle
        {
            private TweenCallback _guardedOnKill;

            public TweenHandle(
                long id,
                Tween tween,
                string tweenId,
                string target,
                string tweenType,
                float duration)
            {
                Id = id;
                Reference = new WeakReference(tween);
                tween.onKill += GenerationBoundary;
                _guardedOnKill = tween.onKill;
                UpdateFingerprint(tweenId, target, tweenType, duration);
            }

            public long Id { get; }
            public WeakReference Reference { get; }
            private string TweenId { get; set; }
            private string Target { get; set; }
            private string TweenType { get; set; }
            private float Duration { get; set; }

            public void UpdateFingerprint(string tweenId, string target, string tweenType, float duration)
            {
                TweenId = tweenId;
                Target = target;
                TweenType = tweenType;
                Duration = duration;
            }

            public bool HasGenerationGuard(Tween tween)
            {
                return tween != null && ReferenceEquals(tween.onKill, _guardedOnKill);
            }

            public void ReleaseGenerationGuard(Tween tween)
            {
                if (!HasGenerationGuard(tween))
                    return;

                tween.onKill -= GenerationBoundary;
                _guardedOnKill = null;
            }

            public bool MatchesFingerprint(Tween tween)
            {
                float currentDuration = tween.Duration(true);
                return string.Equals(TweenId, FormatTweenId(tween), StringComparison.Ordinal) &&
                       string.Equals(Target, FormatTarget(tween.target), StringComparison.Ordinal) &&
                       string.Equals(TweenType, tween.GetType().Name, StringComparison.Ordinal) &&
                       (Duration.Equals(currentDuration) || Mathf.Approximately(Duration, currentDuration));
            }

            private static void GenerationBoundary()
            {
            }
        }
    }
}
