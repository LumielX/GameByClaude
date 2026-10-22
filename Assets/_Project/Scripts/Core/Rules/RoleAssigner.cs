using System;
using System.Collections.Generic;
using System.Linq;

namespace BooTrayal.Core.Rules
{
    public enum Role { Crew, Spy }

    public sealed class RoleAssignment
    {
        public IReadOnlyDictionary<ulong, Role> Roles { get; }
        public IReadOnlyList<ulong> Spies { get; }

        public RoleAssignment(IReadOnlyDictionary<ulong, Role> roles, IReadOnlyList<ulong> spies)
        {
            Roles = roles;
            Spies = spies;
        }

        public Role RoleOf(ulong id) => Roles[id];

        /// <summary>Other spies this player knows about (two-Spy rooms). Empty for crew.</summary>
        public IReadOnlyList<ulong> SpyPartnersOf(ulong id)
        {
            if (RoleOf(id) != Role.Spy) return Array.Empty<ulong>();
            return Spies.Where(s => s != id).ToList();
        }
    }

    public static class RoleAssigner
    {
        public static RoleAssignment Assign(IReadOnlyList<ulong> players, int spyCount, IRandom rng)
        {
            if (players.Distinct().Count() != players.Count)
                throw new ArgumentException("Duplicate player ids", nameof(players));
            if (spyCount < 0 || spyCount > players.Count - 1)
                throw new ArgumentException($"Cannot pick {spyCount} spies from {players.Count} players", nameof(spyCount));

            var shuffled = players.ToList();
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }

            var spies = shuffled.Take(spyCount).ToList();
            var roles = players.ToDictionary(p => p, p => spies.Contains(p) ? Role.Spy : Role.Crew);
            return new RoleAssignment(roles, spies);
        }
    }
}
