namespace Aon.Rules;

public abstract record Requirement;

public sealed record SkillPresenceRequirement(string SkillName) : Requirement;

public sealed record StatThresholdRequirement(string StatName, int Minimum) : Requirement;

public sealed record ItemPresenceRequirement(string ItemName) : Requirement;

public sealed record UnsupportedRequirement(string Raw) : Requirement;
