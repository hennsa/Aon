# Aon.Content

## Book JSON Schema (v0)

This folder contains the initial JSON Schema for book exports produced by `Aon.Tools.BookImporter`.

**Schema file**
- `Book.schema.json`

### Notes
- This schema mirrors the current importer output: `id`, `title`, `frontMatter`, and `sections`.
- Sections include `blocks` (raw HTML grouped by kind) and `choices` for section links.
- `frontMatter` remains raw HTML for now; it can be refined later into structured blocks.
- Use this schema to validate importer outputs before loading them into the application layer.
