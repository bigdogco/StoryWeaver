# 2026-08-12 — write down who served the turn

Small change, and the direct consequence of the day: a measurement without a provider name
attached is not a measurement, and neither is a save.

TODO: [`TODO_PLAY_51_FIXES.md`](../todo/TODO_PLAY_51_FIXES.md).
Why: the sixth provider sighting in [`CHALLENGES.md`](../CHALLENGES.md).

---

## What it fixes

Today produced two statements that were both true and could not be reconciled: *"`movement` is
broken"* and *"`movement` was always fine"*. The reason neither could be checked is that
nothing — not the eval's recorded baselines, not a single saved turn — said which upstream had
served the call.

One model id is routed across independent hosts running their own copies of the same weights,
and they are measurably not equivalent: 0/5 on one and 50/50 clean on another, the same hour,
the same prompt.

So `TurnRecord` now carries `ExtractionProvider`.

Without it, *"canon got worse around turn thirty"* is unanswerable. With it, it can become
*"turn thirty was served by the host that pads `mood_changed` until it runs out of tokens"*.

## What was actually needed

Almost nothing, which is the good part. `ExtractionResult` already carried `Provider` — the
eval has been grouping by it for weeks. It simply was not being kept.

- `TurnRecord.ExtractionProvider`, nullable
- Set at **all three** construction sites: the turn, `/retry`, and `/reroll`. Two out of three
  would have produced a record whose provider silently disagreed with the deltas beside it
- Shown on the turn header — `--- turn 12 · StreamLake ---` — because the question it answers
  is asked while looking at exactly that block, and a field nobody sees is a field nobody uses

## Null is a real answer

Every turn recorded before today has no provider, and will never have one. The field is
nullable and reads back as null, which is not a defect: it says *we do not know who served
this*, which is exactly true and permanently unrecoverable.

The self-test has two halves and **the second is the one that matters** — a turn saved before
the field existed must still load. A save that throws on a bookkeeping field is a playthrough
destroyed by an improvement. It is driven through `JsonWorldRepository` rather than a
serializer directly, so it exercises the options the game actually writes with (`SaveJson.History`)
rather than options that merely resemble them.

## Also done

`providerIgnore` on the extraction role is now `["AtlasCloud", "DeepInfra"]`.

The note beside it records the numbers and ends: *"re-test before removing — this is
degradation, not a permanent property."* That distinction is the point. AtlasCloud is excluded
for a **capability** reason — it returns schema-valid JSON with the wrong delta branch chosen,
which no request parameter can prevent. DeepInfra is excluded for a **health** reason, which
may well not be true next week. An exclusion without its evidence attached becomes folklore,
and folklore never gets removed.

An exclude list rather than a pin, so routing keeps every remaining upstream and one bad host
cannot become a single point of failure.

## Not done, deliberately

**Narration's provider.** It would need `INarrator` to return more than a string, changing a
Core port and every fake behind it, and prose quality has no score to attribute to anyone yet.
Worth doing the day a narration eval exists; pointless before.

## Verified

- `dotnet build` clean, `--selftest` all four suites pass
- New check: the provider survives a real save and load, **and** a turn written before the
  field existed still loads as provider-unknown
