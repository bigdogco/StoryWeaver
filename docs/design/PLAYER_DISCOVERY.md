# Player discovery and private canon

Draft for player review, 2026-09-09. Requested after The Last Lantern exposed
Julian Vale's hidden state in the world panel. This is a proposal, not an approved
schema or implementation. PROJECT.md's locked decisions remain unchanged.

## The observed problem

`WorldPresentation` projects the entire live world: all characters, places, items
and facts, including actual locations, moods, relationships, occupants and knowers.
`Status = hidden` is free text and has no visibility semantics.

The Last Lantern provides two concrete examples:

- Julian is at Hotel Argent, hidden and desperate. The player can inspect those
  truths before finding him.
- Vivian begins in the office holding the hidden black ledger. The item panel
  reveals its holder and contents. `ContextAssembler.AppendCarried` also includes
  it in the narrator's scene context; sharing a room does not establish perception.

The current opening ends before Vivian explains her case, and the seeded player
knows none of its three facts. Starting with a full Julian entry would therefore
invent prior knowledge. His name and the missing-person premise become known when
Vivian explains them, unless an author deliberately changes the opening/backstory.

The proposal has two distinct pieces: **what the player has learned**, persisted
with the playthrough; and **what requires deliberate discovery**, authored as a
rule for presenting the scene. A single visible flag cannot express both.

## Player view and Author view

Add an explicit **Player view / Author view** switch above the world tabs, also
available under View. Open each playthrough in Player view; do not restore an
Author selection from a previous session. Switching views makes no canon write.

Player view is the normal play surface. It lists discovered entries and only the
details learned about them. Entirely undiscovered entries are absent, including
from counts, navigation, filters and empty-state wording. Unknown fields on a known
entry say **Unknown**. There is no placeholder row announcing an undiscovered person.

Author view is labelled **Full canon · contains spoilers** and offers the existing
Add/Edit/Remove actions and complete details. It keeps the player's repair path
available by a deliberate action. Switching to it does not teach the protagonist
anything and does not change narrator context. Switching back replaces all cached
details, tooltips, selection and links with the Player projection.

Both projections come from Core through App. Desktop binds detached display data;
it does not filter a full private object and hope every control remembers the rule.
The same policy applies to location occupants, held items, connections, source names,
picker suggestions, accessible labels and narration-link targets. Player view uses
only learned names/aliases for automatic links. A hidden target must not become
discoverable through a surname, hover, ID, cached Author panel or direct navigation.

Existing full-canon diagnostics (Check Canon, Update State diffs, rejected deltas,
Retry/Reroll details and detailed errors) remain available in Author view. In Player
view show a non-spoiling operation outcome and an explicit **Inspect in Author view**
action. Do not show secret names or counts of hidden entries in a summary. Update
State may still run in Player view; returning its complete report requires Author view.
Already-written prose is never rewritten or censored by switching views.

## What discovery records mean

Add a versioned discovery section to WorldState, saved in canon JSON. Its player
records are keyed by **entity kind and permanent ID**. Each records the latest
learned value for each supported aspect, with its own turn and provenance. Absence
means not learned; it must never fall back to the current canon field.

These are observations of entity state, not a second copy of world truth. They may
be incomplete, stale or mistaken. `Character.Knows` remains a set of fact/lore IDs;
facts retain one authoritative text and their existing attribution semantics. We do
not create a second per-character fact-text store or expand NPC knowledge into this
new observation model in this task. The record format is explicitly player discovery.

| Kind | Player-facing observations | Never copied automatically from canon |
|---|---|---|
| Character | Learned name/alias, safe description, observed condition/demeanour, last seen whereabouts | True identity, complete description, private mood, relationship score/summary, their knowledge or actual current location |
| Location | Learned name, safe description, observed condition, individually discovered outgoing connections | Secret destination names, full connection graph, actual occupant/item lists |
| Item | Learned name, safe description, observed condition, last seen placement/holder | Contents, private properties, actual current placement or possession |

All display strings in these records are intentionally disclosed text. A visibility
bit over `Character.Description` would still expose new private details whenever that
description changes; observations store the text that was learned instead. Likewise,
an observed description of a closed cigarette case does not include its contents.

