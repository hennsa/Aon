namespace Aon.Core;

public sealed class Character
{
    public string Name { get; set; } = string.Empty;
    public int CombatSkill { get; set; }
    public int Endurance { get; set; }
    public List<string> Disciplines { get; } = new();
}
