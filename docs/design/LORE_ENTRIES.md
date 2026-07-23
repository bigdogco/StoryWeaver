# Design — lore entries

**Status:** design settled 2026-07-23, no code yet. Written after the bootstrap closed.
All open decisions resolved — see §7. Ready to build.

The fourth entity type: a named topic with a body of prose, the way a DnD lorebook entry
works. An organisation, a religion, a war, a bloodline, a prophecy.

This document exists because **the retrieval decision is hard to reverse.** Once entries are
in save files with a particular shape, changing how they are keyed and injected means
migrating worlds. Everything else here could be built and revised freely; that one part
cannot, so it gets decided first.

---

## 1. Settled already

Carried forward from `TODO_FUTURE_WORK.md`, not reopened here:

- **A lore entry is not a `Fact`.** A fact is one nameless proposition that is true or not.
  Lore is a named topic with a body. The "no names" rule on facts exists to stop the
  extraction model inventing titles for statements — it does not apply here, because **lore is
  authored, never extracted**, so a human writes the title.
- **Per-character knowledge.** One character has heard of the King's Investigators and another
  has not, and an NPC cannot reference an order they do not know. This is the mechanic that
  separates the system from a chat log.
- **Lore is only for things with no entity representation.** Once something is a real
  `Character` or `Location`, that entity is authoritative. Otherwise a lore entry about Hald
  and the `Character` Hald drift apart — the exact incoherence canon exists to prevent.

---

## 2. The shape

```
LoreEntry
  Id           string     globally unique, like every other id
  Title        string     "The King's Investigators"
  Body         string     a paragraph or several — the reference text
  Keys         string[]   trigger words: "investigator", "king's men", "the order"
  Always       bool       inject regardless of keys (world premise, tone)
  Priority     int        what survives when the budget is tight
```

**What is deliberately absent:**

- No `Knows` on the entry. Knowledge lives on the character, as it already does for facts —
  one direction only, or the two representations disagree.
- No standing toward the player. That was the first shape considered and it was wrong: an
  organisation is reference material, not a simulated actor. If a faction genuinely needs to
  act, it needs `Character`s who belong to it.
- No `EstablishedTurn`. Lore predates the story.

---

## 3. The two decisions that matter

### 3.1 Where entries live: content, not state

**Proposal: lore entries live in their own authored file, separate from `canon.json`.**

The split is clean and it falls out of what each thing *is*:

| | lives in | why |
|---|---|---|
| the entries | `lore.json` (or a folder of markdown) | **content** — authored, mostly static, edited by hand |
| who knows them | `canon.json`, on `Character.Knows` | **state** — changes every session, extracted from play |

This is the same instinct as the already-logged "prompts as editable files, hot-reloadable"
item, and for the same reason: an author wants to edit lore in a text editor between
sessions, without a save-file round trip and without the risk of a hand-edit corrupting play
state. It also means lore can be shipped, shared, or version-controlled independently of
anyone's playthrough — which is exactly how the character-card ecosystem distributes worlds,
and the one thing they are unambiguously good at.

**Consequence to accept:** a save can reference a lore id that the current lore file no longer
defines. That must degrade quietly — a character knowing a since-deleted entry is not a
corruption, it is a dangling reference to be dropped on load with a warning. This is the
opposite of the rule for facts, and it is right, because content and state have different
lifetimes.

### 3.2 Knowledge: one namespace, and learning comes for free

**Proposal: `Character.Knows` holds both fact ids and lore ids, unchanged.**

Ids are already globally unique across characters, locations and facts — `DeltaValidator.Taken`
enforces it, after extraction once emitted a `location_introduced` reusing a character's id.
Extending that to lore costs one line.

The payoff is large and not obvious: **learning lore in play needs no new delta kind.** When
Hald explains the Investigators, the extractor emits `fact_learned` with the lore entry's id,
and it already does this well — `fact_learned` fired 82 times across the 51-turn session and
is the most reliable delta in the set. A separate `lore_learned` would mean a new schema
branch, a new prompt rule, and a new thing to measure, to express something the existing delta
already says.

**The rule that must come with it:** `fact_established` may not use an id owned by lore.
Lore is authored; the extractor may record that someone *learned* an entry, never that one
came into existence. That is a validator rule, and it is the boundary that keeps "authored,
never extracted" true in practice rather than only in intent.

**Open risk to measure, not assume:** the extractor is told facts are single propositions. Given
a lore id in the known-ids roster, does it emit `fact_learned` against it naturally? Unknown.
Worth an eval scenario before committing — the cost of being wrong is a new delta kind, which
is recoverable, so this does not block the design.

---

## 4. Retrieval

The part that is hard to reverse.

### 4.1 Why it is forced

`ContextAssembler` dumps the entire world into every prompt. That is fine at two locations and
three characters, and hopeless at forty lore entries. **Adding lore is the point at which
keyword-triggered injection stops being optional.**

### 4.2 What to match against

Proposal, in order of confidence:

1. **`Always` entries, unconditionally.** The world premise, the tone, the one paragraph that
   should never fall out. Equivalent to a lorebook "constant" entry.
2. **Keys matched against the current player input and the most recent narration.** Both,
   because a topic the narrator just raised is as live as one the player typed.
3. **Entries tied to entities present in the scene.** Undecided whether this is worth the
   complexity — it means a link from a `Character` or `Location` to lore ids, which is a
   second edge type. Deferred; keys can express it well enough at first.

