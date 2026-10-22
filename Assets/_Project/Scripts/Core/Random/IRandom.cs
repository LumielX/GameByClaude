namespace BooTrayal.Core
{
    /// <summary>Injectable random source so game rules stay deterministic in tests.</summary>
    public interface IRandom
    {
        int Next(int minInclusive, int maxExclusive);
        double NextDouble();
    }
}
