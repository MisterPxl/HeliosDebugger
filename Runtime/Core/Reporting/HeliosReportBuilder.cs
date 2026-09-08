using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosScreenshotProvider
    {
        IEnumerator Capture(
            HeliosReportOperationContext context,
            Action<HeliosReportArtifact, Exception> complete);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosUnityScreenshotProvider : IHeliosScreenshotProvider
    {
        public const int DefaultMaxDimension = 1920;

        private readonly int _maxDimension;

        public HeliosUnityScreenshotProvider()
            : this(DefaultMaxDimension)
        {
        }

        public HeliosUnityScreenshotProvider(int maxDimension)
        {
            _maxDimension = Mathf.Max(320, maxDimension);
        }

        public IEnumerator Capture(
            HeliosReportOperationContext context,
            Action<HeliosReportArtifact, Exception> complete)
        {
            HeliosReportOperationContext operationContext = context ?? HeliosReportOperationContext.None;
            if (operationContext.IsCancellationRequested)
            {
                if (complete != null)
                    complete(null, null);
                yield break;
            }

            yield return new WaitForEndOfFrame();

            Texture2D texture = null;
            Texture2D scaled = null;
            HeliosReportArtifact artifact = null;
            Exception captureException = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null)
                    throw new InvalidOperationException("Unity did not return a screenshot texture.");

                scaled = Downscale(texture, _maxDimension);
                byte[] png = scaled.EncodeToPNG();
                artifact = new HeliosReportArtifact("screenshot.png", "image/png", png);
            }
            catch (Exception exception)
            {
                captureException = exception;
            }
            finally
            {
                if (scaled != null && !ReferenceEquals(scaled, texture))
                    UnityEngine.Object.Destroy(scaled);
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            if (complete != null)
                complete(artifact, captureException);
        }

        private static Texture2D Downscale(Texture2D source, int maxDimension)
        {
            int largest = Mathf.Max(source.width, source.height);
            if (largest <= maxDimension)
                return source;

            float scale = (float)maxDimension / largest;
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                result.Apply(false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportBuilder
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly HeliosLogStore _logs;
        private readonly HeliosProfilerSampler _profiler;
        private readonly HeliosSystemInfoRegistry _systemInfo;
        private readonly IHeliosScreenshotProvider _screenshotProvider;
        private readonly IHeliosReportRedactor _redactor;
        private readonly List<HeliosReportArtifact> _attachments = new List<HeliosReportArtifact>();

        public HeliosReportBuilder(
            HeliosLogStore logs,
            HeliosProfilerSampler profiler,
            HeliosSystemInfoRegistry systemInfo,
            IHeliosScreenshotProvider screenshotProvider,
            IHeliosReportRedactor redactor)
        {
            if (logs == null)
                throw new ArgumentNullException("logs");
            if (profiler == null)
                throw new ArgumentNullException("profiler");
            if (systemInfo == null)
                throw new ArgumentNullException("systemInfo");
            if (screenshotProvider == null)
                throw new ArgumentNullException("screenshotProvider");
            if (redactor == null)
                throw new ArgumentNullException("redactor");

            _logs = logs;
            _profiler = profiler;
            _systemInfo = systemInfo;
            _screenshotProvider = screenshotProvider;
            _redactor = redactor;
        }

        public IReadOnlyList<HeliosReportArtifact> Attachments
        {
            get { return _attachments.AsReadOnly(); }
        }

        public void AddAttachment(HeliosReportArtifact attachment)
        {
            if (attachment == null)
                throw new ArgumentNullException("attachment");

            _attachments.Add(attachment);
        }

        public bool RemoveAttachment(HeliosReportArtifact attachment)
        {
            return attachment != null && _attachments.Remove(attachment);
        }

        public void ClearAttachments()
        {
            _attachments.Clear();
        }

        public IEnumerator Build(
            string description,
            int maxBytes,
            HeliosReportOperationContext context,
            Action<HeliosReportBundle, HeliosReportResult> complete)
        {
            HeliosReportOperationContext operationContext = context ?? HeliosReportOperationContext.None;
            if (maxBytes <= 0)
            {
                Complete(
                    complete,
                    null,
                    HeliosReportResult.Fail("The report byte limit must be greater than zero."));
                yield break;
            }

            if (operationContext.IsCancellationRequested)
            {
                Complete(complete, null, HeliosReportResult.Cancelled());
                yield break;
            }

            List<HeliosReportArtifact> artifacts = new List<HeliosReportArtifact>();
            HashSet<string> artifactNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<HeliosReportArtifact> attachmentSnapshot = new List<HeliosReportArtifact>(_attachments);
            int totalSteps = 5 + attachmentSnapshot.Count;
            long totalBytes = 0L;
            string error = null;

            string logsText;
            string systemInfoText;
            string profilerText;
            try
            {
                logsText = _logs.ExportText(int.MaxValue);
                systemInfoText = _systemInfo.ExportText();
                profilerText = _profiler.ExportText();
            }
            catch (Exception exception)
            {
                Complete(
                    complete,
                    null,
                    HeliosReportResult.Fail("Failed to collect report data: " + exception.Message, exception));
                yield break;
            }

            HeliosReportArtifact descriptionArtifact = CreateTextArtifact(
                "description.txt",
                _redactor.Redact(description ?? string.Empty));
            if (!TryAdd(descriptionArtifact, maxBytes, artifacts, artifactNames, ref totalBytes, out error))
            {
                Complete(complete, null, HeliosReportResult.Fail(error));
                yield break;
            }

            operationContext.Report("build.description", 1, totalSteps, "Added description.");
            yield return null;
            if (CompleteIfCancelled(operationContext, complete))
                yield break;

            HeliosReportArtifact logsArtifact = CreateTextArtifact(
                "logs.txt",
                _redactor.Redact(logsText));
            if (!TryAdd(logsArtifact, maxBytes, artifacts, artifactNames, ref totalBytes, out error))
            {
                Complete(complete, null, HeliosReportResult.Fail(error));
                yield break;
            }

            operationContext.Report("build.logs", 2, totalSteps, "Added logs.");
            yield return null;
            if (CompleteIfCancelled(operationContext, complete))
                yield break;

            HeliosReportArtifact systemInfoArtifact = CreateTextArtifact(
                "system-info.txt",
                _redactor.Redact(systemInfoText));
            if (!TryAdd(systemInfoArtifact, maxBytes, artifacts, artifactNames, ref totalBytes, out error))
            {
                Complete(complete, null, HeliosReportResult.Fail(error));
                yield break;
            }

            operationContext.Report("build.system", 3, totalSteps, "Added system information.");
            yield return null;
            if (CompleteIfCancelled(operationContext, complete))
                yield break;

            HeliosReportArtifact profilerArtifact = CreateTextArtifact(
                "profiler.txt",
                _redactor.Redact(profilerText));
            if (!TryAdd(profilerArtifact, maxBytes, artifacts, artifactNames, ref totalBytes, out error))
            {
                Complete(complete, null, HeliosReportResult.Fail(error));
                yield break;
            }

            operationContext.Report("build.profiler", 4, totalSteps, "Added profiler data.");
            yield return null;
            if (CompleteIfCancelled(operationContext, complete))
                yield break;

            HeliosReportArtifact screenshot = null;
            Exception screenshotException = null;
            yield return _screenshotProvider.Capture(
                operationContext,
                delegate(HeliosReportArtifact artifact, Exception exception)
                {
                    screenshot = artifact;
                    screenshotException = exception;
                });

            if (CompleteIfCancelled(operationContext, complete))
                yield break;

            if (screenshotException != null || screenshot == null)
            {
                string message = screenshotException == null
                    ? "Screenshot was unavailable; continuing without it."
                    : "Screenshot capture failed; continuing without it: " + screenshotException.Message;
                operationContext.Report("build.screenshot", 5, totalSteps, message);
            }
            else if (!TryAdd(screenshot, maxBytes, artifacts, artifactNames, ref totalBytes, out error))
            {
                // The screenshot is optional: dropping it beats failing the
                // whole report when it does not fit the remaining budget.
                operationContext.Report(
                    "build.screenshot", 5, totalSteps, "Screenshot skipped: " + error);
            }
            else
            {
                operationContext.Report("build.screenshot", 5, totalSteps, "Added screenshot.");
            }

            for (int i = 0; i < attachmentSnapshot.Count; i++)
            {
                yield return null;
                if (CompleteIfCancelled(operationContext, complete))
                    yield break;

                HeliosReportArtifact attachment = RedactTextArtifact(attachmentSnapshot[i]);
                attachment = EnsureUniqueName(attachment, artifactNames);
                if (!TryAdd(attachment, maxBytes, artifacts, artifactNames, ref totalBytes, out error))
                {
                    operationContext.Report(
                        "build.attachment",
                        6 + i,
                        totalSteps,
                        "Attachment '" + attachment.Name + "' skipped: " + error);
                    continue;
                }

                operationContext.Report(
                    "build.attachment",
                    6 + i,
                    totalSteps,
                    "Added attachment '" + attachment.Name + "'.");
            }

            HeliosReportBundle bundle;
            try
            {
                bundle = new HeliosReportBundle(
                    Guid.NewGuid().ToString("N"),
                    DateTime.UtcNow,
                    artifacts);
            }
            catch (Exception exception)
            {
                Complete(
                    complete,
                    null,
                    HeliosReportResult.Fail("Failed to finalize report bundle: " + exception.Message, exception));
                yield break;
            }

            operationContext.Report("build.complete", totalSteps, totalSteps, "Report bundle built.");
            Complete(complete, bundle, HeliosReportResult.Succeed("Report bundle built."));
        }

        private HeliosReportArtifact RedactTextArtifact(HeliosReportArtifact artifact)
        {
            if (!IsTextMimeType(artifact.MimeType))
                return artifact;

            try
            {
                string text = StrictUtf8.GetString(artifact.GetContentUnsafe());
                return new HeliosReportArtifact(
                    artifact.Name,
                    artifact.MimeType,
                    Utf8.GetBytes(_redactor.Redact(text)));
            }
            catch (DecoderFallbackException)
            {
                return artifact;
            }
        }

        private static HeliosReportArtifact CreateTextArtifact(string name, string text)
        {
            return new HeliosReportArtifact(name, "text/plain; charset=utf-8", Utf8.GetBytes(text));
        }

        private static bool TryAdd(
            HeliosReportArtifact artifact,
            int maxBytes,
            List<HeliosReportArtifact> artifacts,
            HashSet<string> names,
            ref long totalBytes,
            out string error)
        {
            long prospectiveBytes = totalBytes + artifact.ByteCount;
            if (prospectiveBytes > maxBytes)
            {
                error = "Report exceeds the " + maxBytes +
                        " byte limit while adding '" + artifact.Name + "'.";
                return false;
            }

            if (!names.Add(artifact.Name))
            {
                error = "The report contains duplicate artifact name '" + artifact.Name + "'.";
                return false;
            }

            artifacts.Add(artifact);
            totalBytes = prospectiveBytes;
            error = null;
            return true;
        }

        private static HeliosReportArtifact EnsureUniqueName(
            HeliosReportArtifact artifact,
            HashSet<string> existingNames)
        {
            if (!existingNames.Contains(artifact.Name))
                return artifact;

            string extension = Path.GetExtension(artifact.Name);
            string baseName = Path.GetFileNameWithoutExtension(artifact.Name);
            int suffix = 2;
            string candidate;
            do
            {
                candidate = baseName + "-" + suffix + extension;
                suffix++;
            }
            while (existingNames.Contains(candidate));

            return new HeliosReportArtifact(candidate, artifact.MimeType, artifact.GetContentUnsafe());
        }

        private static bool IsTextMimeType(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType))
                return false;

            return mimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ||
                   mimeType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) ||
                   mimeType.StartsWith("application/xml", StringComparison.OrdinalIgnoreCase) ||
                   mimeType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }

        private static bool CompleteIfCancelled(
            HeliosReportOperationContext context,
            Action<HeliosReportBundle, HeliosReportResult> complete)
        {
            if (!context.IsCancellationRequested)
                return false;

            Complete(complete, null, HeliosReportResult.Cancelled());
            return true;
        }

        private static void Complete(
            Action<HeliosReportBundle, HeliosReportResult> complete,
            HeliosReportBundle bundle,
            HeliosReportResult result)
        {
            if (complete != null)
                complete(bundle, result);
        }
    }
}
