using System;

namespace BooTrayal.Core.Economy
{
    /// <summary>Per-player coins for this game. Spending never lowers the ranking score.</summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }
        public int TotalScore { get; private set; }

        public void Earn(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Earned amount must be positive");
            Coins += amount;
            TotalScore += amount;
        }

        public bool TrySpend(int amount)
        {
            if (amount <= 0 || amount > Coins) return false;
            Coins -= amount;
            return true;
        }
    }
}
