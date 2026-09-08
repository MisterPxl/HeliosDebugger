using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosSystemInfoProvider
    {
        string Name { get; }
        void Collect(List<HeliosSerializablePair> values);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosSystemInfoRegistry
    {
        private readonly List<IHeliosSystemInfoProvider> _providers = new List<IHeliosSystemInfoProvider>();

        public IReadOnlyList<IHeliosSystemInfoProvider> Providers
        {
            get { return _providers; }
        }

        public void RegisterProvider(IHeliosSystemInfoProvider provider)
        {
            if (provider == null || _providers.Contains(provider))
                return;

            _providers.Add(provider);
        }

        public bool UnregisterProvider(IHeliosSystemInfoProvider provider)
        {
            return provider != null && _providers.Remove(provider);
        }

        public IReadOnlyList<HeliosSerializablePair> Snapshot()
        {
            List<HeliosSerializablePair> values = new List<HeliosSerializablePair>(64);
            for (int i = 0; i < _providers.Count; i++)
            {
                IHeliosSystemInfoProvider provider = _providers[i];
                try
                {
                    provider.Collect(values);
                }
                catch (Exception exception)
                {
                    values.Add(new HeliosSerializablePair(provider.Name + ".Error", exception.Message));
                }
            }

            return values;
        }

        public string ExportText()
        {
            IReadOnlyList<HeliosSerializablePair> values = Snapshot();
            StringBuilder builder = new StringBuilder(values.Count * 48);
            for (int i = 0; i < values.Count; i++)
                builder.Append(values[i].Key).Append(": ").AppendLine(values[i].Value);

            return builder.ToString();
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosDefaultSystemInfoProvider : IHeliosSystemInfoProvider
    {
        public string Name
        {
            get { return "Unity"; }
        }

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
            values.Add(new HeliosSerializablePair("Screen.Size", Screen.width + "x" + Screen.height));
            values.Add(new HeliosSerializablePair("Screen.Dpi", Screen.dpi.ToString("F1")));
            values.Add(new HeliosSerializablePair("Screen.SafeArea", Screen.safeArea.ToString()));
            values.Add(new HeliosSerializablePair(
                "Quality.Level",
                QualitySettings.names.Length > QualitySettings.GetQualityLevel()
                    ? QualitySettings.names[QualitySettings.GetQualityLevel()]
                    : QualitySettings.GetQualityLevel().ToString()));
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportService
    {
        private readonly HeliosReportBuilder _builder;
        private readonly HeliosTransportRegistry _transports;

        public HeliosReportService(
            HeliosLogStore logs,
            HeliosProfilerSampler profiler,
            HeliosSystemInfoRegistry systemInfo)
            : this(
                new HeliosReportBuilder(
                    logs,
                    profiler,
                    systemInfo,
                    new HeliosUnityScreenshotProvider(),
                    new HeliosReportRedactor()),
                new HeliosTransportRegistry())
        {
        }

        public HeliosReportService(HeliosReportBuilder builder, HeliosTransportRegistry transports)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            if (transports == null)
                throw new ArgumentNullException("transports");

            _builder = builder;
            _transports = transports;
        }

        public HeliosReportBuilder Builder
        {
            get { return _builder; }
        }

        public HeliosTransportRegistry Transports
        {
            get { return _transports; }
        }

        public void RegisterTransport(IHeliosReportTransport transport)
        {
            _transports.Register(transport);
        }

        public bool UnregisterTransport(HeliosTransportId id)
        {
            return _transports.Unregister(id);
        }

        public void AddAttachment(HeliosReportArtifact attachment)
        {
            _builder.AddAttachment(attachment);
        }

        public bool RemoveAttachment(HeliosReportArtifact attachment)
        {
            return _builder.RemoveAttachment(attachment);
        }

        public void ClearAttachments()
        {
            _builder.ClearAttachments();
        }

        public IEnumerator BuildReport(
            string description,
            int maxBytes,
            HeliosReportOperationContext context,
            Action<HeliosReportBundle, HeliosReportResult> complete)
        {
            return _builder.Build(description, maxBytes, context, complete);
        }

        public IHeliosReportTransport GetTransport(HeliosTransportId id)
        {
            IHeliosReportTransport transport;
            return _transports.TryGet(id, out transport) ? transport : null;
        }

        public IEnumerator Submit(
            HeliosTransportId id,
            HeliosReportBundle bundle,
            HeliosReportOperationContext context,
            Action<HeliosReportResult> complete)
        {
            IHeliosReportTransport transport;
            if (!_transports.TryGet(id, out transport))
            {
                if (complete != null)
                    complete(HeliosReportResult.Fail("No report transport is registered for '" + id.Value + "'."));
                yield break;
            }

            if (!transport.IsAvailable)
            {
                if (complete != null)
                    complete(HeliosReportResult.Fail("The '" + transport.DisplayName + "' transport is unavailable."));
                yield break;
            }

            HeliosReportOperationContext operationContext = context ?? HeliosReportOperationContext.None;
            if (operationContext.IsCancellationRequested)
            {
                if (complete != null)
                    complete(HeliosReportResult.Cancelled());
                yield break;
            }

            yield return transport.Submit(bundle, operationContext, complete);
        }
    }
}
