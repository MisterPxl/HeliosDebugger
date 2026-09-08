using System;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportProgress
    {
        public HeliosReportProgress(string stage, int completedSteps, int totalSteps, string message)
        {
            Stage = stage ?? string.Empty;
            CompletedSteps = completedSteps;
            TotalSteps = totalSteps;
            Message = message ?? string.Empty;
        }

        public string Stage { get; private set; }

        public int CompletedSteps { get; private set; }

        public int TotalSteps { get; private set; }

        public string Message { get; private set; }

        public float Normalized
        {
            get
            {
                if (TotalSteps <= 0)
                    return 0f;

                float value = (float)CompletedSteps / TotalSteps;
                if (value < 0f)
                    return 0f;
                return value > 1f ? 1f : value;
            }
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportOperationContext
    {
        private static readonly HeliosReportOperationContext EmptyContext =
            new HeliosReportOperationContext(null, null);

        private readonly Func<bool> _isCancellationRequested;
        private readonly Action<HeliosReportProgress> _progress;

        public HeliosReportOperationContext(
            Func<bool> isCancellationRequested,
            Action<HeliosReportProgress> progress)
        {
            _isCancellationRequested = isCancellationRequested;
            _progress = progress;
        }

        public static HeliosReportOperationContext None
        {
            get { return EmptyContext; }
        }

        public bool IsCancellationRequested
        {
            get { return _isCancellationRequested != null && _isCancellationRequested(); }
        }

        public void Report(string stage, int completedSteps, int totalSteps, string message)
        {
            if (_progress != null)
                _progress(new HeliosReportProgress(stage, completedSteps, totalSteps, message));
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosReportCancellationSource
    {
        private bool _isCancellationRequested;

        public bool IsCancellationRequested
        {
            get { return _isCancellationRequested; }
        }

        public void Cancel()
        {
            _isCancellationRequested = true;
        }

        public HeliosReportOperationContext CreateContext(Action<HeliosReportProgress> progress)
        {
            return new HeliosReportOperationContext(IsCancelled, progress);
        }

        public HeliosReportOperationContext CreateContext()
        {
            return CreateContext(null);
        }

        private bool IsCancelled()
        {
            return _isCancellationRequested;
        }
    }
}
