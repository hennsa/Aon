namespace Aon.Rules;

public abstract record Effect(string Raw);

public sealed record AdjustStatEffect(string StatName, int Delta, string Raw) : Effect(Raw);

public sealed record AddItemEffect(string ItemName, string Category, string Raw) : Effect(Raw);

public sealed record RemoveItemEffect(string ItemName, string Raw) : Effect(Raw);

public sealed record SetFlagEffect(string FlagName, string Value, string Raw) : Effect(Raw);

public sealed record GrantDisciplineEffect(string DisciplineName, string Raw) : Effect(Raw);

public sealed record UpdateCounterEffect(string CounterName, int Value, bool IsAbsolute, string Raw) : Effect(Raw);

public sealed record UnsupportedEffect(string Raw) : Effect(Raw);
