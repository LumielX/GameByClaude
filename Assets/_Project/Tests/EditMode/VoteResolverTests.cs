using System.Collections.Generic;
using BooTrayal.Core.Rules;
using NUnit.Framework;

namespace BooTrayal.Tests
{
    public class VoteResolverTests
    {
        static readonly ulong[] Five = { 1, 2, 3, 4, 5 };
        const ulong A = 1, B = 2;

        static VoteResult Resolve(Dictionary<ulong, ulong?> ballots) => VoteResolver.Resolve(Five, Five, ballots);

        [Test]
        public void AllSkip_NoEjection()
        {
            var r = Resolve(new Dictionary<ulong, ulong?> { { 1, null }, { 2, null }, { 3, null }, { 4, null }, { 5, null } });
            Assert.IsFalse(r.Ejected);
            Assert.IsNull(r.Target);
            Assert.AreEqual(5, r.SkipCount);
        }

        [Test]
        public void ClearMajority_Ejects()
        {
            var r = Resolve(new Dictionary<ulong, ulong?> { { 1, A }, { 2, A }, { 3, A }, { 4, B }, { 5, null } });
            Assert.IsTrue(r.Ejected);
            Assert.AreEqual(A, r.Target);
        }

        [Test]
        public void Tie_NoEjection()
        {
            var r = Resolve(new Dictionary<ulong, ulong?> { { 1, A }, { 2, A }, { 3, B }, { 4, B }, { 5, null } });
            Assert.IsFalse(r.Ejected);
        }

        [Test]
        public void TopEqualsSkip_NoEjection()
        {
            var r = Resolve(new Dictionary<ulong, ulong?> { { 1, A }, { 2, A }, { 3, null }, { 4, null }, { 5, B } });
            Assert.IsFalse(r.Ejected);
        }

        [Test]
        public void NonVoters_CountAsSkip()
        {
            var r = Resolve(new Dictionary<ulong, ulong?> { { 1, A }, { 2, A } });
            Assert.AreEqual(3, r.SkipCount);
            Assert.IsFalse(r.Ejected);
        }

        [Test]
        public void IneligibleVoter_Ignored()
        {
            var voters = new ulong[] { 1, 2, 3 };
            var r = VoteResolver.Resolve(voters, Five, new Dictionary<ulong, ulong?> { { 1, A }, { 2, A }, { 3, null }, { 9, B }, { 8, B } });
            Assert.IsTrue(r.Ejected);
            Assert.AreEqual(A, r.Target);
            Assert.IsFalse(r.Tally.ContainsKey(B));
        }

        [Test]
        public void IneligibleTarget_CountsAsSkip()
        {
            var targets = new ulong[] { 1, 2, 3, 4 };
            var r = VoteResolver.Resolve(Five, targets, new Dictionary<ulong, ulong?> { { 1, 5 }, { 2, 5 }, { 3, 5 }, { 4, A }, { 5, null } });
            Assert.AreEqual(4, r.SkipCount);
            Assert.IsFalse(r.Ejected);
        }

        [Test]
        public void SelfVote_Counts()
        {
            var r = Resolve(new Dictionary<ulong, ulong?> { { 1, A }, { 2, A }, { 3, A }, { 4, null }, { 5, null } });
            Assert.IsTrue(r.Ejected);
            Assert.AreEqual(A, r.Target);
        }

        [Test]
        public void Tally_ReportsCounts()
        {
            var r = Resolve(new Dictionary<ulong, ulong?> { { 1, A }, { 2, A }, { 3, B }, { 4, null } });
            Assert.AreEqual(2, r.Tally[A]);
            Assert.AreEqual(1, r.Tally[B]);
            Assert.AreEqual(2, r.SkipCount);
        }
    }
}
