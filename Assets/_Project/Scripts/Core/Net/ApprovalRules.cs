using BooTrayal.Core.Rules;

namespace BooTrayal.Core.Net
{
    public readonly struct ApprovalDecision
    {
        public bool Approved { get; }
        /// <summary>"" | "bad_payload" | "version_mismatch" | "room_full" | "game_in_progress"</summary>
        public string Reason { get; }

        public ApprovalDecision(bool approved, string reason)
        {
            Approved = approved;
            Reason = reason;
        }

        public static ApprovalDecision Reject(string reason) => new ApprovalDecision(false, reason);
    }

    /// <summary>Host-side decision for an incoming connection (version first, then room state).</summary>
    public static class ApprovalRules
    {
        public static ApprovalDecision Evaluate(byte[] payload, string hostVersion, SessionPhase phase, int connectedPlayers, int maxPlayers)
        {
            if (!ConnectionPayload.TryDecode(payload, out var p)) return ApprovalDecision.Reject("bad_payload");
            if (!GameVersion.IsCompatible(hostVersion, p.Version)) return ApprovalDecision.Reject("version_mismatch");
            if (connectedPlayers >= maxPlayers) return ApprovalDecision.Reject("room_full");
            if (!JoinPolicy.CanJoin(phase, connectedPlayers, maxPlayers, false)) return ApprovalDecision.Reject("game_in_progress");
            return new ApprovalDecision(true, "");
        }
    }
}
