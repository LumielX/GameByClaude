namespace BooTrayal.Core.Rules
{
    public enum Difficulty { Easy, Normal, Hard }

    /// <summary>Room options chosen by the host on the Create Room screen.</summary>
    public sealed class GameConfig
    {
        public int MaxPlayers { get; set; } = 8;
        public bool SpyEnabled { get; set; } = true;
        public int RequestedSpies { get; set; } = 1;
        public int NightMinutes { get; set; } = 10;
        public int Nights { get; set; } = 3;
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    }
}
