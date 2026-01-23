# Series Rules & Token Spec

## Purpose
This document captures a canonical, per-series rules specification for the rules engine.
It maps series mechanics into the existing `Requirement` and `Effect` token formats so that
`RequirementParser`, `EffectParser`, and per-series rule catalogs can be updated in a consistent way.

---

## Canonical Token Reference (Engine-Level)

### Requirement tokens (string form)
- `skill:<SkillName>`
- `item:<ItemName>`
- `stat:<StatName>:<Minimum>` or `stat:<StatName> >= <Minimum>`
- `flag:<FlagName>[:<ExpectedValue>]`
- `counter:<CounterName>:<Minimum>` or `counter:<CounterName> >= <Minimum>`
- `combat:>=<MinimumCombatModifier>`
- `slot:<SlotName>:<Minimum>` or `slot:<SlotName> >= <Minimum>`

### Effect tokens (string form)
- `stat:<StatName>:<Delta>`
- `combat:<Delta>`
- `endurance:damage:<Amount>`
- `endurance:heal:<Amount>`
- `item:add:<ItemName>[:<Category>]`
- `item:remove:<ItemName>`
- `flag:<FlagName>[:<Value>]`
- `discipline:<DisciplineName>`
- `counter:<CounterName>:+N|-N|N` (absolute if no sign)
- `slot:<SlotName>:+N|-N|N` (absolute if no sign)

### Canonical counters
These counters are used across series for inventory/rule bookkeeping:
- `gold` (Lone Wolf, Grey Star)
- `meals` (Lone Wolf)
- `ammunition` (Freeway Warrior)
- `fuel` (Freeway Warrior)
- `grenades` (Freeway Warrior)
- `medkits` (Freeway Warrior)
- `armor` (Freeway Warrior vehicle armor)
- `credits` (Freeway Warrior money)
- `rations` (Grey Star)
- `crystal-shards` (Grey Star)

### Canonical slots
Slots are represented as counters with the implicit `slot:` prefix. Example: `slot:backpack`.
Suggested slot names per series appear in the per-series sections below.

---

## Series: Lone Wolf (Kai)

### Core stats
- Combat Skill (CS)
- Endurance (END)

### Skill/discipline system
- **Kai Disciplines** are stored as character disciplines.
- Requirements map as: `skill:<DisciplineName>`.
- Grants map as: `discipline:<DisciplineName>`.

### Inventory rules
- Backpack slots: 8 (`slot:backpack`)
- Weapon slot: 1 (`slot:weapon`)
- Special items are not limited by backpack slots (represented as items with category `special`).
- Money: `counter:gold`
- Meals: `counter:meals`

### Combat resolution & modifiers
- Base combat ratio = (Player CS + combat modifier) − Enemy CS.
- Combat modifier uses `combat:<Delta>` to adjust `Character.CombatSkillBonus`.
- Book-specific modifiers (e.g., magic bonuses, Kai discipline bonuses) should map to `combat:<Delta>`
  or to stat deltas if permanent.
- Use `stat:CombatSkill:<Delta>` and `stat:Endurance:<Delta>` for permanent adjustments.

### Canonical requirements/effects mapping
- Requirement: requires Kai Discipline `Hunting` → `skill:Hunting`.
- Requirement: at least 2 meals → `counter:meals:2`.
- Effect: lose 1 END → `endurance:damage:1`.
- Effect: gain 2 gold → `counter:gold:+2`.
- Effect: gain weapon `Sword` → `item:add:Sword:weapon` + `slot:weapon:+1` (if the weapon slot is tracked).

### Example RuleDefinition entries (Lone Wolf)
```json
{
  "rules": [
    {
      "id": "lw.hunting.meal-skip",
      "requirements": ["skill:Hunting"],
      "effects": ["counter:meals:+1"]
    },
    {
      "id": "lw.sommerswerd.combat-bonus",
      "requirements": ["item:Sommerswerd"],
      "effects": ["combat:+2"]
    }
  ]
}
```

### Example Choice metadata (Lone Wolf)
```json
{
  "text": "If you have the Kai Discipline of Sixth Sense, turn to 123.",
  "targetId": "123",
  "requirements": ["skill:Sixth Sense"],
  "effects": [],
  "randomOutcomes": [],
  "ruleIds": []
}
```

---

## Series: Grey Star (Shianti)

### Core stats
- Combat Skill (CS)
- Endurance (END)

### Skill/discipline system
- **Shianti Disciplines** (e.g., Elementalism, Assimilance).
- Requirements map as: `skill:<DisciplineName>`.
- Grants map as: `discipline:<DisciplineName>`.

