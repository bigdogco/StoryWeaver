# 2026-08-12 — a second world, and what it caught

`worlds/ashfall`, plus `--pack` to reach it. Written to restore the blank-slate coverage Marrow
gave up this morning, and to test something Marrow structurally cannot: whether a pack is
portable.

TODO: [`TODO_SECOND_PACK.md`](../todo/TODO_SECOND_PACK.md).

---

## The pack

A waystation on a volcanic pass, the road closed at the Teeth, travellers stuck inside under
falling ash. Deliberately unlike Marrow — no marsh, no cult, no well, no shared id prefix — and
**the player arrives as a stranger**, which is what makes a blank slate coherent rather than
merely an omission.

Three seated characters, two sheets, three lore entries (one `common: true`), one held item,
and **no `player.md`**.

| | Marrow | Ashfall |
|---|---|---|
| character-creation prompts | skipped | **runs** |
| `{{player}}` resolves to | a name the author wrote | **a name typed at the prompt** |

That second row is the whole point. `{{player}}` was justified by *a pack author cannot know
the player's name*, and until now every use of it resolved to a name a pack author had in fact
written. Behn's sheet reads `does not trust {{player}} an inch yet`, and there is no file
anywhere that knows what that will say.

## Reaching it

`--pack <id>`, defaulting to `marrow`. `--save <id>` defaults to the pack id, because two packs
sharing one save is not a configuration but a corruption — the character and location ids
written by one world do not exist in the other.

`PackId` and `SaveId` were `const`, read at fourteen sites. They are now static fields set once
at startup. That is worth a raised eyebrow and is written up in the code: the alternative was
threading two strings through six signatures for no behavioural gain, in a harness that runs
one session per process. A UI would pass them and the fields would go.

One trap avoided by knowing the file: `Program` picks the settings path as the first argument
not preceded by a value-taking flag, so `--pack ashfall` would have been read as a settings
file. `--pack` and `--save` had to join `valueFlags`.

## What the pack caught on its first load

**Design decision 1 has never been true.**

The character-sheets design says `seed.json` drops `name` and `description` for any character
with a sheet — *"nothing is written twice, so nothing can disagree."* `Entity.Name` is
`required`. A seed omitting a name is refused by the deserializer, and always has been.

Marrow carries names for Hald and Mabb. That read as harmless duplication for a week. It was
the file format quietly complying with the type while the design document said the opposite.

Behaviour is not wrong: `ApplySheets` overwrites, so the sheet wins deterministically. What is
wrong is that two files can state a name and only one is read. **Rename someone in their sheet
and the seed keeps the old name, with no error** — the precise failure decision 1 exists to
prevent, sitting inside the decision meant to prevent it.

Not fixed here. The fix is to make `Name` optional on the seed path and then refuse, loudly, a
seed that names a character who has a sheet — turning duplication into a load error rather than
a trap. That touches a Core type and wants its own decision.

## The methodological bit, which is the real find

Before writing any of it I pre-flighted the pack with a throwaway script: kebab ids, every
character placed, attitude targets resolving, `{{ }}` references resolving. **It passed.**

The real loader failed it in one line.

The script reimplemented the load rules *from my understanding of them*, so it could only ever
confirm that understanding. **A check written from the same understanding as the thing it
checks cannot find a misunderstanding.**

This is the same lesson as `CheckShippedPackLoads` — added yesterday because every other
self-test builds a pack designed to fail, and none of them would notice a tightened rule
breaking a real world. Both say: run the real thing against real content.

`CheckShippedPackLoads` now iterates every folder in `worlds/` rather than opening a named one,
and reports whether each pack authors its player. A second pack is worthless as a regression
guard if the check only ever opens the first.

## Verified

- `dotnet build` clean, `--selftest` all four suites pass
- `worlds/ashfall` — 3 seated, 2 with sheets, 3 lore, blank slate
- `worlds/marrow` — 4 seated, 4 with sheets, 3 lore, authored player

## Not verified

Nobody has played it. The moment worth watching is the first turn: the opening prompts running
for the first time since Marrow stopped covering them, and `{{player}}` resolving to a name
that exists in no file.