Separate names from other aspects. An unnamed person may be known as **the woman in
the green coat** without exposing their canonical identity. A later identification
updates the learned label and can retain that public alias for links. A canon rename
alone does not update the learned name. IDs are hidden in Player view.

Whereabouts is a typed value: at a location, held by a character (items), or explicitly
unknown after a narrated loss of contact. Relations use stable IDs, but labels and
links resolve only through the player's discovery records. If a destination/holder
is not identified, show the disclosed description or **Not identified**, never its
private name. Learning a named destination should also record that location's identity.

Each aspect distinguishes **observed**, **reported** and **authored starting knowledge**.
A report can point to an existing learned, attributed fact; it must not change the
subject's actual location or mark the report as verified. Preserve the latest direct
sighting separately from the latest report when they disagree. Present both, with
their source/turn, instead of letting a rumour overwrite a witnessed encounter.
This is a small last-known record, not a full journal or a probability system.

### Last known is not current

If the player sees Julian at the hotel on turn 8 and leaves, the panel says **Last
seen: Hotel Argent · turn 8**. If Julian moves secretly on turn 10, nothing in that
entry changes. Never compare his real location with the remembered one to decide
whether to show an uncertainty marker: even that would reveal hidden movement.

An observation from the latest completed turn may say **Seen here this turn**.
Otherwise show its recorded turn. Do not treat co-location or `LastSeenTurn` as proof:
the existing engine touches every co-located character, including concealed ones.
The location panel's people/items sections describe recorded sightings and their
turns, not a live census. Newly narrated observations refresh them; silent changes do not.

If the player sees Julian leave without seeing where he goes, record **Whereabouts
now unknown**, keeping the earlier sighting as historical context. An unseen departure
does not generate that update. Secret deletion, destruction or author correction also
does not erase or change remembered observations. Missing canon targets remain readable
as remembered entries without an automatic **Removed** label in Player view.

### Departure without an established destination

Clarified with the player after the initial draft: the engine currently extracts
world changes from visible narration. It has no autonomous offscreen simulation or
private LLM action channel. This proposal does not add either mechanism. Examples of
secret movement mean author changes or future capabilities, not current engine behaviour.

If narration establishes that Julian leaves his current location but does not establish
a destination, actual canon must record `LocationId = null` (offstage). Player discovery
records whereabouts unknown and retains the last sighting. Leaving the old LocationId
unchanged would incorrectly keep him in the room. If a destination is established for
that particular movement but not disclosed to the protagonist, preserve that actual
destination while withholding it in Player view. An old location is not evidence of
a destination for a new departure.

Proposed extraction change: allow the existing NPC `character_moved` destination to
be null for an explicitly narrated departure with no established destination. Keep
`player_moved`'s existing destination requirement. Update schema, validator, applier
and context handling together; null must not invent connections or destroy the NPC's
held items. This is separate from an observation-only change: learning a rumour,
losing sight of someone within the same room, or failing to find someone must not
automatically move them offstage. Only a departure from the location justifies it.

An offstage NPC can later return through an ordinary movement to a real location;
the same stable identity and possessions survive. Actual and observed whereabouts
must be scored independently in the evals below.

IDs remain permanent identities. Reusing a removed ID keeps its old discovery record;
it does not silently reset knowledge or reveal replacement fields. Author UI must explain
this consequence. Missing/disagreeing keys are diagnosed in Author view; unresolved
relationships in Player view use recorded text without guessing another target.

## Facts and lore

Player view's Canon tab shows only facts whose IDs are in `world.Player.Knows`.
Attribution is retained, so **Vivian says Julian went to the station** is not presented
as a verified position. Source names use discovered labels. Do not show other
characters' knowledge sets, or the global Known by list.

A fact is the existing indivisible unit of knowledge. A fact mixing a public clue
and a secret answer must be split by the author; a visibility flag cannot safely
reveal half its text. Rewording a known fact keeps the existing correction semantics:
its single text changes for its knowers. The author must review those memberships.

