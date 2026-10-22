using System.Collections.Generic;

namespace BooTrayal.Core.Net
{
    /// <summary>
    /// Bookkeeping for ConnectionService: which async operation is current (so Leave can cancel it),
    /// whether that operation already failed, the rejection key it received, and the host's connected roster.
    /// </summary>
    public sealed class ConnectionTracker
    {
        readonly HashSet<ulong> _clients = new HashSet<ulong>();
        int _generation;
        bool _failed;

        public string RejectionKey { get; private set; }
        public int ClientCount => _clients.Count;

        /// <summary>Starts a new operation and returns its token.</summary>
        public int Begin()
        {
            _generation++;
            _failed = false;
            RejectionKey = null;
            return _generation;
        }

        /// <summary>Makes every outstanding token stale (the player pressed Leave).</summary>
        public void Cancel() => _generation++;

        public bool IsCurrent(int token) => token == _generation;

        /// <summary>Keeps only real rejection keys; NGO transport text is ignored.</summary>
        public void RecordRejection(string reason)
        {
            if (DisconnectClassifier.IsRejection(reason)) RejectionKey = reason;
        }

        /// <summary>True the first time the current operation fails, so failure is reported once.</summary>
        public bool TryMarkFailed()
        {
            if (_failed) return false;
            _failed = true;
            return true;
        }

        public void ClientConnected(ulong clientId) => _clients.Add(clientId);

        /// <summary>False for ids that never finished connecting (e.g. refused by approval).</summary>
        public bool ClientDisconnected(ulong clientId) => _clients.Remove(clientId);

        public void ResetClients() => _clients.Clear();
    }
}
