namespace BooTrayal.Core.Rules
{
    /// <summary>The lobby's Start button: only when everyone in the room has pressed Ready.</summary>
    public static class LobbyRules
    {
        public static bool CanStart(int players, int ready) => players >= 1 && ready == players;
    }
}