Lore needs care: the current context says a character **has heard of** a known topic;
that is not evidence they have read every paragraph of its authored body. Player
view may show learned topic titles, but must not expose whole non-common lore bodies
from a topic membership alone. Common lore remains author-declared common knowledge.
Learned details can be recorded as specific facts/observations; Author view retains
the complete lore browser. Review bundled common lore for accidentally embedded secrets.

## Concealment is an explicit authoring rule

Alongside starting discovery, the pack may mark a character, item, location or directed
connection **Requires discovery**, with an optional private narrative instruction.
Default: **Ordinary scene presentation**. This is structured metadata, not a parser
for words like hidden, invisible or missing in a Status string.

The rule tells the narrator not to announce an undiscovered presence, object, place
or route merely because it is in the current scene. It is not an executable quest
condition, skill check or guarantee of invisibility. Discovery happens through a
narrated encounter, search, disclosure or other story event. Ordinary scene presentation
also does not mean every field is public: describing an item never reveals all its
contents, and a present NPC's internal motives remain private.

The rule does not erase learned identity or past sightings. Knowing that Julian exists
does not satisfy discovery of his present hiding place. Knowing Hotel Argent exists
does not disclose a concealed rear entrance; directed connections have their own rule.
The narrator considers the player's recorded observations when maintaining continuity.
It must not repeatedly make the player rediscover something already exposed in the scene.

Initial proposal for The Last Lantern: Julian and the black ledger require discovery.
The visible cigarette case does not, but only what is visibly described is initially
known. Do not infer additional secret routes from colourful hotel prose; the author
sets route rules explicitly. Keep Status available for physical/situational description.

These rules are seeded into each new save and editable through Author view. A later
pack update does not silently rewrite the rules or observations of an existing save.

## Narrator context and automatic discovery

The narrator needs actual scene truth to keep the mystery coherent. Simply filtering
its input to the player panel would make it forget concealed participants and clues.
Build clearly separated context sections:

1. **Player knowledge and observations**: the same disclosure policy used for the UI,
   including reported versus observed information and last-known turns.
2. **Private scene truth**: actual relevant entities/state, their presentation rules
   and sheets. Being listed here is not permission to disclose their private fields.
3. **Private story/lore guidance**: scenario, secrets and relevant authored context,
   with common/player-known material distinguished explicitly.

Keep the current scene-based scope; this design does not introduce a whole-world
prompt dump or a relevance/retrieval subsystem. Name references for the narrator
must distinguish a private canonical name from the player's known alias. Never
fall back to private names inside public descriptions. The extractor retains IDs
and enough private context to resolve existing targets without inventing new ones.

Discovery is recorded **from what narration actually reveals**, not from the
player's attempted action, private context, current co-location, status changes,
fact text name matching or accepted movement deltas alone. A failed search teaches
nothing about the concealed target. A player's assertion may be a lie; their input
alone cannot reveal an entity or establish its location.

### Proposed extraction contract

Extend the existing extraction call with three closed, typed delta kinds:
`character_observed`, `location_observed`, and `item_observed`. These are provisional
names for schema review. Each has an existing target ID, explicit supported aspects,
provenance and a short evidence quotation from the current narration. They update
only player observation records, never the entity's actual state.

Use optional typed aspect records: omitted means leave that aspect unchanged; a
specified **whereabouts unknown** is an explicit update. There is no arbitrary
property path or generic string-to-field assignment. Conditions/demeanour are
disclosed wording, not a request to copy a private canon field. Location connections
are individual directed observations, never a copy of all current connections.

Existing `fact_established` / `fact_learned` continue to handle learned truths and
attributed claims. A merely mentioned name which does not identify an existing canon
entity can remain in such a claim without creating a new entity. Do not weaken the
current rule against inventing canonical people/places from a player's assertion.
When narration genuinely introduces an entity, its accepted introduction and its
observation may occur in the same batch, in that dependency order.

Existing deltas were considered first. Movement/status changes describe world truth
without identifying what the player perceived; a sighting may occur with no such
change at all. Fact learning covers claims, but turning free-text facts into entity
field snapshots would require another semantic interpretation step. That is why this
proposal makes observed entity aspects explicit rather than inferring them from those
existing deltas. It does not claim the three proposed branches have been measured yet.