### Inventory rules
- Backpack slots: 10 (`slot:backpack`)
- Weapon slot: 1 (`slot:weapon`)
- Special items are unlimited (category `special`).
- Money: `counter:gold` or `counter:crystal-shards` (book-dependent)
- Rations: `counter:rations`

### Combat resolution & modifiers
- Base combat ratio = (Player CS + combat modifier) − Enemy CS.
- Magic items/spells add `combat:<Delta>` while active.
- Temporary END boosts use `endurance:heal:<Amount>` (or `stat:Endurance:<Delta>` if it changes max END in the narrative).

### Canonical requirements/effects mapping
- Requirement: knows `Elementalism` → `skill:Elementalism`.
- Requirement: at least 3 rations → `counter:rations:3`.
- Effect: spend 2 crystal shards → `counter:crystal-shards:-2`.
- Effect: gain 1 END → `endurance:heal:1`.

### Example RuleDefinition entries (Grey Star)
```json
{
  "rules": [
    {
      "id": "gs.elementalism.fire-combat",
      "requirements": ["skill:Elementalism"],
      "effects": ["combat:+1"]
    },
    {
      "id": "gs.rations.consume",
      "requirements": ["counter:rations:1"],
      "effects": ["counter:rations:-1", "endurance:heal:1"]
    }
  ]
}
```

### Example Choice metadata (Grey Star)
```json
{
  "text": "If you possess the Discipline of Assimilance, you may enter.",
  "targetId": "045",
  "requirements": ["skill:Assimilance"],
  "effects": [],
  "randomOutcomes": [],
  "ruleIds": []
}
```

---

## Series: Freeway Warrior

### Core stats
- Combat Skill (CS)
- Endurance (END)

### Skill/discipline system
- Freeway Warrior uses **skills/abilities** (e.g., Driver, Gunner, Mechanic) instead of disciplines.
- Requirements map as: `skill:<SkillName>` (stored in `Character.CoreSkills`).
- Grants map as: `discipline:<SkillName>` (same storage as disciplines for now), or use `stat:<SkillName>:+1` if skill levels are numeric.

### Inventory rules
- Vehicle cargo slots: 12 (`slot:cargo`)
- Weapon slots: 2 (`slot:weapon`)
- Money: `counter:credits`
- Ammunition: `counter:ammunition`
- Fuel: `counter:fuel`
- Grenades: `counter:grenades`
- Medkits: `counter:medkits`
- Armor: `counter:armor`

### Combat resolution & modifiers
- Base combat ratio = (Player CS + combat modifier) − Enemy CS.
- Vehicle armor/weapon upgrades are modeled as `stat:CombatSkill:+N` for permanent bonuses,
  or `combat:+N` for temporary encounter modifiers.
- Vehicle damage reduces `counter:armor` and/or `endurance:damage:<N>` depending on book rule.

### Canonical requirements/effects mapping
- Requirement: ammo at least 5 → `counter:ammunition:5`.
- Requirement: cargo slots >= 1 → `slot:cargo:1`.
- Effect: spend 1 fuel → `counter:fuel:-1`.
- Effect: take 2 damage → `endurance:damage:2`.
- Effect: gain weapon `Shotgun` → `item:add:Shotgun:weapon` + `slot:weapon:+1`.

### Example RuleDefinition entries (Freeway Warrior)
```json
{
  "rules": [
    {
      "id": "fw.gunner.bonus",
      "requirements": ["skill:Gunner"],
      "effects": ["combat:+1"]
    },
    {
      "id": "fw.ammo.spend",
      "requirements": ["counter:ammunition:1"],
      "effects": ["counter:ammunition:-1"]
    }
  ]
}
```

### Example Choice metadata (Freeway Warrior)
```json
{
  "text": "If you have at least 5 rounds of ammunition, you may fire.",
  "targetId": "210",
  "requirements": ["counter:ammunition:5"],
  "effects": ["counter:ammunition:-1"],
  "randomOutcomes": [],
  "ruleIds": []
}
```

---

## Implementation Guidance (Follow-up Updates)
- **`RequirementParser`**
  - Ensure `skill`, `item`, `stat`, `flag`, `counter`, `combat`, and `slot` keys remain canonical.
  - Keep the `>=` and `:` forms for thresholds.
- **`EffectParser`**
  - Ensure `stat`, `combat`, `endurance`, `item`, `flag`, `discipline`, `counter`, and `slot` keys remain canonical.
  - Support absolute (`counter:gold:3`) vs. delta (`counter:gold:+3`) updates.
- **Per-series rule catalogs**
  - Populate `RulesCatalog.<Series>.json` with the `RuleDefinition` entries described above.
  - Keep series-specific counters and slots documented here as the source of truth.
