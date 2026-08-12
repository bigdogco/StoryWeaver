# 2026-08-12 — the sheet owns the name

Making a week-old design decision true, and checking the second pack that found it false.

TODO: [`TODO_SECOND_PACK.md`](../todo/TODO_SECOND_PACK.md),
[`TODO_ITEM_PLACEMENT.md`](../todo/TODO_ITEM_PLACEMENT.md).

---

## The `ashfall` session first

Fifty turns, and it validated the thing the pack was built for.

**`{{player}}` resolved to a name no file knew.** From the prompt log:

```
does not trust Rook an inch yet
```

Behn's sheet says `does not trust {{player}} an inch yet`. "Rook" was typed at the opening
prompt thirty seconds earlier. Zero unrendered `{{player}}` anywhere in the log. The mechanism
was justified by *a pack author cannot know the player's name*, and until now every use of it
had resolved to a name a pack author had in fact written.

**Cleanest extraction yet: 1 rejection in 132 applied deltas** (Marrow was 7 in 136).
`Location.Status` on 7 of 9 locations. Six facts in fifty turns, all durable, no duplicates.

**And the new provider field earned itself immediately:**

```
StreamLake 42    SiliconFlow 7    Venice 1
```

Three upstreams in one session. Every earlier session had this same spread and no record of it.

## What the session found: status is absorbing placement

Three `item_status_changed` deltas encoding *where a thing ended up*, each leaving the item held
by a person while its status says otherwise:

```
t14  clay-cup-ale      = "shattered on the floor"                     still held by Behn
t29  old-mining-cable  = "severed and dropped into the shaft"         still held by Rook
t44  severed-chain     = "wrapped and knotted around the sluice gate" still held by Rook
```

The single rejection is the same gap from the other side: `item_moved loose-rock → null/null`,
because the rock "dropped into the dark" and the schema requires *somewhere*.

**Third sighting of one pattern:** when the schema has no slot for something the prose keeps
saying, the model puts it in the nearest slot that accepts it, and the validator cannot tell —
the delta is well-formed and the field is free text. Facts absorbed location state; status is
now absorbing placement.

Logged, not fixed. It needs a reproduction first, and the rejection count says nothing about
this class — **it was found by reading canon, not by reading rejections.**

## The `Name` fix

Yesterday's second pack exposed that decision 1 — *`seed.json` drops `name` for any character
with a sheet; nothing is written twice, so nothing can disagree* — had never been true.
`Entity.Name` was `required`, so a seed omitting a name was refused by the deserializer.

`Name` is no longer `required`. Two load-time checks replace it:

- **`RequireSheetsOwnTheirNames`** — a seed naming a character who has a sheet is refused,
  before the merge, because afterwards there is no way to tell who supplied what
- **`RequireEverythingIsNamed`** — anything still nameless after the merge is refused

**Strictly stronger than what was given up.** `required` only checked that the property was
*present*: `"name": ""` satisfied it and produced a nameless character. That is now a load
error. Every entity construction site in `src/` was checked to still set `Name`, since the
compiler no longer insists.

Six duplicated names came out of the two packs.

## What fixing it exposed, which is the better lesson

`CheckPlayerSheetCannotDeclareAttitudes` started failing — for **duplication**, thrown before
the pack ever reached the attitude it was testing.

The test asserts only *"this throws `InvalidDataException`"*. Had the new rule fired one line
later, the test would have stayed green while testing nothing at all.

**A test that asserts only that something throws passes on the wrong exception**, and adding a
rule upstream of one is exactly how that happens. Two fixtures were corrected to fail for their
own reasons again.

That is the same shape as the pre-flight script from yesterday — a check that confirms what you
already believe rather than what is true — arriving from a third direction in two days.

## Verified

- `dotnet build` clean, `--selftest` all four suites pass
- `worlds/ashfall` 3 seated / 2 sheets / 3 lore / blank slate; `worlds/marrow` 4 / 4 / 3 /
  authored player — both with names now stated once
- New: a sheet can be the only place a character is named; a seed naming a sheeted character
  is refused; a blank name is refused
