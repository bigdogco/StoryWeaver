# narration.md companion clarification — the untestable half of the fix

Completes the companion-follow fix. The extraction rule (committed 2ef85ff) makes canon record a
companion who followed; this makes the narrator's side explicit so the two agree.

Added one bullet under **World consistency** in `prompts/narration.md`: the people shown as
present are who is in the scene; do not give lines or actions to someone the state places
elsewhere; the exception is travel — when the player moves, a companion at their side may go with
them and is then present where they arrive; anyone placed in a scene is taken to be really there.

This keeps the narrator from staging absent characters (the same discipline the extraction guards
`player-absent-character` and `narrator-mention` enforce) while explicitly permitting the
companion-follow the extractor now reconciles.

**Not eval-measurable** — the extraction eval uses fixed, hand-written narration and never calls
the narrator — so this ships on judgement rather than a number. The real test is a live session:
whether narrator and extractor together keep a companion's location straight across moves. Left
open in TODO_COMPANION_FOLLOW_ as a second-live-sighting confirmation. No code changed.
