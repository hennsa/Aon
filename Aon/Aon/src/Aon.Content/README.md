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
