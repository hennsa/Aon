namespace Aon.Rules;

public interface IRandomNumberGenerator
{
    int Next(int minInclusive, int maxInclusive);
}
