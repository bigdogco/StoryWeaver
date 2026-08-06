# Devlog — who these people are

**Date:** 2026-08-06
**Scope:** character sheets, `{{ }}` references, and a character creation step

---

## What shipped

Authored identity as pack content: `worlds/{pack}/characters/{id}.md`, one file per character,
the same shape as a lore entry. Filename is the id, `#` heading is the name, prose body is what
the narrator reads, frontmatter carries only attitudes.

```markdown
---
attitudes:
  kings-investigators: has never met one and would rather it stayed that way
  drinker-mabb: tolerates him, and watches his mouth when strangers are in
---

# Hald

Heavyset and watchful, with forearms like ham hocks and a publican's memory for faces...
```

Rendered into the scene under the character it belongs to:

```
### Mabb
An old marsh-hand gone soft at the edges...
State: drunk, maudlin
Toward the player: no strong feelings (0)
Feels about:
  - The Cult of the Blind: believes every word of it and will not say so sober
  - Hald: drank with him for thirty years and is still a little afraid of him
  - Pavel: curious about Pavel in the way a lonely man is curious about anyone new
```

## The split, and why nothing is written twice

**The sheet defines the character; `seed.json` holds their starting state.** A sheet supplies
name and description; the seed supplies location, mood, status, standing and knowledge. Merged
at load. Nothing lives in two places, so nothing can disagree — which is the failure every
other arrangement shared in a different disguise.

A seeded character with no sheet is untouched, so every pack that existed before today keeps
working. A sheet with no seed entry creates the character **offstage**, which
`Character.LocationId` already allowed.

The interesting distinction is inside the attitudes. *"Drank with him for thirty years and is
still a little afraid of him"* is **history** — permanently true, however the relationship
develops. `Toward the player: (0)` is a number that moves every turn. Both appear, one line
apart, and they are different kinds of thing: **the sheet holds the why, canon holds the
standing.**

## Prose, not fields

Decided before building and worth restating: the model consumes prose either way, so structured
fields buy nothing for comprehension and cost expressiveness. `build: heavyset` loses "wipes the
same patch of counter when he is thinking", which is the detail that makes Hald land.

Frontmatter carries attitudes only, because that is the one part code must resolve against ids.

**One parser extension, deliberately.** `MarkdownLoreReader`'s frontmatter was flat by design —
three scalars and a list, no YAML dependency, unknown keys refused. Attitudes need one level of
nesting, so a shared `Frontmatter` reader now handles exactly one, with the same strictness. The
alternative was `dislikes: a, b`, which parses today and throws away the phrase.

Both readers moved onto it, and onto a shared `MarkdownFile` for the heading-and-body grammar.
The lore checks passed unchanged through that refactor, which is the only reason to trust it.

## Two problems the first render exposed

Neither was predicted; both were obvious the moment a sheet appeared in a prompt.

**Headings collided.** A sheet writes `## Manner` because that is natural markdown in its own
file. Pasted in unchanged it sat at the same level as `## Present` and `## World lore`, so a
character's sections read as top-level sections of the prompt and the structure the model relies
on quietly stopped meaning anything. Authored bodies are now pushed to `####`. An author should
not have to know what depth their prose will be rendered at.

**"Curious about You."** The design predicted this one: the seed shipped a player named `"You"`,
so `{{player}}` rendered exactly as feared. The fix is not a better default — **names are fixed,
for the player as much as for any authored character** — so character creation is now a required
step before turn one, asking for a name and a description. It also replaces a starting character
nobody chose.

## References

`{{player}}` and `{{<entity-id>}}`, resolved at context assembly so a rename flows through, and
**validated at pack load** so an unresolvable id fails by file and name rather than reaching a
prompt. An id in the prose is the bug that produced "the heavy oak door of the marrow-tavern
flies outward"; a reference resolving to a blank is a sentence with a hole in it. Both are worse
than a startup that refuses and says why.

Attitude targets are validated too — an attitude toward nothing is a dangling edge with no
visible symptom, since the sheet reads fine and the feeling attaches to nobody.

Deliberately a closed set of two forms. SillyTavern's macros grew conditionals, randomness and
state lookups; a third form here should be a decision rather than a discovery.

## Results

```
--selftest         4 new checks: nested attitudes, unknown-key refusal, reference resolution,
                   unresolvable detection
full scored set    50/50 clean, pinned
end to end         sheets load, merge, render; creation captures name and description;
                   {{player}} resolves to it
```

## Untested, and worth being honest about

**Whether the narrator actually uses any of this.** The entire case for prose over fields is
expressiveness — and if the body is ignored in favour of the one-line description, the design is
wrong and nothing here would reveal it. That needs a narration-side check, which does not exist.

**Context size.** Sheets are the third contributor after lore and loose items, and the first
that can add several paragraphs per present character. A crowded room is now a much larger
prompt, and none of it is measured.

Both want the same thing: a play session.
