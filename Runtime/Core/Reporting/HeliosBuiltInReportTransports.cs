using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace HeliosDebugger
{
    public sealed class HeliosLocalReportTransport : IHeliosReportTransport
    {
        private readonly HeliosReportMaterializer _materializer;
        private readonly string _rootDirectory;

        public HeliosLocalReportTransport()
            : this(
                new HeliosReportMaterializer(),
                System.IO.Path.Combine(Application.persistentDataPath, "HeliosReports"))
        {
        }

        public HeliosLocalReportTransport(HeliosReportMaterializer materializer, string rootDirectory)
        {
            if (materializer == null)
                throw new ArgumentNullException("materializer");
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("A local report directory is required.", "rootDirectory");

            _materializer = materializer;
            _rootDirectory = rootDirectory;
        }

        public HeliosTransportId Id
        {
            get { return HeliosTransportId.LocalExport; }
        }

        public string DisplayName
        {
            get { return "Local Export"; }
        }

        public bool IsAvailable
        {
            get { return true; }
        }

        public IEnumerator Submit(
            HeliosReportBundle bundle,
            HeliosReportOperationContext context,
            Action<HeliosReportResult> complete)
        {
            HeliosReportResult materializeResult = null;
            yield return _materializer.Materialize(
                bundle,
                _rootDirectory,
                context,
                delegate(HeliosMaterializedReport report, HeliosReportResult result)
                {
                    materializeResult = result;
                });

            if (complete == null)
                yield break;

            if (materializeResult == null)
            {
                complete(HeliosReportResult.Fail("Local report export did not complete."));
                yield break;
            }

            complete(materializeResult.Success
                ? HeliosReportResult.Succeed("Report exported locally.", materializeResult.Location)
                : materializeResult);
        }
    }

    public sealed class HeliosWebhookReportTransport : IHeliosReportTransport
    {
        private string _endpoint;
        private int _timeoutSeconds;

        public HeliosWebhookReportTransport()
            : this(null, 20)
        {
        }

        public HeliosWebhookReportTransport(string endpoint)
            : this(endpoint, 20)
        {
        }

        public HeliosWebhookReportTransport(string endpoint, int timeoutSeconds)
        {
            _endpoint = endpoint;
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public HeliosTransportId Id
        {
            get { return HeliosTransportId.Webhook; }
        }

        public string DisplayName
        {
            get { return "Webhook"; }
        }

        public bool IsAvailable
        {
            get { return IsValidEndpoint(_endpoint); }
        }

        public string Endpoint
        {
            get { return _endpoint; }
        }

        public int TimeoutSeconds
        {
            get { return _timeoutSeconds; }
        }

        public void Configure(string endpoint, int timeoutSeconds)
        {
            _endpoint = endpoint;
            _timeoutSeconds = Math.Max(1, timeoutSeconds);
        }

        public IEnumerator Submit(
            HeliosReportBundle bundle,
            HeliosReportOperationContext context,
            Action<HeliosReportResult> complete)
        {
            HeliosReportOperationContext operationContext = context ?? HeliosReportOperationContext.None;
            if (bundle == null)
            {
                Invoke(complete, HeliosReportResult.Fail("No report bundle to submit."));
                yield break;
            }

            if (!IsAvailable)
            {
                Invoke(complete, HeliosReportResult.Fail("Webhook endpoint is not configured or is invalid."));
                yield break;
            }

            if (operationContext.IsCancellationRequested)
            {
                Invoke(complete, HeliosReportResult.Cancelled());
                yield break;
            }

            string body;
            try
            {
                body = JsonUtility.ToJson(new HeliosWebhookPayload(bundle), false);
            }
            catch (Exception exception)
            {
                Invoke(
                    complete,
                    HeliosReportResult.Fail("Failed to serialize webhook payload: " + exception.Message, exception));
                yield break;
            }

            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            UnityWebRequest request;
            UnityWebRequestAsyncOperation requestOperation;
            try
            {
                request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = _timeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                requestOperation = request.SendWebRequest();
            }
            catch (Exception exception)
            {
                Invoke(
                    complete,
                    HeliosReportResult.Fail("Failed to start webhook request: " + exception.Message, exception));
                yield break;
            }

            while (!requestOperation.isDone)
            {
                if (operationContext.IsCancellationRequested)
                {
                    request.Abort();
                    request.Dispose();
                    Invoke(complete, HeliosReportResult.Cancelled());
                    yield break;
                }

                int progress = request.uploadProgress < 0f
                    ? 0
                    : (int)(request.uploadProgress * 100f);
                operationContext.Report(
                    "submit.webhook",
                    progress,
                    100,
                    "Uploading report bundle.");
                yield return null;
            }

            bool succeeded = request.result == UnityWebRequest.Result.Success;
            string error = request.error;
            long responseCode = request.responseCode;
            request.Dispose();

            if (succeeded)
            {
                operationContext.Report("submit.webhook", 100, 100, "Report bundle uploaded.");
                Invoke(complete, HeliosReportResult.Succeed("Report submitted to webhook."));
            }
            else
            {
                Invoke(
                    complete,
                    HeliosReportResult.Fail(
                        "Webhook failed with HTTP " + responseCode + ": " + (error ?? "Unknown error.")));
            }
        }

        private static bool IsValidEndpoint(string endpoint)
        {
            Uri uri;
            return Uri.TryCreate(endpoint, UriKind.Absolute, out uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static void Invoke(Action<HeliosReportResult> complete, HeliosReportResult result)
        {
            if (complete != null)
                complete(result);
        }
    }

    [Serializable]
    public sealed class HeliosWebhookPayload
    {
        public string bundleId;
        public string createdAtUtc;
        public List<HeliosWebhookArtifactPayload> artifacts;

        public HeliosWebhookPayload(HeliosReportBundle bundle)
        {
            if (bundle == null)
                throw new ArgumentNullException("bundle");

            bundleId = bundle.Id;
            createdAtUtc = bundle.CreatedAtUtc.ToString("o");
            artifacts = new List<HeliosWebhookArtifactPayload>(bundle.Artifacts.Count);
            for (int i = 0; i < bundle.Artifacts.Count; i++)
                artifacts.Add(new HeliosWebhookArtifactPayload(bundle.Artifacts[i]));
        }

        public string BundleId
        {
            get { return bundleId; }
        }

        public string CreatedAtUtc
        {
            get { return createdAtUtc; }
        }

        public IReadOnlyList<HeliosWebhookArtifactPayload> Artifacts
        {
            get { return artifacts.AsReadOnly(); }
        }
    }

    [Serializable]
    public sealed class HeliosWebhookArtifactPayload
    {
        public string name;
        public string mimeType;
        public string base64Content;

        public HeliosWebhookArtifactPayload(HeliosReportArtifact artifact)
        {
            if (artifact == null)
                throw new ArgumentNullException("artifact");

            name = artifact.Name;
            mimeType = artifact.MimeType;
            base64Content = Convert.ToBase64String(artifact.GetContentUnsafe());
        }

        public string Name
        {
            get { return name; }
        }

        public string MimeType
        {
            get { return mimeType; }
        }

        public string Base64Content
        {
            get { return base64Content; }
        }
    }
}
