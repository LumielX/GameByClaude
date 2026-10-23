namespace BooTrayal.Core.Ghosts
{
    /// <summary>GDD 6.1: Wander → Investigate → (1 s Alert) → Chase → Grab → Cooldown → Wander; Flee from a flashlight.</summary>
    public enum GhostState : byte { Wander, Investigate, Alert, Chase, Grab, Cooldown, Flee }

    /// <summary>What happened on one GhostBrain tick (for logs, sounds and the game layer).</summary>
    public enum GhostEvent { None, Heard, Alerted, ChaseStarted, Grabbed, Killed, Spared, GaveUp, Fled }
}
