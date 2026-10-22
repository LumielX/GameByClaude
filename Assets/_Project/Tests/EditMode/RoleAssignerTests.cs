using System;
using System.Collections.Generic;
using System.Linq;
using BooTrayal.Core;
using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class RoleAssignerTests
    {
        static List<ulong> Players(int n) => Enumerable.Range(1, n).Select(i => (ulong)i).ToList();

        [Test]
        public void Assign_ZeroSpies_AllCrew()
        {
            var a = RoleAssigner.Assign(Players(4), 0, new SeededRandom(1));
            Assert.IsTrue(a.Roles.Values.All(r => r == Role.Crew));
            CollectionAssert.IsEmpty(a.Spies);
        }

        [Test]
        public void Assign_OneSpy_ExactlyOneSpy()
        {
            var a = RoleAssigner.Assign(Players(5), 1, new SeededRandom(7));
            Assert.AreEqual(1, a.Roles.Values.Count(r => r == Role.Spy));
            Assert.AreEqual(1, a.Spies.Count);
            Assert.AreEqual(Role.Spy, a.RoleOf(a.Spies[0]));
        }

        [Test]
        public void Assign_TwoSpies_DistinctAndPartnersKnowEachOther()
        {
            var a = RoleAssigner.Assign(Players(8), 2, new SeededRandom(3));
            Assert.AreEqual(2, a.Spies.Distinct().Count());
            ulong spyA = a.Spies[0], spyB = a.Spies[1];
            CollectionAssert.AreEqual(new[] { spyB }, a.SpyPartnersOf(spyA));
            CollectionAssert.AreEqual(new[] { spyA }, a.SpyPartnersOf(spyB));
            var crew = a.Roles.First(kv => kv.Value == Role.Crew).Key;
            CollectionAssert.IsEmpty(a.SpyPartnersOf(crew));
        }

        [Test]
        public void Assign_SameSeed_SameResult()
        {
            var a = RoleAssigner.Assign(Players(6), 1, new SeededRandom(42));
            var b = RoleAssigner.Assign(Players(6), 1, new SeededRandom(42));
            CollectionAssert.AreEqual(a.Spies, b.Spies);
        }

        [Test]
        public void Assign_ManySeeds_EveryPlayerCanBeSpy()
        {
            var seen = new HashSet<ulong>();
            for (int seed = 0; seed < 1000; seed++)
                seen.UnionWith(RoleAssigner.Assign(Players(5), 1, new SeededRandom(seed)).Spies);
            CollectionAssert.AreEquivalent(Players(5), seen);
        }

        [Test]
        public void Assign_DoesNotMutateInput()
        {
            var input = Players(6);
            var copy = input.ToList();
            RoleAssigner.Assign(input, 1, new SeededRandom(5));
            CollectionAssert.AreEqual(copy, input);
        }

        [Test]
        public void Assign_TooManySpies_Throws()
        {
            Assert.Throws<ArgumentException>(() => RoleAssigner.Assign(Players(3), 3, new SeededRandom(1)));
        }

        [Test]
        public void Assign_DuplicateIds_Throws()
        {
            Assert.Throws<ArgumentException>(() => RoleAssigner.Assign(new List<ulong> { 1, 2, 2, 3, 4 }, 1, new SeededRandom(1)));
        }
    }
}
