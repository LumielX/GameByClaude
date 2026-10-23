using System.Collections.Generic;
using System.Linq;
using BooTrayal.Core.Economy;
using BooTrayal.Core.Rules;

namespace BooTrayal.Core.Night
{
    /// <summary>The result of a closed meeting, handed to the game layer once.</summary>
    public readonly struct MeetingOutcome
    {
        public bool Ejected { get; }
        public ulong Target { get; }
        public bool WasSpy { get; }
        public IReadOnlyList<ulong> CorrectVoters { get; }

        public MeetingOutcome(bool ejected, ulong target, bool wasSpy, IReadOnlyList<ulong> correctVoters)
        {
            Ejected = ejected;
            Target = target;
            WasSpy = wasSpy;
            CorrectVoters = correctVoters;
        }
    }

    /// <summary>
    /// One night's server-side rules (GDD 4, 5.1, 7): roles, life states, tasks, bell meetings, Spy powers,
    /// ghost modifiers and coins. The clock is the sum of Tick dts, so tests are deterministic.
    /// </summary>
    public sealed class NightSession
    {
        sealed class Modifier
        {
            public GhostModifiers Effect;
            public double Remaining;
        }

        readonly List<NightPlayer> _players;
        readonly MeetingRules _bell = new MeetingRules();
        readonly Dictionary<ulong, SpyPowerCooldowns> _powers = new Dictionary<ulong, SpyPowerCooldowns>();
        readonly Dictionary<ulong, int> _coins = new Dictionary<ulong, int>();
        readonly HashSet<ulong> _correctVoters = new HashSet<ulong>();
        readonly List<Modifier> _modifiers = new List<Modifier>();
        MeetingOutcome? _outcome;
        double _clock;
        bool _ended;
        NightVerdict _verdict;

        public NightSession(IReadOnlyList<ulong> players, int spyCount, double seconds, IRandom rng)
        {
            Roles = RoleAssigner.Assign(players, spyCount, rng);
            Sabotage = new SabotageTracker(spyCount);
            foreach (var spy in Roles.Spies)
            {
                Sabotage.Register(spy);
                _powers[spy] = new SpyPowerCooldowns();
            }
            _players = players.Select(id => new NightPlayer { Id = id, Role = Roles.RoleOf(id), State = LifeState.Alive }).ToList();
            SecondsLeft = seconds;
        }

        public RoleAssignment Roles { get; }
        public IReadOnlyList<NightPlayer> Players => _players;
        public SabotageTracker Sabotage { get; }
        public double SecondsLeft { get; private set; }
        public bool RitualComplete { get; private set; }
        public int SealsClosed { get; private set; }
        public Meeting ActiveMeeting { get; private set; }
        public bool Ended => _ended;
        public bool MeetingActive => ActiveMeeting != null;

        public float GhostSpeedMultiplier => _modifiers.Aggregate(1f, (m, x) => m * x.Effect.SpeedMul);
        public float GhostHearingMultiplier => _modifiers.Aggregate(1f, (m, x) => m * x.Effect.HearingMul);

        NightPlayer Find(ulong id) => _players.FirstOrDefault(p => p.Id == id);

        public LifeState StateOf(ulong id) => Find(id)?.State ?? LifeState.Disconnected;
        public bool IsAlive(ulong id) => Find(id)?.State == LifeState.Alive;
        public Role RoleOf(ulong id) => Find(id)?.Role ?? Role.Crew;

        /// <summary>The ghost may grab this player: alive, and not an unexposed Spy (GDD 6.1).</summary>
        public bool IsHarmable(ulong id)
        {
            var p = Find(id);
            return p != null && p.State == LifeState.Alive && !(p.Role == Role.Spy && !p.Exposed);
        }

        public NightVerdict Tick(float dt)
        {
            if (_ended) return _verdict;
            if (dt < 0f || float.IsNaN(dt)) dt = 0f;
            _clock += dt;
            SecondsLeft -= dt;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                _modifiers[i].Remaining -= dt;
                if (_modifiers[i].Remaining <= 1e-4) _modifiers.RemoveAt(i);
            }

            if (ActiveMeeting != null && ActiveMeeting.Tick(dt) == MeetingPhase.Closed)
            {
                Resolve(ActiveMeeting);
                ActiveMeeting = null;
            }

            var v = NightJudge.Evaluate(_players, RitualComplete, SecondsLeft, Sabotage);
            if (v.Ended)
            {
                _ended = true;
                _verdict = v;
                ActiveMeeting = null; // a night that ends mid-meeting cancels it
            }
            return v;
        }

