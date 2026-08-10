using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Unity.Profiling;
using UnityEngine;

namespace HeliosDebugger
{
    public enum HeliosLogLevel
    {
        Log,
        Warning,
        Error,
        Exception,
        Assert
    }

    public sealed class HeliosLogEntry
    {
        public HeliosLogEntry(int sequence, DateTime timestamp, HeliosLogLevel level, string message, string stackTrace)
        {
            Sequence = sequence;
            Timestamp = timestamp;
            Level = level;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Category = TryParseCategory(Message);
        }

        public int Sequence { get; }
        public DateTime Timestamp { get; }
        public HeliosLogLevel Level { get; }
        public string Message { get; }
        public string StackTrace { get; }
        public string Category { get; }

        private static string TryParseCategory(string message)
        {
            if (string.IsNullOrEmpty(message) || message[0] != '[')
                return string.Empty;

            int first = message.IndexOf(']');
            if (first < 0 || first + 1 >= message.Length || message[first + 1] != '[')
                return string.Empty;

            int second = message.IndexOf(']', first + 2);
            if (second < 0 || second + 1 >= message.Length || message[second + 1] != '[')
                return string.Empty;

            int third = message.IndexOf(']', second + 2);
            if (third < 0)
                return string.Empty;

            return message.Substring(second + 2, third - second - 2);
        }
    }

    public sealed class HeliosLogStore : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Queue<HeliosLogEntry> _entries = new Queue<HeliosLogEntry>();
        private readonly Queue<HeliosLogEntry> _pending = new Queue<HeliosLogEntry>();

        private int _capacity;
        private int _sequence;
        private int _revision;
        private bool _subscribed;

        public event Action Changed;

        public HeliosLogStore(int capacity)
        {
            _capacity = Mathf.Max(32, capacity);
            Subscribe();
        }

        /// <summary>
        /// Monotonic counter incremented whenever the visible entries change.
        /// Allows consumers to detect changes without allocating a snapshot.
        /// </summary>
        public int Revision
        {
            get
            {
                lock (_gate)
                {
                    return _revision;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Count;
                }
            }
        }

        public IReadOnlyList<HeliosLogEntry> Snapshot()
        {
            FlushPending();
            lock (_gate)
            {
                return new List<HeliosLogEntry>(_entries);
            }
        }

        public void SetCapacity(int capacity)
        {
            bool changed;
            lock (_gate)
            {
                _capacity = Mathf.Max(32, capacity);
                changed = TrimToCapacity();
                if (changed)
                    _revision++;
            }

            if (changed)
                Changed?.Invoke();
        }

