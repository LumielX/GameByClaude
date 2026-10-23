using System.Collections.Generic;
using System.Linq;
using BooTrayal.Core.Economy;

namespace BooTrayal.Core.Night
{
    /// <summary>A whole game: nights played so far and every player's wallet (ranking = total coins earned).</summary>
    public sealed class GameSession
    {
        readonly Dictionary<ulong, Wallet> _wallets = new Dictionary<ulong, Wallet>();

        public GameSession(int nights)
        {
            Nights = nights < 1 ? 1 : nights;
        }

        public int Nights { get; }
        /// <summary>0-based index of the current (or next) night.</summary>
        public int NightIndex { get; private set; }
        public bool IsOver => NightIndex >= Nights;

        public void AddPlayer(ulong id)
        {
            if (!_wallets.ContainsKey(id)) _wallets[id] = new Wallet();
        }

        public Wallet WalletOf(ulong id)
        {
            AddPlayer(id);
            return _wallets[id];
        }

        public void ApplyAwards(IReadOnlyDictionary<ulong, int> awards)
        {
            foreach (var kv in awards)
                if (kv.Value > 0) WalletOf(kv.Key).Earn(kv.Value);
        }

        public void NextNight() => NightIndex++;

        public IReadOnlyList<(ulong Id, int Score)> Ranking() =>
            _wallets.Select(kv => (kv.Key, kv.Value.TotalScore))
                .OrderByDescending(x => x.Item2).ThenBy(x => x.Item1)
                .Select(x => (x.Item1, x.Item2)).ToList();
    }

    /// <summary>Which seal stations are active tonight (GDD 4.1 seal counts, 8: fewer two-person puzzles for 2 players).</summary>
    public static class SealPlanner
    {
        public static IReadOnlyList<int> Pick(IRandom rng, IReadOnlyList<bool> isTwoPerson, int count, bool limitTwoPerson)
        {
            var order = Enumerable.Range(0, isTwoPerson.Count).ToList();
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            var picked = new List<int>();
            int twoPerson = 0;
            foreach (var i in order)
            {
                if (picked.Count >= count) break;
                if (limitTwoPerson && isTwoPerson[i] && twoPerson >= 1) continue;
                picked.Add(i);
                if (isTwoPerson[i]) twoPerson++;
            }
            // Not enough solo stations: fill up with the rest.
            foreach (var i in order)
            {
                if (picked.Count >= count) break;
                if (!picked.Contains(i)) picked.Add(i);
            }
            return picked;
        }
    }
}
