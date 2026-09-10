# Player discovery implementation — 2026-09-10

The player approved the complete discovery design, including best-effort legacy saves,
strict Reroll restrictions and repeated extraction evaluation. No commit or push made.

Core now stores versioned per-aspect memories, reports, aliases, directed routes and
private presentation rules. Detached Player projections expose learned values; Author
projections expose canon. Private moves/removals retain historical memories. Three typed
observation deltas share extraction, with evidence/reference checks, same-turn no-ops,
original-turn retries and author protection. NPC departures can clear actual whereabouts.

Desktop opens each save in Player view. Switching rebuilds navigation and details;
diagnostic details require Author view. The detached knowledge editor supports explicit
field copies, reports, rules and routes. Creation optionally records knowledge in the
same save. Bundled packs have authored starting memories; openings avoid private fallback
descriptions and resolve name references through disclosed labels.

Validation: the complete solution built with zero warnings/errors during implementation.
The latest code also builds in an isolated output directory while live eval processes
hold the normal binaries. No automated UI/unit tests were run. Manual desktop acceptance
is outstanding, with steps in the Desktop README.

## Measurements so far

Pinned model: `deepseek/deepseek-v3.2`, provider `StreamLake`, three runs per case.
Baseline: 10 existing scenarios, 30/30 clean. Initial discovery: 21 scenarios, 90%
required coverage with unsupported disclosures and one parse failure. Initial regression:
97% required coverage with one rename parse failure.

Schema observation fields were separated from existing string fields after conversion
failures; prompt/evidence rules were revised against captured failures. Fixture corrections
are documented in CHALLENGES rather than attributed to model improvement.

Targeted v4: 24 calls, 100% required coverage, 23/24 clean, one raw speaker-detail violation.
The full v5 discovery and regression runs are complete. Their JSON reports retain raw
responses, upstream providers, prompt/schema fingerprints, rejected deltas and no-ops.
Required coverage alone does not establish disclosure correctness; quoted evidence can
still be semantically unrelated to an aspect. Manual prose review remains necessary.

Review fixes after v5 binaries were loaded concern author creation whereabouts, player
opening references, input-error handling and retaining an explicitly cleared last sighting.
The prompt/schema used by v5 remain unchanged during those runs.

Final v5 results: discovery 71/78 clean by the Harness metric, 98% required coverage;
regression 26/30 clean, 93% required coverage. Discovery missed hotel identity twice
and lore-topic learning twice; raw proposals included one unsupported speaker-detail
update and two absent evidence excerpts. Regression missed three final destinations
and established one deflection as a fact. No parse failures occurred in either v5 run.
The clean metric does not require zero rejected deltas; consult rejection reasons too.
These are measured remaining model limitations, not a claim of reliable disclosure.

Final normal-output `dotnet build StoryWeaver.sln -c Release --no-restore -v:q`:
zero warnings and zero errors. `git diff --check` reports no whitespace errors.
Legacy author-save initialization and knowledge-editor failure recovery were also
reviewed and fixed. Manual UI/narrator acceptance and model reliability follow-up are
explicit backlog items. Implementation/evaluation work is complete; no commit made.

## Correction — 2026-09-10

The regression-suite results above (93% required, 26/30 clean) are described as measured
model limitations. That was wrong. The drop was a prompt regression introduced by this
work: the rewritten opening paragraph of `prompts/extraction.md` withdrew the rule making
the player's own movement authoritative while restraining player knowledge claims, and
`player-arrival` began reporting the waypoint instead of the destination. Separating the
two rules restored the suite to 30/30. See
`2026-09-10_144418_player-discovery-prompt-and-fixes.md` for the v6–v8 prompt versions,
the per-version scores and the three code fixes that followed.
