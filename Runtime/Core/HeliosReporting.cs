using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HeliosDebugger
{
    public interface IHeliosSystemInfoProvider
    {
        string Name { get; }
        void Collect(List<HeliosSerializablePair> values);
    }

    public sealed class HeliosSystemInfoRegistry
    {
        private readonly List<IHeliosSystemInfoProvider> _providers = new List<IHeliosSystemInfoProvider>();

        public IReadOnlyList<IHeliosSystemInfoProvider> Providers => _providers;

        public void RegisterProvider(IHeliosSystemInfoProvider provider)
        {
            if (provider == null || _providers.Contains(provider))
                return;

            _providers.Add(provider);
        }

        public IReadOnlyList<HeliosSerializablePair> Snapshot()
        {
            var values = new List<HeliosSerializablePair>(64);
            for (int i = 0; i < _providers.Count; i++)
            {
                try
                {
                    _providers[i].Collect(values);
                }
                catch (Exception ex)
                {
                    values.Add(new HeliosSerializablePair($"{_providers[i].Name}.Error", ex.Message));
                }
            }

            return values;
        }

        public string ExportText()
        {
            IReadOnlyList<HeliosSerializablePair> values = Snapshot();
            var builder = new StringBuilder(values.Count * 48);
            for (int i = 0; i < values.Count; i++)
                builder.Append(values[i].Key).Append(": ").AppendLine(values[i].Value);

            return builder.ToString();
        }
    }

    public sealed class HeliosDefaultSystemInfoProvider : IHeliosSystemInfoProvider
    {
        public string Name => "Unity";

        public void Collect(List<HeliosSerializablePair> values)
        {
            values.Add(new HeliosSerializablePair("Application.Identifier", Application.identifier));
            values.Add(new HeliosSerializablePair("Application.Version", Application.version));
            values.Add(new HeliosSerializablePair("Application.UnityVersion", Application.unityVersion));
            values.Add(new HeliosSerializablePair("Application.Platform", Application.platform.ToString()));
            values.Add(new HeliosSerializablePair("Application.SystemLanguage", Application.systemLanguage.ToString()));
            values.Add(new HeliosSerializablePair("Application.InternetReachability", Application.internetReachability.ToString()));
            values.Add(new HeliosSerializablePair("Build.Debug", UnityEngine.Debug.isDebugBuild.ToString()));
            values.Add(new HeliosSerializablePair("Device.Model", SystemInfo.deviceModel));
            values.Add(new HeliosSerializablePair("Device.Name", SystemInfo.deviceName));
            values.Add(new HeliosSerializablePair("Device.Type", SystemInfo.deviceType.ToString()));
            values.Add(new HeliosSerializablePair("OS", SystemInfo.operatingSystem));
            values.Add(new HeliosSerializablePair("CPU.Type", SystemInfo.processorType));
            values.Add(new HeliosSerializablePair("CPU.Count", SystemInfo.processorCount.ToString()));
            values.Add(new HeliosSerializablePair("Memory.SystemMB", SystemInfo.systemMemorySize.ToString()));
            values.Add(new HeliosSerializablePair("GPU.Name", SystemInfo.graphicsDeviceName));
            values.Add(new HeliosSerializablePair("GPU.Type", SystemInfo.graphicsDeviceType.ToString()));
            values.Add(new HeliosSerializablePair("GPU.MemoryMB", SystemInfo.graphicsMemorySize.ToString()));
            values.Add(new HeliosSerializablePair("Screen.Size", $"{Screen.width}x{Screen.height}"));
            values.Add(new HeliosSerializablePair("Screen.Dpi", Screen.dpi.ToString("F1")));
            values.Add(new HeliosSerializablePair("Screen.SafeArea", Screen.safeArea.ToString()));
            values.Add(new HeliosSerializablePair("Quality.Level", QualitySettings.names.Length > QualitySettings.GetQualityLevel()
                ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                : QualitySettings.GetQualityLevel().ToString()));
        }
    }

    public sealed class HeliosReportAttachment
    {
        public HeliosReportAttachment(string fileName, byte[] bytes)
        {
            FileName = fileName;
            Bytes = bytes ?? Array.Empty<byte>();
        }

        public string FileName { get; }
        public byte[] Bytes { get; }
    }

    public sealed class HeliosBugReport
    {
        public string Description { get; set; }
        public string DirectoryPath { get; set; }
        public string LogsPath { get; set; }
        public string SystemInfoPath { get; set; }
        public string ProfilerPath { get; set; }
        public string ScreenshotPath { get; set; }
        public IReadOnlyList<string> AttachmentPaths { get; set; }
    }

    public sealed class HeliosReportResult
    {
        private HeliosReportResult(bool success, string message, string path, Exception exception)
        {
            Success = success;
            Message = message;
            Path = path;
            Exception = exception;
        }

        public bool Success { get; }
        public string Message { get; }
        public string Path { get; }
        public Exception Exception { get; }

        public static HeliosReportResult Succeed(string message, string path = null)
        {
            return new HeliosReportResult(true, message, path, null);
        }

        public static HeliosReportResult Fail(string message, Exception exception = null)
        {
            return new HeliosReportResult(false, message, null, exception);
        }
    }

    public interface IHeliosReportTransport
    {
        HeliosReportTransportKind Kind { get; }
        string DisplayName { get; }
        IEnumerator Submit(HeliosBugReport report, HeliosDebuggerSettings settings, Action<HeliosReportResult> complete);
    }

    public sealed class HeliosReportService
    {
        private readonly HeliosLogStore _logs;
        private readonly HeliosProfilerSampler _profiler;
        private readonly HeliosSystemInfoRegistry _systemInfo;
        private readonly List<IHeliosReportTransport> _transports = new List<IHeliosReportTransport>();
        private readonly List<HeliosReportAttachment> _attachments = new List<HeliosReportAttachment>();

        public HeliosReportService(HeliosLogStore logs, HeliosProfilerSampler profiler, HeliosSystemInfoRegistry systemInfo)
        {
            _logs = logs;
            _profiler = profiler;
            _systemInfo = systemInfo;
        }

        public IReadOnlyList<IHeliosReportTransport> Transports => _transports;

        public void RegisterTransport(IHeliosReportTransport transport)
        {
            if (transport == null)
                return;

            for (int i = 0; i < _transports.Count; i++)
            {
                if (_transports[i].Kind == transport.Kind)
                {
                    _transports[i] = transport;
                    return;
                }
            }

            _transports.Add(transport);
        }

        public void AddAttachment(HeliosReportAttachment attachment)
        {
            if (attachment != null)
                _attachments.Add(attachment);
        }

        public IEnumerator BuildReport(string description, HeliosDebuggerSettings settings, Action<HeliosBugReport, HeliosReportResult> complete)
        {
            string root = Path.Combine(Application.persistentDataPath, "HeliosReports");
            string directory = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            var attachmentPaths = new List<string>();
            string logsPath = null;
            string systemPath = null;
            string profilerPath = null;
            string screenshotPath = null;

            try
            {
                Directory.CreateDirectory(directory);

                string descriptionPath = Path.Combine(directory, "description.txt");
                logsPath = Path.Combine(directory, "logs.txt");
                systemPath = Path.Combine(directory, "system-info.txt");
                profilerPath = Path.Combine(directory, "profiler.txt");

                File.WriteAllText(descriptionPath, Redact(description ?? string.Empty));
                File.WriteAllText(logsPath, Redact(_logs.ExportText(settings.LogCapacity)));
                File.WriteAllText(systemPath, _systemInfo.ExportText());
                File.WriteAllText(profilerPath, _profiler.ExportText());

                for (int i = 0; i < _attachments.Count; i++)
                {
                    HeliosReportAttachment attachment = _attachments[i];
                    string safeName = MakeSafeFileName(attachment.FileName);
                    string path = Path.Combine(directory, safeName);
                    File.WriteAllBytes(path, attachment.Bytes);
                    attachmentPaths.Add(path);
                }

                screenshotPath = Path.Combine(directory, "screenshot.png");
                ScreenCapture.CaptureScreenshot(screenshotPath);
            }
            catch (Exception ex)
            {
                complete?.Invoke(null, HeliosReportResult.Fail($"Failed to build report: {ex.Message}", ex));
                yield break;
            }

            yield return null;
            yield return null;

            var report = new HeliosBugReport
            {
                Description = description,
                DirectoryPath = directory,
                LogsPath = logsPath,
                SystemInfoPath = systemPath,
                ProfilerPath = profilerPath,
                ScreenshotPath = !string.IsNullOrEmpty(screenshotPath) && File.Exists(screenshotPath) ? screenshotPath : null,
                AttachmentPaths = attachmentPaths
            };

            complete?.Invoke(report, HeliosReportResult.Succeed("Report archive created.", directory));
        }

        public IHeliosReportTransport GetTransport(HeliosReportTransportKind kind)
        {
            for (int i = 0; i < _transports.Count; i++)
            {
                if (_transports[i].Kind == kind)
                    return _transports[i];
            }

            return null;
        }

        private static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string redacted = text;
            redacted = RedactToken(redacted, "token");
            redacted = RedactToken(redacted, "password");
            redacted = RedactToken(redacted, "secret");
            redacted = RedactToken(redacted, "authorization");
            return redacted;
        }

        private static string RedactToken(string text, string marker)
        {
            int index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                int lineEnd = text.IndexOf('\n', index);
                if (lineEnd < 0)
                    lineEnd = text.Length;

                text = text.Substring(0, index) + marker + "=<redacted>" + text.Substring(lineEnd);
                index = text.IndexOf(marker, index + marker.Length, StringComparison.OrdinalIgnoreCase);
            }

            return text;
        }

        private static string MakeSafeFileName(string fileName)
        {
            string safe = string.IsNullOrWhiteSpace(fileName) ? "attachment.bin" : fileName;
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                safe = safe.Replace(invalid[i], '_');
            return safe;
        }
    }

    public sealed class HeliosLocalReportTransport : IHeliosReportTransport
    {
        public HeliosReportTransportKind Kind => HeliosReportTransportKind.LocalExport;
        public string DisplayName => "Local Export";

        public IEnumerator Submit(HeliosBugReport report, HeliosDebuggerSettings settings, Action<HeliosReportResult> complete)
        {
            complete?.Invoke(report == null
                ? HeliosReportResult.Fail("No report to export.")
                : HeliosReportResult.Succeed("Report exported locally.", report.DirectoryPath));
            yield break;
        }
    }

    public sealed class HeliosWebhookReportTransport : IHeliosReportTransport
    {
        public HeliosReportTransportKind Kind => HeliosReportTransportKind.Webhook;
        public string DisplayName => "Webhook";

        public IEnumerator Submit(HeliosBugReport report, HeliosDebuggerSettings settings, Action<HeliosReportResult> complete)
        {
            if (report == null)
            {
                complete?.Invoke(HeliosReportResult.Fail("No report to submit."));
                yield break;
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.WebhookUrl))
            {
                complete?.Invoke(HeliosReportResult.Fail("Webhook URL is not configured."));
                yield break;
            }

            string body = JsonUtility.ToJson(new HeliosWebhookPayload(report), true);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            var request = new UnityWebRequest(settings.WebhookUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = 20
            };
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            bool ok = request.result == UnityWebRequest.Result.Success;
            complete?.Invoke(ok
                ? HeliosReportResult.Succeed("Report submitted to webhook.", report.DirectoryPath)
                : HeliosReportResult.Fail($"Webhook failed: {request.error}"));
            request.Dispose();
        }
    }

    public sealed class HeliosNativeShareReportTransport : IHeliosReportTransport
    {
        public HeliosReportTransportKind Kind => HeliosReportTransportKind.NativeShare;
        public string DisplayName => "Native Share";

        public IEnumerator Submit(HeliosBugReport report, HeliosDebuggerSettings settings, Action<HeliosReportResult> complete)
        {
#if UNITY_WEBGL
            complete?.Invoke(HeliosReportResult.Fail("Native sharing is unavailable on WebGL."));
#else
            if (report == null)
                complete?.Invoke(HeliosReportResult.Fail("No report to share."));
            else
            {
                string uri = new Uri(report.DirectoryPath).AbsoluteUri;
                Application.OpenURL(uri);
                complete?.Invoke(HeliosReportResult.Succeed("Opened local report folder for sharing.", report.DirectoryPath));
            }
#endif
            yield break;
        }
    }

    [Serializable]
    public sealed class HeliosWebhookPayload
    {
        [SerializeField] private string _description;
        [SerializeField] private string _logs;
        [SerializeField] private string _systemInfo;
        [SerializeField] private string _profiler;

        public HeliosWebhookPayload(HeliosBugReport report)
        {
            _description = report.Description;
            _logs = SafeRead(report.LogsPath);
            _systemInfo = SafeRead(report.SystemInfoPath);
            _profiler = SafeRead(report.ProfilerPath);
        }

        private static string SafeRead(string path)
        {
            return string.IsNullOrEmpty(path) || !File.Exists(path) ? string.Empty : File.ReadAllText(path);
        }
    }
}