        public void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
                _pending.Clear();
                _revision++;
            }

            Changed?.Invoke();
        }

        public void FlushPending()
        {
            bool changed = false;

            lock (_gate)
            {
                while (_pending.Count > 0)
                {
                    _entries.Enqueue(_pending.Dequeue());
                    changed = true;
                }

                TrimToCapacity();

                if (changed)
                    _revision++;
            }

            if (changed)
                Changed?.Invoke();
        }

        public string ExportText(int maxEntries)
        {
            IReadOnlyList<HeliosLogEntry> entries = Snapshot();
            int start = Mathf.Max(0, entries.Count - Mathf.Max(1, maxEntries));
            var builder = new StringBuilder(entries.Count * 96);

            for (int i = start; i < entries.Count; i++)
            {
                HeliosLogEntry entry = entries[i];
                builder.Append('[')
                    .Append(entry.Timestamp.ToString("HH:mm:ss.fff"))
                    .Append("][")
                    .Append(entry.Level)
                    .Append(']');

                if (!string.IsNullOrEmpty(entry.Category))
                    builder.Append('[').Append(entry.Category).Append(']');

                builder.Append(' ').AppendLine(entry.Message);

                if (!string.IsNullOrEmpty(entry.StackTrace))
                    builder.AppendLine(entry.StackTrace);
            }

            return builder.ToString();
        }

        public void Dispose()
        {
            if (!_subscribed)
                return;

            Application.logMessageReceivedThreaded -= OnLogReceived;
            _subscribed = false;
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            Application.logMessageReceivedThreaded += OnLogReceived;
            _subscribed = true;
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            HeliosLogLevel level = ConvertLevel(type);
            var entry = new HeliosLogEntry(
                sequence: Interlocked.Increment(ref _sequence),
                timestamp: DateTime.Now,
                level: level,
                message: condition,
                stackTrace: stackTrace);

            lock (_gate)
            {
                _pending.Enqueue(entry);
            }
        }

        private bool TrimToCapacity()
        {
            bool removed = false;
            while (_entries.Count > _capacity)
            {
                _entries.Dequeue();
                removed = true;
            }

            return removed;
        }

        private static HeliosLogLevel ConvertLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return HeliosLogLevel.Warning;
                case LogType.Error:
                    return HeliosLogLevel.Error;
                case LogType.Exception:
                    return HeliosLogLevel.Exception;
                case LogType.Assert:
                    return HeliosLogLevel.Assert;
                case LogType.Log:
                default:
                    return HeliosLogLevel.Log;
            }
        }
    }

    public sealed class HeliosProfilerSample
    {
        public HeliosProfilerSample(float frameMs, long totalMemory, long gcAllocated, long drawCalls, long scriptsTime)
            : this(frameMs, totalMemory, -1L, -1L, gcAllocated, drawCalls, scriptsTime)
        {
        }

        public HeliosProfilerSample(
            float frameMs,
            long usedMemory,
            long managedMemory,
            long reservedMemory,
            long gcAllocated,
            long drawCalls,
            long scriptsTime)
        {
            FrameMs = frameMs;
            TotalMemory = usedMemory;
            UsedMemory = usedMemory;
            ManagedMemory = managedMemory;
            ReservedMemory = reservedMemory;
            GcAllocated = gcAllocated;
            DrawCalls = drawCalls;
            ScriptsTime = scriptsTime;
        }

        public float FrameMs { get; }
        public float Fps => FrameMs <= 0f ? 0f : 1000f / FrameMs;
        public long TotalMemory { get; }
        public long UsedMemory { get; }
        public long ManagedMemory { get; }
        public long ReservedMemory { get; }
        public long GcAllocated { get; }
        public long DrawCalls { get; }
        public long ScriptsTime { get; }
    }

    public sealed class HeliosProfilerSampler : IDisposable
    {
        private readonly List<HeliosProfilerSample> _history = new List<HeliosProfilerSample>();
        private ProfilerRecorder _totalMemory;
        private ProfilerRecorder _managedMemory;
        private ProfilerRecorder _reservedMemory;
        private ProfilerRecorder _gcAllocated;
        private ProfilerRecorder _drawCalls;
        private ProfilerRecorder _scriptsTime;
        private int _capacity;
        private bool _started;

        public HeliosProfilerSampler(int capacity)
        {
            _capacity = Mathf.Max(30, capacity);
            Start();
        }

        public IReadOnlyList<HeliosProfilerSample> History => _history;
        public HeliosProfilerSample Latest => _history.Count == 0 ? null : _history[_history.Count - 1];

        public void SetCapacity(int capacity)
        {
            _capacity = Mathf.Max(30, capacity);
            Trim();
        }

        public void Tick(float deltaTime)
        {
            if (!_started)
                return;

            float frameMs = Mathf.Max(0.0001f, deltaTime * 1000f);
            var sample = new HeliosProfilerSample(
                frameMs,
                LastValue(_totalMemory),
                LastValue(_managedMemory),
                LastValue(_reservedMemory),
                LastValue(_gcAllocated),
                LastValue(_drawCalls),
                LastValue(_scriptsTime));

            _history.Add(sample);
            Trim();
        }

        public void Reset()
        {
            _history.Clear();
        }

        public string ExportText()
        {
            var builder = new StringBuilder(_history.Count * 64);
            for (int i = 0; i < _history.Count; i++)
            {
                HeliosProfilerSample sample = _history[i];
                builder.Append("FrameMs=")
                    .Append(sample.FrameMs.ToString("F2"))
                    .Append(", Fps=")
                    .Append(sample.Fps.ToString("F1"))
                    .Append(", UsedMemory=")
                    .Append(sample.UsedMemory)
                    .Append(", ManagedMemory=")
                    .Append(sample.ManagedMemory)
                    .Append(", ReservedMemory=")
                    .Append(sample.ReservedMemory)
                    .Append(", GcAllocated=")
                    .Append(sample.GcAllocated)
                    .Append(", DrawCalls=")
                    .Append(sample.DrawCalls)
                    .Append(", ScriptsTime=")
                    .Append(sample.ScriptsTime)
                    .AppendLine();
            }

            return builder.ToString();
        }

        public void Dispose()
        {
            DisposeRecorder(ref _totalMemory);
            DisposeRecorder(ref _managedMemory);
            DisposeRecorder(ref _reservedMemory);
            DisposeRecorder(ref _gcAllocated);
            DisposeRecorder(ref _drawCalls);
            DisposeRecorder(ref _scriptsTime);
            _started = false;
        }

        private void Start()
        {
            _totalMemory = TryStart(ProfilerCategory.Memory, "Total Used Memory");
            _managedMemory = TryStart(ProfilerCategory.Memory, "GC Used Memory");
            _reservedMemory = TryStart(ProfilerCategory.Memory, "Total Reserved Memory");
            _gcAllocated = TryStart(ProfilerCategory.Memory, "GC Allocated In Frame");
            _drawCalls = TryStart(ProfilerCategory.Render, "Draw Calls Count");
            _scriptsTime = TryStart(ProfilerCategory.Scripts, "BehaviourUpdate");
            _started = true;
        }

        private static ProfilerRecorder TryStart(ProfilerCategory category, string markerName)
        {
            try
            {
                return ProfilerRecorder.StartNew(category, markerName, 1);
            }
            catch (Exception)
            {
                return default;
            }
        }

        private static long LastValue(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue : -1L;
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();
        }

        private void Trim()
        {
            int excess = _history.Count - _capacity;
            if (excess > 0)
                _history.RemoveRange(0, excess);
        }
    }
}
