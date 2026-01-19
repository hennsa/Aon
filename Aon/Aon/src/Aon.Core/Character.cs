namespace Aon.Core;

public sealed class Character
{
    public const string CombatSkillBonusAttribute = "CombatSkillBonus";
    public string Name { get; set; } = string.Empty;
    public int CombatSkill { get; set; }
    public int Endurance { get; set; }
    public List<string> Disciplines { get; set; } = new();
    public Dictionary<string, int> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> CoreSkills { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Inventory Inventory { get; set; } = new();

    public int GetEffectiveCombatSkill()
    {
        return CombatSkill + GetAttributeBonus(CombatSkillBonusAttribute);
    }

    private int GetAttributeBonus(string key)
    {
        return Attributes.TryGetValue(key, out var value) ? value : 0;
    }
}
