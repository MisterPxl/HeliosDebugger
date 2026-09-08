using System;
using System.Collections.Generic;
using System.Text;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    [Flags]
    public enum HeliosLogLevelMask
    {
        None = 0,
        Log = 1 << 0,
        Warning = 1 << 1,
        Error = 1 << 2,
        Exception = 1 << 3,
        Assert = 1 << 4,
        All = Log | Warning | Error | Exception | Assert
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosLogFilter
    {
        public HeliosLogLevelMask Levels { get; set; } = HeliosLogLevelMask.All;
        public string Search { get; set; } = string.Empty;
        public bool CollapseDuplicates { get; set; }

        public HeliosLogFilter Clone()
        {
            return new HeliosLogFilter
            {
                Levels = Levels,
                Search = Search,
                CollapseDuplicates = CollapseDuplicates
            };
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosLogViewEntry
    {
        public HeliosLogViewEntry(HeliosLogEntry representative)
        {
            Representative = representative ?? throw new ArgumentNullException(nameof(representative));
            Count = 1;
        }

        public HeliosLogEntry Representative { get; private set; }
        public int Count { get; private set; }

        internal void Add(HeliosLogEntry entry)
        {
            Count++;
            Representative = entry;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosLogQuery
    {
        private readonly HeliosLogStore _store;

        public HeliosLogQuery(HeliosLogStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public IReadOnlyList<HeliosLogViewEntry> Execute(HeliosLogFilter filter)
        {
            HeliosLogFilter effective = filter ?? new HeliosLogFilter();
            IReadOnlyList<HeliosLogEntry> source = _store.Snapshot();
            List<HeliosLogViewEntry> result = new List<HeliosLogViewEntry>(source.Count);
            Dictionary<LogIdentity, HeliosLogViewEntry> collapsed = effective.CollapseDuplicates
                ? new Dictionary<LogIdentity, HeliosLogViewEntry>()
                : null;

            for (int i = 0; i < source.Count; i++)
            {
                HeliosLogEntry entry = source[i];
                if (!Matches(entry, effective))
                    continue;

                if (collapsed == null)
                {
                    result.Add(new HeliosLogViewEntry(entry));
                    continue;
                }

                LogIdentity identity = new LogIdentity(entry);
                if (collapsed.TryGetValue(identity, out HeliosLogViewEntry existing))
                {
                    existing.Add(entry);
                    continue;
                }

                HeliosLogViewEntry viewEntry = new HeliosLogViewEntry(entry);
                collapsed.Add(identity, viewEntry);
                result.Add(viewEntry);
            }

            return result;
        }

        public string Export(HeliosLogFilter filter)
        {
            IReadOnlyList<HeliosLogViewEntry> entries = Execute(filter);
            StringBuilder builder = new StringBuilder(entries.Count * 128);
            for (int i = 0; i < entries.Count; i++)
            {
                HeliosLogViewEntry viewEntry = entries[i];
                HeliosLogEntry entry = viewEntry.Representative;
                builder.Append('[')
                    .Append(entry.Timestamp.ToString("HH:mm:ss.fff"))
                    .Append("][")
                    .Append(entry.Level)
                    .Append(']');

                if (!string.IsNullOrEmpty(entry.Category))
                    builder.Append('[').Append(entry.Category).Append(']');
                if (viewEntry.Count > 1)
                    builder.Append("[x").Append(viewEntry.Count).Append(']');

                builder.Append(' ').AppendLine(entry.Message);
                if (!string.IsNullOrEmpty(entry.StackTrace))
                    builder.AppendLine(entry.StackTrace);
            }
            return builder.ToString();
        }

        private static bool Matches(HeliosLogEntry entry, HeliosLogFilter filter)
        {
            if ((filter.Levels & MaskFor(entry.Level)) == 0)
                return false;
            if (string.IsNullOrWhiteSpace(filter.Search))
                return true;

            string search = filter.Search;
            return entry.Message.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.StackTrace.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   entry.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static HeliosLogLevelMask MaskFor(HeliosLogLevel level)
        {
            switch (level)
            {
                case HeliosLogLevel.Warning:
                    return HeliosLogLevelMask.Warning;
                case HeliosLogLevel.Error:
                    return HeliosLogLevelMask.Error;
                case HeliosLogLevel.Exception:
                    return HeliosLogLevelMask.Exception;
                case HeliosLogLevel.Assert:
                    return HeliosLogLevelMask.Assert;
                case HeliosLogLevel.Log:
                default:
                    return HeliosLogLevelMask.Log;
            }
        }

        private readonly struct LogIdentity : IEquatable<LogIdentity>
        {
            private readonly HeliosLogLevel _level;
            private readonly string _message;
            private readonly string _stackTrace;

            public LogIdentity(HeliosLogEntry entry)
            {
                _level = entry.Level;
                _message = entry.Message;
                _stackTrace = entry.StackTrace;
            }

            public bool Equals(LogIdentity other)
            {
                return _level == other._level &&
                       string.Equals(_message, other._message, StringComparison.Ordinal) &&
                       string.Equals(_stackTrace, other._stackTrace, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is LogIdentity other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)_level;
                    hash = (hash * 397) ^ (_message != null ? _message.GetHashCode() : 0);
                    hash = (hash * 397) ^ (_stackTrace != null ? _stackTrace.GetHashCode() : 0);
                    return hash;
                }
            }
        }
    }
}
