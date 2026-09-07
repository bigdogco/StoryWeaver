# Fix the companion-follow omission with an extraction-prompt rule

Iterated the fix against the `companion-follows` diagnostic rather than waiting for a second live
sighting — a scope call by the player. Only `extraction.md` is measurable this way; the eval uses
fixed narration, so the `narration.md` clarification stays a separate, unshipped change.

## The rule

One bullet added to `prompts/extraction.md`, among the movement rules: a character already in the
known ids, present in the scene at the player's side but placed elsewhere by the state, came
along — emit `character_moved` to the player's location. Explicitly **not** for someone merely
spoken about, remembered, or named as being elsewhere — the same mention/arrival line the rest of
the prompt already draws, which is what protects the "don't move absent people" cases.

## Measurement — matched before/after, pinned per provider

| | StreamLake | Baidu |
|---|---|---|
| `companion-follows` before | Mona missed 7/7 | Mona missed ~2/6 |
| `companion-follows` after  | clean 7/7        | clean 6/6 |
| scored-set regressions     | none             | none |
| `player-absent-character` (Tomas) forbidden | 0 | 0 |
| `narrator-mention` (Ilse) forbidden | 0 | 0 |

The scored set (deflection, revelation, movement, hostility, new-character, redescription,
atmosphere, player-arrival, two-stage-entry, name-reveal) stayed 100% / forbidden 0 on both
providers; `two-stage-entry`'s known cistern miss is unrelated variance.

**The bug is provider-dependent** — StreamLake fails it hard, Baidu mostly gets it right unaided.
That is the point rather than a footnote: the play path routes across both, and the rule lifts the
weak provider to clean without denting the strong one or the absent-person guards. DeepInfra does
not serve `deepseek-v3.2` (404); Baidu was rate-limited (429s), so its n is smaller — but forbidden
stayed 0 on every successful run.

No engine code changed. `narration.md` clarification and a second live sighting remain open in
TODO_COMPANION_FOLLOW_.
