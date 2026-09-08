using System;
using System.Collections.Generic;
using System.IO;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public struct HeliosTransportId : IEquatable<HeliosTransportId>
    {
        public static readonly HeliosTransportId LocalExport = new HeliosTransportId("local.export");
        public static readonly HeliosTransportId Webhook = new HeliosTransportId("webhook");
        public static readonly HeliosTransportId NativeShare = new HeliosTransportId("native.share");

        private readonly string _value;

        public HeliosTransportId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A transport ID cannot be empty.", "value");

            _value = value.Trim();
        }

        public string Value
        {
            get { return _value ?? string.Empty; }
        }

        public bool IsValid
        {
            get { return !string.IsNullOrEmpty(_value); }
        }

        public bool Equals(HeliosTransportId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is HeliosTransportId && Equals((HeliosTransportId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(HeliosTransportId left, HeliosTransportId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HeliosTransportId left, HeliosTransportId right)
        {
            return !left.Equals(right);
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportArtifact
    {
        private readonly byte[] _content;

        public HeliosReportArtifact(string name, string mimeType, byte[] content)
        {
            Name = NormalizeName(name);
            MimeType = string.IsNullOrWhiteSpace(mimeType) ? "application/octet-stream" : mimeType.Trim();
            _content = content == null ? new byte[0] : (byte[])content.Clone();
        }

        public string Name { get; private set; }

        public string MimeType { get; private set; }

        public int ByteCount
        {
            get { return _content.Length; }
        }

        public byte[] GetContentCopy()
        {
            return (byte[])_content.Clone();
        }

        internal byte[] GetContentUnsafe()
        {
            return _content;
        }

        private static string NormalizeName(string name)
        {
            string normalized = string.IsNullOrWhiteSpace(name) ? "artifact.bin" : Path.GetFileName(name.Trim());
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
                normalized = normalized.Replace(invalidCharacters[i], '_');

            if (normalized == "." || normalized == ".." || string.IsNullOrWhiteSpace(normalized))
                return "artifact.bin";

            return normalized;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportBundle
    {
        private readonly List<HeliosReportArtifact> _artifacts;
        private readonly IReadOnlyList<HeliosReportArtifact> _readOnlyArtifacts;

        public HeliosReportBundle(
            string id,
            DateTime createdAtUtc,
            IEnumerable<HeliosReportArtifact> artifacts)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A report bundle ID cannot be empty.", "id");
            if (artifacts == null)
                throw new ArgumentNullException("artifacts");

            Id = id.Trim();
            CreatedAtUtc = createdAtUtc.Kind == DateTimeKind.Utc
                ? createdAtUtc
                : createdAtUtc.ToUniversalTime();
            _artifacts = new List<HeliosReportArtifact>();
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long totalBytes = 0L;

            foreach (HeliosReportArtifact artifact in artifacts)
            {
                if (artifact == null)
                    throw new ArgumentException("A report bundle cannot contain a null artifact.", "artifacts");
                if (!names.Add(artifact.Name))
                    throw new ArgumentException("Duplicate artifact name: " + artifact.Name, "artifacts");

                totalBytes += artifact.ByteCount;
                _artifacts.Add(artifact);
            }

            TotalBytes = totalBytes;
            _readOnlyArtifacts = _artifacts.AsReadOnly();
        }

        public string Id { get; private set; }

        public DateTime CreatedAtUtc { get; private set; }

        public long TotalBytes { get; private set; }

        public IReadOnlyList<HeliosReportArtifact> Artifacts
        {
            get { return _readOnlyArtifacts; }
        }

        public bool TryGetArtifact(string name, out HeliosReportArtifact artifact)
        {
            for (int i = 0; i < _artifacts.Count; i++)
            {
                if (string.Equals(_artifacts[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    artifact = _artifacts[i];
                    return true;
                }
            }

            artifact = null;
            return false;
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportResult
    {
        private HeliosReportResult(
            bool success,
            bool wasCancelled,
            string message,
            string location,
            Exception exception)
        {
            Success = success;
            WasCancelled = wasCancelled;
            Message = message ?? string.Empty;
            Location = location;
            Exception = exception;
        }

        public bool Success { get; private set; }

        public bool WasCancelled { get; private set; }

        public string Message { get; private set; }

        public string Location { get; private set; }

        public Exception Exception { get; private set; }

        public static HeliosReportResult Succeed(string message, string location)
        {
            return new HeliosReportResult(true, false, message, location, null);
        }

        public static HeliosReportResult Succeed(string message)
        {
            return Succeed(message, null);
        }

        public static HeliosReportResult Fail(string message, Exception exception)
        {
            return new HeliosReportResult(false, false, message, null, exception);
        }

        public static HeliosReportResult Fail(string message)
        {
            return Fail(message, null);
        }

        public static HeliosReportResult Cancelled(string message)
        {
            return new HeliosReportResult(false, true, message, null, null);
        }

        public static HeliosReportResult Cancelled()
        {
            return Cancelled("Report operation cancelled.");
        }
    }
}
