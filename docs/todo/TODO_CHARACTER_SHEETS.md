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
- [x] ~~A sheet with no seed entry defines an offstage character~~ — **reversed 2026-08-06**,
      it must be placed in the seed. See §9.1 and "Amendments" below
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
- [x] ~~A sheet with no seed entry becomes an offstage character~~ — to be replaced, below
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

## Amendments — built 2026-08-06

Design: §9 of [`CHARACTER_SHEETS.md`](../design/CHARACTER_SHEETS.md). Both are load-time
refusals in `WorldPack`, alongside `RequirePlayer` and `RejectUnresolvedReferences`.
Devlog: [`2026-08-06_placement-and-ids.md`](../devlog/2026-08-06_placement-and-ids.md).

### 9.1 A sheet must be placed in the seed

- [x] `WorldPack.ApplySheets` refuses a sheet whose id has no `seed.json` entry, naming the
      file and saying to add them at a location
- [x] `RequireEveryoneIsPlaced` — **broader than the design said**, and better: *every*
      seeded character needs a location, not only those with sheets. A `locationId: null`
      entry is unreachable for the same reasons and authored by the same person
- [x] `/character` left alone. Blank-means-offstage stays for characters invented in play
- [x] `worlds/marrow` still loads — 3 seated, 2 with sheets, 3 lore
- [x] Self-tests: sheet with no seed entry refused; seeded character with no location
      refused; a seeded character with **no sheet** still loads untouched

### 9.2 Ids are kebab-case, enforced

- [x] `EntityId` in Storage — lowercase letters, digits, single hyphens, no leading or
      trailing hyphen, no doubled hyphen. Hand-written rather than a regex
- [x] Applied to sheet filenames, lore filenames, and character/location/item/fact keys in
      `seed.json`. Checked before anything reads them, so a malformed id is reported as
      itself rather than as the dangling reference it causes
- [x] Self-tests: the accept/refuse table, plus a pack whose sheet filename has an
      underscore — the way this mistake actually arrives

### 9.3 A player sheet replaces character creation

- [x] `WorldPack.AuthorsThePlayer` — true when the pack ships `characters/player.md`
- [x] `PlaySession` skips the opening prompts when it is set, and says who you are instead,
      pointing at `/rename` so an authored protagonist does not read as a locked one
- [x] ~~`worlds/marrow` deliberately ships **no** `player.md`~~ — **changed 2026-08-12**, it
      now ships one. The first session played against sheets made the case: an authored
      protagonist with a companion who names them is what `{{player}}` was built for. The
      branch stays covered by the self-test, which loads the same pack both ways; what is lost
      is a *shipped world* exercising the opening prompts by hand
- [x] A second pack restores that manual coverage — `worlds/ashfall`, built the same day, and
      the blank-slate shape now lives there. Confirmed in play: `{{player}}` rendered as
      "Rook", a name no file knew
- [x] Self-test covers **both** branches. The interesting failure is the one that still looks
      like it works, and checking only the sheet branch would pass while the blank-slate path
      silently stopped asking anyone their name

### Found while building

- [x] **A check on shipped content needs a check against shipped content.** Every other
      self-test builds a pack designed to fail. Tightening a load rule can break the world in
      the next folder over without one of them noticing, so `CheckShippedPackLoads` loads the
      real `worlds/marrow` — skipping, not failing, when run from elsewhere

### Left open on purpose

- [x] **Should extraction-proposed ids be held to the same shape? Measured 2026-08-12: no.**
      Every id reference in every save swept — **1528 of them, zero malformed.** The model
      produces kebab-case without being told to, which the schema's own examples
      (`cellar-poisoning`, `militia-woman`) are quietly doing the work for.

      Adding a validator check would buy nothing and cost a rejection cascade: a refused
      `character_introduced` takes every delta referencing it down too. **A guard against a
      failure that has never occurred, whose failure mode is worse than the thing it guards
      against.** If a future model starts emitting `Bloated_Man`, normalisation across the
      whole batch is the fix, not rejection — but that is for the day it happens

## Measure first, per the pattern

- [x] **Does the narrator actually use sheet detail?** The point of prose over fields is
      expressiveness; if the body is ignored in favour of the one-line description, the design
      is wrong. Needs a narration-side check, which does not exist — see `TODO_NARRATION_EVAL.md` — **moved to TODO_FUTURE_WORK 2026-08-13.** [Phase 1 — narration eval]
- [x] **Does an id ever reach the prose through `{{ }}`?** The failure that forced the
      `ForNarration` / `ForExtraction` split. Validation should make it impossible; verify
      rather than assume — **moved to TODO_FUTURE_WORK "Pending a session" 2026-08-13.**
- [x] Context size with a full cast of sheets — the third contributor to the budgeting problem
      after lore and loose items, and still unmeasured — **moved to TODO_FUTURE_WORK 2026-08-13.**

## Verify

- [x] `dotnet build` clean, self-tests for the merge and for `{{ }}` validation — **done 2026-08-06**, shipped clean.
- [x] Full scored sweep, **provider pinned** — **done**, run many times since.
- [x] A play session with sheets authored for Hald, Mabb and the player — **done**, marrow ships sheets and has been played.

## Explicitly out of scope

- **Stats and numbers.** "Quick with a knife" is prose; anything the engine reasons about is the
  dice-resolved-checks design
- **Extracted relationship change.** `relationship_changed` has fired **once in 253 turns
  across five sessions**, re-checked 2026-08-12 on a healthy provider. The capability is fine
  — the `hostility` scenario scores 10/10 — but the trigger does not occur, because standing
  accumulates and a per-turn extractor sees one turn. Making it move is the reconciliation-pass
  problem
- **Budgeting or selective loading.** Send whole sheets for present characters and measure
  before introducing a way to silently omit one
