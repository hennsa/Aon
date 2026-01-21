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
