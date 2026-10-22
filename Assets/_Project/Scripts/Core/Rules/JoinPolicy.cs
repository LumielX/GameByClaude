namespace BooTrayal.Core.Rules
{
    public enum SessionPhase { Lobby, InNight, Summary, Shop, GameOver }

    /// <summary>Joining or rejoining is only allowed between nights (GDD 17).</summary>
    public static class JoinPolicy
    {
        public static bool CanJoin(SessionPhase phase, int currentPlayers, int maxPlayers, bool isRejoin)
        {
            bool betweenNights = phase == SessionPhase.Lobby || phase == SessionPhase.Summary || phase == SessionPhase.Shop;
            return betweenNights && currentPlayers < maxPlayers;
        }
    }
}