### 4.3 Where it goes in the prompt — settled by existing architecture

There is an entry in `CHALLENGES.md` warning that injecting lore mid-prompt rewrites the
prefix and destroys caching for everything below it.

**That problem is already solved and lore does not reopen it.** Narration keeps volatile world
state in the *last* message specifically so the system prompt and replayed history stay a
stable, cacheable prefix. Lore joins that same volatile block. Nothing above it moves.

This is worth stating loudly because it is the single most common way a lorebook implementation
becomes expensive, and the architecture avoided it by accident of an earlier decision.

### 4.4 Budget and what gets cut

- A token budget for the lore block, configurable per role.
- Fill by `Priority`, then by match strength, then stop.
- **Report every cut.** `CHALLENGES.md` calls silent drops "the single biggest source of *why
  did the AI forget the Duke exists?*", and `TODO_FUTURE_WORK.md` already lists surfacing this
  as a player-facing differentiator. Almost nobody does it. It should be built in the same pass
  as the budgeting, not after — a budget without reporting is the bug.

### 4.5 What the extractor sees

**Proposal: the extractor gets lore ids and titles, never bodies.**

It needs the ids to emit `fact_learned` against them. It does not need the prose, and giving it
several paragraphs of reference material invites exactly the invention the extraction prompt
spends most of its length suppressing. The narrator gets the bodies; the bookkeeper gets the
index.

This mirrors the existing `ForNarration` / `ForExtraction` split, which was introduced for a
different reason and keeps paying out.

---

## 5. Authoring

- `/lore` in the console harness, matching `/place`, `/character`, `/fact`, `/rename`.
- Editing the file by hand is the expected path for real authoring; `/lore` is for capture
  mid-session.
- Hot reload is desirable and already logged for prompts. Same mechanism if it exists by then.

---

## 6. Risks

- **Scope.** This is the largest item on the list, because it is really two: an entity type and
  a retrieval layer. If it needs cutting, the entity plus always-on injection is useful alone,
  and keyed retrieval can follow once a world is big enough to prove the need.
- **The fact store is still the dumping ground.** §9 found the model routing three unrelated
  gaps through `fact_established`. Adding a fourth entity type does not fix that pressure and
  could give the overflow somewhere new to pool. The `fact_established`-may-not-target-lore
  rule is the guard, and it should be in from the first commit.
- **Extraction behaviour against lore ids is unmeasured.** See §3.2.
- **Two sources of truth for "what is in the world."** A world is now `canon.json` plus
  `lore.json`, and the load path has to be explicit about which wins where. The rule is simple —
  entities are canon, reference is lore, lore never describes an entity — but it is a rule
  someone can break by writing a lore entry about Hald.

---

## 7. Decisions — settled 2026-07-23

All four resolved as proposed.

1. **Entries live in their own authored files**, separate from `canon.json`. Knowledge of them
   stays in canon. Content and state have different lifetimes; a save referencing a deleted
   entry drops it with a warning rather than being treated as corruption.
2. **One `Knows` namespace.** Fact ids and lore ids together, so learning lore in play needs no
   new delta kind — the extractor emits the existing `fact_learned` against a lore id. Comes
   with the validator rule that `fact_established` may not target a lore id.
3. **Always-on injection first.** Ship the entity, authoring and per-character knowledge with
   every entry injected into the volatile block. Keyed retrieval and budgeting follow once a
   real world is large enough to prove the need — designing a budget against a guess about
   world size is how the last two false conclusions started.
4. **Markdown, one file per entry, filename is the id.**

### 7.1 The file format

```markdown
---
keys: investigator, king's men, the order
always: false
priority: 10
---

# The King's Investigators

An order answering directly to the crown, empowered to enter any holding and
question any subject. They wear no uniform and carry a seal rather than a
warrant. In the marsh towns they are half-rumour.
```

Saved as `lore/kings-investigators.md` → id `kings-investigators`.

- **The id is the filename.** The filesystem enforces uniqueness for free and there is no
  `id:` field that can drift from anything. Collision with a character, location or fact id is
  still checked on load — the same global-uniqueness rule as `DeltaValidator.Taken`.
- **The title is the first `#` heading.** Missing it is an error, not a default.
- **Frontmatter carries only what prose cannot**: `keys`, `always`, `priority`, all optional.
- **The body is everything after the heading**, and is the only part the narrator receives.
- **Parse failures are loud.** An unparseable file names itself and its line and does not
  silently vanish. Same principle as rejected deltas and budget-cut entries: a silent drop is
  the worst failure mode in this genre and we have written it up twice.

No YAML dependency. Four scalar fields and one comma-separated list is a ~40-line strict
parser that refuses what it does not understand rather than guessing. A YAML library would buy
nested structures the design deliberately does not have.

### 7.2 Note for whoever builds the Lore Writer

A UI editor is the expected authoring surface eventually, which makes markdown a *safer*
choice rather than a riskier one: the tool writes the file, and the format stays readable for
diffing, sharing and hand-inspection. It also removes the one real hazard of markdown bodies —
a constrained body field means headings and bullet lists never reach the narrator, where they
would leak structure into prose in the same way an id once did.

**The constraint that comes with it: if the UI both reads and writes these files, the format
must round-trip losslessly.** A strict parser that rejects unknown keys and a human who adds
one are a bad combination. Once the window exists, it should be the only writer.
