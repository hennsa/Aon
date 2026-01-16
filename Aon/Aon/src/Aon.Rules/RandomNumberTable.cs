namespace Aon.Rules;

public sealed class RandomNumberTable
{
    public int Roll(IRandomNumberGenerator generator)
    {
        return generator.Next(0, 9);
    }
}
