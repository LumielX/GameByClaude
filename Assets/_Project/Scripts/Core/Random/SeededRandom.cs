namespace BooTrayal.Core
{
    public sealed class SeededRandom : IRandom
    {
        readonly System.Random _random;

        public SeededRandom(int seed)
        {
            _random = new System.Random(seed);
        }

        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public double NextDouble() => _random.NextDouble();
    }
}
