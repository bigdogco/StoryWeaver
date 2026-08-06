# TODO — character sheets

Authored identity for characters, and the general form of the player sheet asked for in play.

Design: [`docs/design/CHARACTER_SHEETS.md`](../design/CHARACTER_SHEETS.md) — **all six decisions
settled 2026-08-04, ready to build.**
Supersedes [`TODO_PLAYER_SHEET.md`](TODO_PLAYER_SHEET.md), whose protection work is done.

---

## Settled

- [x] Sheet defines the character; `seed.json` holds starting state only
- [x] Prose body, frontmatter only for what code reads
- [x] Attitudes toward groups *and* toward anyone with a sheet, including the player
- [x] The sheet holds the permanent *why*; canon holds the moving *standing*
- [x] Parser extended by exactly one nesting level, same strictness
- [x] A sheet with no seed entry defines an offstage character
- [x] `{{player}}` and `{{entity-id}}`, resolved at assembly, validated at load
- [x] `{{player}}` resolves to the name
- [x] Character creation is a required step at world start

## Build — done 2026-08-06

See [`2026-08-06_character-sheets.md`](../devlog/2026-08-06_character-sheets.md).

- [x] `CharacterSheet` in Core — id, name, body, attitudes
- [x] Shared `Frontmatter` reader with exactly one nesting level, same strictness; both
      readers moved onto it, plus a shared `MarkdownFile` for heading-and-body
- [x] `WorldPack` loads `characters/*.md`
- [x] Load merge: sheet for identity, seed entry for state; a seeded character with no sheet
      is untouched
- [x] A sheet with no seed entry becomes an offstage character
- [x] `{{ }}` resolution in `ContextAssembler`, for sheets and lore bodies alike
- [x] `{{ }}` validation at pack load — unresolvable ids and attitude targets fail by file
      and name
- [x] `ContextAssembler` renders the sheet body and attitudes under each present character
- [x] Character creation at world start: name required, description optional
- [x] Sheets for Hald and Mabb

## Found while building

- [x] **Authored headings collided with the document's own.** A sheet's `## Manner` landed at
      the same level as `## Present`, so a character's sections read as top-level sections of
      the prompt. Authored bodies are now pushed to `####` — an author should not have to know
      what depth their prose is rendered at
- [x] **"Curious about You"** — the predicted consequence of the seed's default player name,
      fixed by character creation rather than a better default

## Measure first, per the pattern

- [ ] **Does the narrator actually use sheet detail?** The point of prose over fields is
      expressiveness; if the body is ignored in favour of the one-line description, the design
      is wrong. Needs a narration-side check, which does not exist — see `TODO_NARRATION_EVAL.md`
- [ ] **Does an id ever reach the prose through `{{ }}`?** The failure that forced the
      `ForNarration` / `ForExtraction` split. Validation should make it impossible; verify
      rather than assume
- [ ] Context size with a full cast of sheets — the third contributor to the budgeting problem
      after lore and loose items, and still unmeasured

## Verify

- [ ] `dotnet build` clean, self-tests for the merge and for `{{ }}` validation
- [ ] Full scored sweep, **provider pinned**
- [ ] A play session with sheets authored for Hald, Mabb and the player

## Explicitly out of scope

- **Stats and numbers.** "Quick with a knife" is prose; anything the engine reasons about is the
  dice-resolved-checks design
- **Extracted relationship change.** `relationship_changed` has fired zero times in 102 turns;
  making standing move is the reconciliation-pass problem
- **Budgeting or selective loading.** Send whole sheets for present characters and measure
  before introducing a way to silently omit one
