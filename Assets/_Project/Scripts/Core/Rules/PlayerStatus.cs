namespace BooTrayal.Core.Rules
{
    public enum LifeState { Alive, MiniGhost, Escaped, Disconnected }

    /// <summary>One player's standing during a night, as seen by the server.</summary>
    public sealed class NightPlayer
    {
        public ulong Id { get; set; }
        public Role Role { get; set; }
        public LifeState State { get; set; }
        /// <summary>A Spy caught by the meeting vote (or one who disconnected).</summary>
        public bool Exposed { get; set; }
    }
}
