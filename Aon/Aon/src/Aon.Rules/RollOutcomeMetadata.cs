namespace Aon.Rules;

public sealed record RollOutcomeMetadata(int Min, int Max, string TargetId, IReadOnlyList<string> Effects)
{
    public bool Contains(int roll) => roll >= Min && roll <= Max;
    public bool HasTarget => !string.IsNullOrWhiteSpace(TargetId);
    public bool HasEffects => Effects.Count > 0;
}
