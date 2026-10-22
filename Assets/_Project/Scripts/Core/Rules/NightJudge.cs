using System.Collections.Generic;
using System.Linq;

namespace BooTrayal.Core.Rules
{
    public enum NightWinner { None, Crew, Spies }

    public readonly struct NightVerdict
    {
        public bool Ended { get; }
        public NightWinner Winner { get; }
        /// <summary>"escaped" | "time_up" | "crew_wiped" | "sabotage_goal" | "" while ongoing.</summary>
        public string Reason { get; }

        public NightVerdict(bool ended, NightWinner winner, string reason)
        {
            Ended = ended;
            Winner = winner;
            Reason = reason;
        }
    }

    /// <summary>Decides whether a night is over and who won (GDD 4.2).</summary>
    public static class NightJudge
    {
        public static NightVerdict Evaluate(IReadOnlyList<NightPlayer> players, bool ritualComplete, double secondsLeft, SabotageTracker sabotage)
        {
            var crew = players.Where(p => p.Role == Role.Crew).ToList();
            bool coOp = sabotage.Spies.Count == 0;
            bool spyGoal = !coOp && sabotage.GoalReached && sabotage.AnyUncaught;
            int alive = crew.Count(p => p.State == LifeState.Alive);
            int escaped = crew.Count(p => p.State == LifeState.Escaped);
            var loserWinner = coOp ? NightWinner.None : NightWinner.Spies;

            if (alive == 0 && escaped == 0)
                return new NightVerdict(true, loserWinner, "crew_wiped");

            if (ritualComplete && escaped > 0 && alive == 0)
                return new NightVerdict(true, spyGoal ? NightWinner.Spies : NightWinner.Crew, spyGoal ? "sabotage_goal" : "escaped");

            if (secondsLeft <= 0)
            {
                bool crewMadeIt = ritualComplete && escaped > 0;
                var winner = spyGoal ? NightWinner.Spies : crewMadeIt ? NightWinner.Crew : loserWinner;
                return new NightVerdict(true, winner, spyGoal ? "sabotage_goal" : crewMadeIt ? "escaped" : "time_up");
            }

            return spyGoal
                ? new NightVerdict(false, NightWinner.Spies, "sabotage_goal")
                : new NightVerdict(false, NightWinner.None, "");
        }
    }
}
