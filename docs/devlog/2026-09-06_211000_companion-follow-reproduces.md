# companion-follows now reproduces the omission

Ran the `companion-follows` diagnostic added earlier today. As first written it scored **14/14
clean, 7/7** on deepseek-v3.2 — measuring a behaviour the uno-spike save never produced, because
the narration plainly walked Mona to the tavern with the player ("falls into step at your
shoulder", "push through the door together").

Reshaped the narration to the subtle shape the save actually produced: the player's own action is
the only movement stated, and Mona is merely *discovered present* in the room they enter (matching
save turn 5, "You push open the door... Mona sits at a corner table"). Nothing else changed —
same seed, same scoring.

**Result: deepseek-v3.2 / StreamLake, n=7 — Mona left behind 6/7** (`required 8/14`, forbidden
0.00; the player's own move lands 7/7). A clean omission: no re-introduction, no workaround, the
companion's `character_moved` simply not emitted.

Recorded the measurement and the prose-shape lesson in CHALLENGES.md and TODO_COMPANION_FOLLOW_.
First half of the PROJECT.md §3 gate (reproduce in a scenario) is met; the prompt change stays
held pending a second live sighting. No engine or prompt code changed.
