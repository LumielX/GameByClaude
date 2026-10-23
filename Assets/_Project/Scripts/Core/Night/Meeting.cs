using System.Collections.Generic;
using BooTrayal.Core.Rules;

namespace BooTrayal.Core.Night
{
    public enum MeetingPhase : byte { None, Discuss, Vote, Closed }

    /// <summary>One shrine-bell meeting (GDD 7.4): discussion, then a vote that closes early once everyone voted.</summary>
    public sealed class Meeting
    {
        const float Epsilon = 1e-4f;

        readonly HashSet<ulong> _voters;
        readonly HashSet<ulong> _targets;
        readonly Dictionary<ulong, ulong?> _ballots = new Dictionary<ulong, ulong?>();
        readonly float _voteSeconds;

        public Meeting(ulong caller, IReadOnlyCollection<ulong> voters, IReadOnlyCollection<ulong> targets, float discussSeconds, float voteSeconds)
        {
            Caller = caller;
            _voters = new HashSet<ulong>(voters);
            _targets = new HashSet<ulong>(targets);
            _voteSeconds = voteSeconds;
            Phase = MeetingPhase.Discuss;
            TimeLeft = discussSeconds;
        }

        public ulong Caller { get; }
        public MeetingPhase Phase { get; private set; }
        public float TimeLeft { get; private set; }
        public IReadOnlyCollection<ulong> Voters => _voters;
        public IReadOnlyCollection<ulong> Targets => _targets;
        public IReadOnlyDictionary<ulong, ulong?> Ballots => _ballots;
        /// <summary>Valid once <see cref="Phase"/> is Closed.</summary>
        public VoteResult Result { get; private set; }

        bool AllVoted => _ballots.Count >= _voters.Count;

        public MeetingPhase Tick(float dt)
        {
            switch (Phase)
            {
                case MeetingPhase.Discuss:
                    TimeLeft -= dt;
                    if (TimeLeft <= Epsilon)
                    {
                        Phase = MeetingPhase.Vote;
                        TimeLeft = _voteSeconds;
                    }
                    break;
                case MeetingPhase.Vote:
                    if (!AllVoted) TimeLeft -= dt;
                    if (AllVoted || TimeLeft <= Epsilon) Close();
                    break;
            }
            return Phase;
        }

        /// <summary>A vote for <paramref name="target"/> (null = skip). Last vote wins; only during the vote.</summary>
        public bool Cast(ulong voter, ulong? target)
        {
            if (Phase != MeetingPhase.Vote || !_voters.Contains(voter)) return false;
            if (target.HasValue && !_targets.Contains(target.Value)) return false;
            _ballots[voter] = target;
            return true;
        }

        /// <summary>A voter left or died: their ballot no longer counts and nobody waits for them.</summary>
        public void RemoveVoter(ulong id)
        {
            _voters.Remove(id);
            _ballots.Remove(id);
        }

        void Close()
        {
            Phase = MeetingPhase.Closed;
            TimeLeft = 0f;
            Result = VoteResolver.Resolve(_voters, _targets, _ballots);
        }
    }
}