        void Resolve(Meeting m)
        {
            var r = m.Result;
            if (!r.Ejected || !r.Target.HasValue)
            {
                _outcome = new MeetingOutcome(false, 0, false, new ulong[0]);
                return;
            }
            var target = Find(r.Target.Value);
            bool spy = target != null && target.Role == Role.Spy;
            var correct = new List<ulong>();
            if (target != null && target.State == LifeState.Alive) target.State = LifeState.MiniGhost;
            if (spy)
            {
                target.Exposed = true;
                Sabotage.MarkCaught(target.Id);
                correct.AddRange(m.Ballots.Where(b => b.Value == target.Id).Select(b => b.Key));
                foreach (var id in correct) _correctVoters.Add(id);
                _modifiers.Add(new Modifier { Effect = ExorciseEffects.Correct, Remaining = ExorciseEffects.Correct.Seconds });
                SecondsLeft += ExorciseEffects.CorrectBonusSeconds;
            }
            else
            {
                _modifiers.Add(new Modifier { Effect = ExorciseEffects.Wrong, Remaining = ExorciseEffects.Wrong.Seconds });
            }
            _outcome = new MeetingOutcome(true, r.Target.Value, spy, correct);
        }

        /// <summary>The last closed meeting's outcome, once.</summary>
        public MeetingOutcome? TakeOutcome()
        {
            var o = _outcome;
            _outcome = null;
            return o;
        }

        // ---- Life events ----

        public bool Kill(ulong id)
        {
            var p = Find(id);
            if (p == null || p.State != LifeState.Alive) return false;
            p.State = LifeState.MiniGhost;
            ActiveMeeting?.RemoveVoter(id);
            return true;
        }

        /// <summary>Mini ghosts can be revived at the shrine, except a Spy caught this night (GDD 7.4).</summary>
        public bool CanRevive(ulong id)
        {
            var p = Find(id);
            return p != null && p.State == LifeState.MiniGhost && !(p.Role == Role.Spy && p.Exposed);
        }

        public bool Revive(ulong id)
        {
            // A meeting is a gathering, not a safe window for chores (GDD 7.4).
            if (!CanRevive(id) || ActiveMeeting != null) return false;
            Find(id).State = LifeState.Alive;
            return true;
        }

        public bool Escape(ulong id)
        {
            var p = Find(id);
            if (p == null || p.State != LifeState.Alive || !RitualComplete || ActiveMeeting != null) return false;
            p.State = LifeState.Escaped;
            ActiveMeeting?.RemoveVoter(id);
            return true;
        }

        /// <summary>Disconnected mid-night. A Spy who leaves loses the night (GDD 7.1).</summary>
        public void PlayerLeft(ulong id)
        {
            var p = Find(id);
            if (p == null) return;
            p.State = LifeState.Disconnected;
            if (p.Role == Role.Spy)
            {
                p.Exposed = true;
                Sabotage.MarkCaught(id);
            }
            ActiveMeeting?.RemoveVoter(id);
        }

        // ---- Tasks and coins ----

        public void SealClosed(IEnumerable<ulong> contributors)
        {
            SealsClosed++;
            foreach (var id in contributors.Distinct()) Add(id, ScoreRules.SealClosed);
        }

        public void ItemDelivered(IEnumerable<ulong> by)
        {
            foreach (var id in by.Distinct()) Add(id, ScoreRules.RitualItemDelivered);
        }

        public void CompleteRitual() => RitualComplete = true;

        void Add(ulong id, int amount) => _coins[id] = (_coins.TryGetValue(id, out var n) ? n : 0) + amount;

        public IReadOnlyDictionary<ulong, int> FinishAwards(NightVerdict v)
        {
            var awards = new Dictionary<ulong, int>(_coins);
            foreach (var kv in ScoreRules.NightEndAwards(_players, v.Winner, _correctVoters, Sabotage.GoalWon))
                awards[kv.Key] = (awards.TryGetValue(kv.Key, out var n) ? n : 0) + kv.Value;
            return awards;
        }

        // ---- Meetings ----

        public bool RingBell(ulong id, bool beingChased, float discussSeconds, float voteSeconds)
        {
            if (_ended || !_bell.CanRing(id, StateOf(id), beingChased, ActiveMeeting != null)) return false;
            _bell.MarkRang(id);
            var alive = _players.Where(p => p.State == LifeState.Alive).Select(p => p.Id).ToList();
            ActiveMeeting = new Meeting(id, alive, alive, discussSeconds, voteSeconds);
            return true;
        }

        // ---- Spy powers ----

        public bool TryUsePower(ulong spy, SpyPower power, out string reason)
        {
            var p = Find(spy);
            if (p == null || p.Role != Role.Spy) { reason = "not_spy"; return false; }
            if (p.State != LifeState.Alive) { reason = "not_alive"; return false; }
            if (p.Exposed) { reason = "exposed"; return false; }
            if (ActiveMeeting != null) { reason = "meeting"; return false; }
            if (_ended) { reason = "ended"; return false; }
            var cd = _powers[spy];
            if (!cd.Ready(power, _clock)) { reason = "cooldown"; return false; }
            cd.Use(power, _clock);
            reason = "";
            return true;
        }

        public bool RecordSabotage(ulong spy, bool hadEffect) => Sabotage.Record(spy, hadEffect);
    }
}