Core validates targets and references against accepted canon changes, checks evidence
is present in the current narration, verifies required provenance and referenced
learned facts, and orders discovery dependencies explicitly. Reject invalid
observations visibly; do not substitute a secret real value. A rejected introduction
cannot leave a discovery entry pointing to a newly invented target. A contradictory
reported whereabouts value can be valid as an attributed report; it is not canon truth.

The optional aspects form one validated observation operation per subject. A malformed
aspect refuses that observation, preserving earlier values; unrelated valid canon or
observation deltas can still apply under the existing partial-batch rules. Reported
aspects require a referenced fact learned by the player and its attribution; the
validator orders fact establishment/learning before dependent observations. A subject
must have a known safe label already or receive one in the observation. Cross-links
remain unavailable until their destination's safe identity is known, even if the
underlying reference is structurally valid. No batch order may reveal a private label.

Evidence quotations make a proposal auditable, but do not mechanically prove that a
sentence revealed a particular secret or that the model resolved an alias correctly.
That semantic accuracy needs manual examples and provider-labelled measurement.
The proposal uses one extraction call per turn, not a second discovery model pass.
Adding three schema branches can affect existing extraction: this cost is explicit
and must be evaluated before calling automatic discovery reliable. The set remains
closed and enumerated, consistent with PROJECT.md.

## Turn lifecycle, failure and repair

Apply validated canon changes and validated observations under the existing session
guard and persist them in the same canon save. Preserve per-aspect timestamps and
evidence/provenance in the observation records; include applied observation deltas
in TurnRecord.Applied so their effect is auditable. The UI refreshes only from the
resulting Player projection. Observation failures must not expose private fallbacks.

An unchanged repeated observation in the same turn is a no-op. Seeing the same
thing again on a later turn may refresh its last-seen timestamp and counts as a
real discovery-state change. Partial observation updates preserve prior aspects.

Retry uses the original narration and turn number, retains earlier applied changes,
and merges only valid observations. It cannot restamp old prose as today's sighting,
overwrite a newer observation, or silently undo an author correction. Store a
per-aspect revision/provenance marker; author edits are protected from automatic
retry of older evidence unless the author explicitly clears that protection.
Exact same-turn conflicting observations are refused for author review, rather
than arbitrarily letting response order decide which location the player remembers.

Reroll remains available only when nothing changed. A turn that changed discovery
state now counts too, even if the physical world stayed still. This makes Reroll
more restrictive; adding snapshot-based undo is a separate, unapproved feature.

If extraction fails, retain the narration and previous observations. Show **Discovery
updates incomplete** without exposing rejected private values. Retry or Author view's
discovery correction repairs them. If the narrator itself spoils a secret in prose,
the UI cannot undo the reading: keep the prose, and record what was actually revealed.
Concealment is not a censorship filter or a guarantee against model mistakes.

Existing partial-write recovery still applies: a save/history failure may leave
memory or disk partly advanced, so require reopen and do not claim Cancel rolls it
back. Check Canon gains discovery integrity checks, but permits remembered references
to deleted entities as historical observations rather than treating them all as damage.
Unknown versions, invalid kinds, impossible aspect shapes and broken observation/claim
references need explicit findings or refusal at the relevant boundary.

## Starting state, legacy saves and author controls

New packs can author starting discovery in seed JSON beside canonical state. Load it
as separate values; never generate it by copying every entity's current fields.
Authors provide safe labels/descriptions, known connections, provenance and initial
fact memberships. The pack loader validates IDs and shapes. Starting observations
use turn 0. Complete player creation before resolving the player's own authored name.

The opening is already-written prose and has no extraction pass. Starting discovery
must therefore match it explicitly. Do not silently call an LLM on session open or
infer all co-located entities as visible. Updating each bundled pack's initial
discovery and presentation rules is part of implementation, with The Last Lantern
reviewed against the sequence below. Future World Editor controls author this same data.

