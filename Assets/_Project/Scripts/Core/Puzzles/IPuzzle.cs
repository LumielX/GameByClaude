namespace BooTrayal.Core.Puzzles
{
    /// <summary>Server-side puzzle state. Every puzzle can be sabotaged by the Spy (GDD 8).</summary>
    public interface IPuzzle
    {
        string Kind { get; }
        bool RequiresTwoPlayers { get; }
        bool Solved { get; }
        float Progress01 { get; }

        /// <summary>Removes progress. True only if progress existed; a solved puzzle is never undone.</summary>
        bool TrySabotage(IRandom rng, ulong saboteur);
    }
}
