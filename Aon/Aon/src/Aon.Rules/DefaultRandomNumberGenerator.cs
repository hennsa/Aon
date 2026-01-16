namespace Aon.Rules;

public sealed class DefaultRandomNumberGenerator : IRandomNumberGenerator
{
    private readonly Random _random = new();

    public int Next(int minInclusive, int maxInclusive)
    {
        return _random.Next(minInclusive, maxInclusive + 1);
    }
}