| The Last Lantern moment | Player-facing result |
|---|---|
| Opening as currently written | Player, office, Vivian's introduced name/visible appearance, Eddie and the visible cigarette case; no Julian or ledger entry just from private seed data |
| Vivian describes her missing husband | Julian becomes known by name with disclosed background; **Whereabouts: Unknown**; learned facts reflect what she actually says |
| Vivian mentions the ledger | Its existence/claimed purpose becomes known; holder, coded contents and private properties stay unknown |
| Player enters Hotel Argent without finding Julian | Disclosed hotel details update; Julian does not appear in an occupant list because of co-location |
| Player finds Julian | Narrated appearance/condition and sighting at the hotel become recorded; private motives/knowledge remain private |
| Julian moves out of sight | Previous sighting remains; no live tracking |
| Someone reports another location | Show an attributed report alongside the last actual sighting; do not teleport Julian in canon |

Missing discovery in an old save means **not initialized**, not **everything known**.
Open it with a minimal Player projection (the player's own identity; other information
unknown) and a clear compatibility notice. Offer Author view to initialize known
entries deliberately. Do not apply today's pack seed to a progressed save, replay
old extraction automatically, or rewrite history to guess what the player learned.
Initialization persists on an explicit author save or the next successful turn,
not as an incidental write during a read-only open. Legacy packs without discovery
remain usable with the same limited initial view and an authoring notice.

The discovery format is versioned. New code refuses unsupported discovery versions
rather than dropping their fields on save. Older binaries do not understand the new
state and cannot be promised a safe round trip; compatibility documentation must say so.

Author view adds **Player knowledge** alongside full entity editing:

- Mark identity known/unknown; edit the safe name/description and individual observed
  aspects; set/clear a last known location/holder or reported information.
- Review actual canon beside the observation. **Copy this field from canon** is
  explicit per field; it is never the effect of simply checking Known.
- Inspect and edit Requires discovery and its private narrative instruction.
- Select what the player should know when adding an entity. Default is no automatic
  discovery; creation by the world author does not imply an encounter in the story.

These are typed, stale-checked Core operations with detached drafts and one save,
like the existing editor. Changing canon does not refresh observations implicitly.
Changing discovery does not move entities or alter private state. Marking an entity
unknown hides its observation entry; it does not silently remove independently
learned facts that name it. Show that consequence and let the author edit those facts'
knowledge memberships explicitly. Removal previews likewise explain retained memories.
Editing facts continues to use the existing Known by controls. No new UI logic owns
cross-entity mutations, and no application code is changed by this design task.

## Implementation and validation after approval

Implementation needs Core observation/rule models and projection, Storage support
for the versioned JSON, App/session integration, three typed extraction schema cases
and nullable NPC departure support in the existing movement kind,
narrator/extractor prompt changes, bundled pack seed authoring, and Desktop view and
correction controls. It is a domain-and-UI task, larger than filtering the tabs.

Manual acceptance must cover the Last Lantern sequence, false reports, unnamed people,
duplicate aliases, hidden held items, unknown item contents, secret directed routes,
observed departure versus secret movement, and an author change after a sighting.
Inspect every indirect surface, including narration links after Author/Player switches,
reports, source labels and old observations whose canon target was removed.

Also review turn-zero opening, fresh/legacy saves, save/reopen, partial extraction,
no-op retries, stale and conflicting observations, author-protected corrections,
discovery-only Reroll refusal and persistence failure. Keep the Add/Remove manual
review outstanding; this proposal does not certify that earlier implementation.

The player explicitly requested eval coverage for these new scenarios. Required cases,
expected outcomes and run/report rules are in `PLAYER_DISCOVERY_EVALS.md`; implementing
them is part of the feature, not optional follow-up work. Existing extraction regression
scenarios must run alongside them when evaluating the changed schema/prompts. General
code testing remains build-only under project policy; model evals measure extraction,
and desktop interaction stays under manual review. No model calls or runtime tests were
run for this design. Do not claim a planned case has passed or equate schema validity
with correct perception.

Review decisions: the Player/Author split, snapshot observations rather than visibility
bits over live fields, explicit presentation rules, three new extraction kinds in the
existing call, the conservative legacy-save behaviour, and the Reroll consequence.
They are recommendations awaiting the player's approval, not locked decisions yet.
