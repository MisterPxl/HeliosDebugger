using System;
using System.Collections;
using UnityEngine;

namespace HeliosDebugger
{
    public interface IHeliosNativeShareProvider
    {
        bool IsAvailable { get; }

        IEnumerator Share(
            HeliosMaterializedReport report,
            HeliosReportOperationContext context,
            Action<HeliosReportResult> complete);
    }

    public sealed class HeliosNativeShareProviderRegistry
    {
        private IHeliosNativeShareProvider _provider;

        public IHeliosNativeShareProvider Provider
        {
            get { return _provider; }
        }

        public bool HasAvailableProvider
        {
            get { return _provider != null && _provider.IsAvailable; }
        }

        public void Register(IHeliosNativeShareProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            _provider = provider;
        }

        public bool Unregister(IHeliosNativeShareProvider provider)
        {
            if (provider == null || !ReferenceEquals(_provider, provider))
                return false;

            _provider = null;
            return true;
        }

        public void Clear()
        {
            _provider = null;
        }
    }

    public sealed class HeliosNativeShareReportTransport : IHeliosReportTransport
    {
        private readonly HeliosNativeShareProviderRegistry _providers;
        private readonly HeliosReportMaterializer _materializer;
        private readonly string _materializationRoot;

        public HeliosNativeShareReportTransport()
            : this(
                new HeliosNativeShareProviderRegistry(),
                new HeliosReportMaterializer(),
                System.IO.Path.Combine(Application.temporaryCachePath, "HeliosReports", "NativeShare"))
        {
        }

        public HeliosNativeShareReportTransport(
            HeliosNativeShareProviderRegistry providers,
            HeliosReportMaterializer materializer,
            string materializationRoot)
        {
            if (providers == null)
                throw new ArgumentNullException("providers");
            if (materializer == null)
                throw new ArgumentNullException("materializer");
            if (string.IsNullOrWhiteSpace(materializationRoot))
                throw new ArgumentException("A native share materialization directory is required.", "materializationRoot");

            _providers = providers;
            _materializer = materializer;
            _materializationRoot = materializationRoot;
        }

        public HeliosTransportId Id
        {
            get { return HeliosTransportId.NativeShare; }
        }

        public string DisplayName
        {
            get { return "Native Share"; }
        }

        public bool IsAvailable
        {
            get { return _providers.HasAvailableProvider; }
        }

        public HeliosNativeShareProviderRegistry Providers
        {
            get { return _providers; }
        }

        public IEnumerator Submit(
            HeliosReportBundle bundle,
            HeliosReportOperationContext context,
            Action<HeliosReportResult> complete)
        {
            HeliosReportOperationContext operationContext = context ?? HeliosReportOperationContext.None;
            IHeliosNativeShareProvider provider = _providers.Provider;

            if (bundle == null)
            {
                Invoke(complete, HeliosReportResult.Fail("No report bundle to share."));
                yield break;
            }

            if (provider == null || !provider.IsAvailable)
            {
                Invoke(complete, HeliosReportResult.Fail("No available native share provider is registered."));
                yield break;
            }

            if (operationContext.IsCancellationRequested)
            {
                Invoke(complete, HeliosReportResult.Cancelled());
                yield break;
            }

            HeliosMaterializedReport materializedReport = null;
            HeliosReportResult materializeResult = null;
            yield return _materializer.Materialize(
                bundle,
                _materializationRoot,
                operationContext,
                delegate(HeliosMaterializedReport report, HeliosReportResult result)
                {
                    materializedReport = report;
                    materializeResult = result;
                });

            if (materializeResult == null)
            {
                Invoke(complete, HeliosReportResult.Fail("Native share materialization did not complete."));
                yield break;
            }

            if (!materializeResult.Success)
            {
                Invoke(complete, materializeResult);
                yield break;
            }

            if (operationContext.IsCancellationRequested)
            {
                CleanupMaterialized(materializedReport);
                Invoke(complete, HeliosReportResult.Cancelled());
                yield break;
            }

            operationContext.Report("submit.native-share", 0, 1, "Opening native share provider.");
            HeliosReportResult shareResult = null;
            try
            {
                yield return provider.Share(
                    materializedReport,
                    operationContext,
                    delegate(HeliosReportResult result)
                    {
                        shareResult = result;
                    });
            }
            finally
            {
                // Providers signal completion once the share sheet is dismissed
                // and must consume the files before then; the temporary
                // materialization is deleted on every outcome.
                CleanupMaterialized(materializedReport);
            }

            if (operationContext.IsCancellationRequested)
            {
                Invoke(complete, HeliosReportResult.Cancelled());
                yield break;
            }

            if (shareResult == null)
            {
                Invoke(complete, HeliosReportResult.Fail("Native share provider did not complete."));
                yield break;
            }

            if (shareResult.Success)
                operationContext.Report("submit.native-share", 1, 1, "Native share completed.");

            Invoke(complete, shareResult);
        }

        private static void CleanupMaterialized(HeliosMaterializedReport report)
        {
            if (report == null)
                return;

            try
            {
                if (System.IO.Directory.Exists(report.DirectoryPath))
                    System.IO.Directory.Delete(report.DirectoryPath, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Helios could not delete temporary share files at '" +
                    report.DirectoryPath + "': " + exception.Message);
            }
        }

        private static void Invoke(Action<HeliosReportResult> complete, HeliosReportResult result)
        {
            if (complete != null)
                complete(result);
        }
    }
}
