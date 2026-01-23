# Aon.Content

## Book JSON Schema (v0)

This folder contains the initial JSON Schema for book exports produced by `Aon.Tools.BookImporter`.

**Schema file**
- `Book.schema.json`

### Notes
- This schema mirrors the current importer output: `id`, `title`, `frontMatter`, and `sections`.
- Sections include `blocks` (normalized text grouped by kind) and `choices` for section links.
- `frontMatter` remains raw HTML for now; it can be refined later into structured blocks.
- Use this schema to validate importer outputs before loading them into the application layer.

### Rule metadata fields
Choices can optionally include rule metadata to drive downstream logic:
- `requirements`: array of strings describing prerequisites (e.g., disciplines, items, or flags).
- `effects`: array of strings describing state changes (e.g., "gain: gold+3").
- `ruleIds`: array of string identifiers that map to structured rules elsewhere.
- `randomOutcomes`: array of objects describing random number table routing, each with:
  - `min`/`max`: inclusive integer bounds (0–9).
  - `targetId`: section id that should be reached for the outcome.
  - `effects`: optional array of rule effect strings to apply for the outcome.

Outcomes may specify a `targetId`, `effects`, or both, enabling roll-based branching to a new section or a direct effect application.

### Supported requirement/effect tokens
Rule metadata strings are parsed by `Aon.Rules`. Tokens follow the pattern `key:value` and are case-insensitive for the key.

**Requirement tokens**
- `skill:<name>`: requires a named discipline or series skill.
- `item:<name>`: requires an inventory item (by name).
- `stat:<name>:<min>` or `stat:<name>>=<min>`: requires a stat/attribute value at or above `min`.
- `flag:<name>` or `flag:<name>:<value>`: requires a flag to exist, optionally matching a value.
- `counter:<name>:<min>` or `counter:<name>>=<min>`: requires a counter to meet a minimum value.
- `combat:>=<min>` or `combat:<min>`: requires a combat modifier bonus at or above `min`.
- `slot:<slotName>:<min>` or `slot:<slotName>>=<min>`: requires a tracked slot count (e.g., weapon/armor).

**Effect tokens**
- `stat:<name>:<delta>`: add or remove a stat/attribute value.
- `combat:<delta>`: add or remove combat modifier bonus (e.g., weapon bonuses).
- `endurance:damage:<amount>`: deal endurance damage.
- `endurance:heal:<amount>`: restore endurance.
- `item:add:<name>:<category>` or `item:remove:<name>`: add/remove inventory items.
- `flag:<name>` or `flag:<name>:<value>`: set a flag (defaults to `true`).
- `discipline:<name>`: grant a discipline or series skill.
- `counter:<name>:<value>`: set or adjust a counter (`+/-` values are relative; others are absolute).
- `slot:<slotName>:<value>`: set or adjust a slot counter (`+/-` values are relative; others are absolute).

**Series usage**
- **Lone Wolf (`lw`)**: combat modifiers, endurance damage/heal, weapon/armor slots, plus series counters/flags (e.g., gold, Sommerswerd flags).
- **Grey Star (`gs`)**: combat modifiers, endurance damage/heal, weapon/armor slots, plus series counters/flags (e.g., willpower pools, spell flags).
- **Freeway Warrior (`fw`)**: combat modifiers, endurance damage/heal, weapon/armor slots, plus series counters/flags (e.g., ammo, Medi-kit units, vehicle flags).

Series counters/flags are intentionally free-form; keep names consistent within a series when authoring metadata.

### Choice rule metadata markup (importer input)
When authoring book source HTML for `Aon.Tools.BookImporter`, embed rule metadata on the choice element using data attributes.

**Supported attributes**
- `data-requirements`: delimited list (`|`, `;`, or `,`) of requirement strings.
- `data-effects`: delimited list (`|`, `;`, or `,`) of effect strings.
- `data-rule-ids`: delimited list (`|`, `;`, or `,`) of rule identifiers.
- `data-random-outcomes`: JSON array of random outcome objects (see example below).
- `data-rules`: JSON object with any of the properties above, which merges with the other attributes.

**Example**
```html
<p class="choice"
   data-requirements="discipline:Sixth Sense|item:rope"
   data-effects="gain:gold+3"
   data-rule-ids="lw:charge:rope"
   data-random-outcomes='[
     { "min": 0, "max": 4, "targetId": "120" },
     { "min": 5, "max": 9, "effects": ["gain:gold+1"] }
   ]'>
  Turn to 120.
</p>
```

**Alternate JSON container**
```html
<p class="choice"
   data-rules='{
     "requirements": ["discipline:Tracking"],
     "effects": ["gain:gold+1"],
     "ruleIds": ["lw:reward:tracking"],
     "randomOutcomes": [{ "min": 0, "max": 9, "targetId": "85" }]
   }'>
  Turn to 85.
</p>
```
