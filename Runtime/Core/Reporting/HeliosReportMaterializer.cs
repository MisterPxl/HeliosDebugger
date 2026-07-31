using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace HeliosDebugger
{
    public sealed class HeliosMaterializedArtifact
    {
        public HeliosMaterializedArtifact(string name, string mimeType, string path)
        {
            Name = name;
            MimeType = mimeType;
            Path = path;
        }

        public string Name { get; private set; }

        public string MimeType { get; private set; }

        public string Path { get; private set; }
    }

    public sealed class HeliosMaterializedReport
    {
        private readonly IReadOnlyList<HeliosMaterializedArtifact> _artifacts;

        public HeliosMaterializedReport(
            HeliosReportBundle bundle,
            string directoryPath,
            IList<HeliosMaterializedArtifact> artifacts)
        {
            if (bundle == null)
                throw new ArgumentNullException("bundle");
            if (string.IsNullOrEmpty(directoryPath))
                throw new ArgumentException("A materialized report requires a directory.", "directoryPath");
            if (artifacts == null)
                throw new ArgumentNullException("artifacts");

            Bundle = bundle;
            DirectoryPath = directoryPath;
            _artifacts = new List<HeliosMaterializedArtifact>(artifacts).AsReadOnly();
        }

        public HeliosReportBundle Bundle { get; private set; }

        public string DirectoryPath { get; private set; }

        public IReadOnlyList<HeliosMaterializedArtifact> Artifacts
        {
            get { return _artifacts; }
        }
    }

    public sealed class HeliosReportMaterializer
    {
        public IEnumerator Materialize(
            HeliosReportBundle bundle,
            string rootDirectory,
            HeliosReportOperationContext context,
            Action<HeliosMaterializedReport, HeliosReportResult> complete)
        {
            HeliosReportOperationContext operationContext = context ?? HeliosReportOperationContext.None;
            if (bundle == null)
            {
                Complete(complete, null, HeliosReportResult.Fail("No report bundle to materialize."));
                yield break;
            }

            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                Complete(complete, null, HeliosReportResult.Fail("No report output directory was configured."));
                yield break;
            }

            if (operationContext.IsCancellationRequested)
            {
                Complete(complete, null, HeliosReportResult.Cancelled());
                yield break;
            }

            string reportDirectoryName =
                bundle.CreatedAtUtc.ToString("yyyyMMdd_HHmmss") + "_" + SanitizeDirectoryName(bundle.Id);
            string finalDirectory = System.IO.Path.Combine(rootDirectory, reportDirectoryName);
            string partialDirectory = finalDirectory + ".partial-" + Guid.NewGuid().ToString("N");

            try
            {
                Directory.CreateDirectory(rootDirectory);
                Directory.CreateDirectory(partialDirectory);
            }
            catch (Exception exception)
            {
                Complete(
                    complete,
                    null,
                    HeliosReportResult.Fail("Failed to create report directory: " + exception.Message, exception));
                yield break;
            }

            for (int i = 0; i < bundle.Artifacts.Count; i++)
            {
                if (operationContext.IsCancellationRequested)
                {
                    TryDeleteDirectory(partialDirectory);
                    Complete(complete, null, HeliosReportResult.Cancelled());
                    yield break;
                }

                HeliosReportArtifact artifact = bundle.Artifacts[i];
                string artifactPath = System.IO.Path.Combine(partialDirectory, artifact.Name);
                try
                {
                    File.WriteAllBytes(artifactPath, artifact.GetContentUnsafe());
                }
                catch (Exception exception)
                {
                    TryDeleteDirectory(partialDirectory);
                    Complete(
                        complete,
                        null,
                        HeliosReportResult.Fail(
                            "Failed to materialize '" + artifact.Name + "': " + exception.Message,
                            exception));
                    yield break;
                }

                operationContext.Report(
                    "materialize.artifact",
                    i + 1,
                    bundle.Artifacts.Count,
                    "Wrote '" + artifact.Name + "'.");
                yield return null;
            }

            if (operationContext.IsCancellationRequested)
            {
                TryDeleteDirectory(partialDirectory);
                Complete(complete, null, HeliosReportResult.Cancelled());
                yield break;
            }

            try
            {
                if (Directory.Exists(finalDirectory))
                    finalDirectory = finalDirectory + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);

                Directory.Move(partialDirectory, finalDirectory);
            }
            catch (Exception exception)
            {
                TryDeleteDirectory(partialDirectory);
                Complete(
                    complete,
                    null,
                    HeliosReportResult.Fail("Failed to finalize report directory: " + exception.Message, exception));
                yield break;
            }

            List<HeliosMaterializedArtifact> materializedArtifacts =
                new List<HeliosMaterializedArtifact>(bundle.Artifacts.Count);
            for (int i = 0; i < bundle.Artifacts.Count; i++)
            {
                HeliosReportArtifact artifact = bundle.Artifacts[i];
                materializedArtifacts.Add(
                    new HeliosMaterializedArtifact(
                        artifact.Name,
                        artifact.MimeType,
                        System.IO.Path.Combine(finalDirectory, artifact.Name)));
            }

            HeliosMaterializedReport report =
                new HeliosMaterializedReport(bundle, finalDirectory, materializedArtifacts);
            operationContext.Report(
                "materialize.complete",
                bundle.Artifacts.Count,
                bundle.Artifacts.Count,
                "Report materialized.");
            Complete(
                complete,
                report,
                HeliosReportResult.Succeed("Report materialized.", finalDirectory));
        }

        private static void Complete(
            Action<HeliosMaterializedReport, HeliosReportResult> complete,
            HeliosMaterializedReport report,
            HeliosReportResult result)
        {
            if (complete != null)
                complete(report, result);
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
            catch (Exception)
            {
            }
        }

        private static string SanitizeDirectoryName(string value)
        {
            char[] characters = string.IsNullOrEmpty(value)
                ? new char[] { 'r', 'e', 'p', 'o', 'r', 't' }
                : value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char character = characters[i];
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                    characters[i] = '_';
            }
            return new string(characters);
        }
    }
}
