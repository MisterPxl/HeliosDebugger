using System;
using System.Collections;
using System.Collections.Generic;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosReportTransport
    {
        HeliosTransportId Id { get; }

        string DisplayName { get; }

        bool IsAvailable { get; }

        IEnumerator Submit(
            HeliosReportBundle bundle,
            HeliosReportOperationContext context,
            Action<HeliosReportResult> complete);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosTransportRegistry
    {
        private readonly Dictionary<HeliosTransportId, IHeliosReportTransport> _byId =
            new Dictionary<HeliosTransportId, IHeliosReportTransport>();
        private readonly List<IHeliosReportTransport> _ordered = new List<IHeliosReportTransport>();

        public IReadOnlyList<IHeliosReportTransport> Transports
        {
            get { return _ordered.AsReadOnly(); }
        }

        public void Register(IHeliosReportTransport transport)
        {
            if (transport == null)
                throw new ArgumentNullException("transport");
            if (!transport.Id.IsValid)
                throw new ArgumentException("A transport must have a valid ID.", "transport");

            IHeliosReportTransport previous;
            if (_byId.TryGetValue(transport.Id, out previous))
            {
                int index = _ordered.IndexOf(previous);
                if (index >= 0)
                    _ordered[index] = transport;
            }
            else
            {
                _ordered.Add(transport);
            }

            _byId[transport.Id] = transport;
        }

        public bool Unregister(HeliosTransportId id)
        {
            IHeliosReportTransport transport;
            if (!_byId.TryGetValue(id, out transport))
                return false;

            _byId.Remove(id);
            _ordered.Remove(transport);
            return true;
        }

        public bool TryGet(HeliosTransportId id, out IHeliosReportTransport transport)
        {
            return _byId.TryGetValue(id, out transport);
        }

        public bool TryGetAvailable(HeliosTransportId id, out IHeliosReportTransport transport)
        {
            if (_byId.TryGetValue(id, out transport) && transport.IsAvailable)
                return true;

            transport = null;
            return false;
        }

        public IReadOnlyList<IHeliosReportTransport> GetAvailableTransports()
        {
            List<IHeliosReportTransport> available = new List<IHeliosReportTransport>();
            for (int i = 0; i < _ordered.Count; i++)
            {
                if (_ordered[i].IsAvailable)
                    available.Add(_ordered[i]);
            }

            return available.AsReadOnly();
        }
    }
}
