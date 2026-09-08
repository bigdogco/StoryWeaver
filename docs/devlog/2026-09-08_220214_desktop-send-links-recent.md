# Desktop send, narration links and recent playthroughs

The desktop Play surface now has the first live turn loop. Sending a draft adds a
pending player entry immediately, clears and disables the composer while the
session writes narration and updates canon, then replaces the pending entry with
the saved turn. Failures restore the draft unless the world has already changed,
in which case the UI blocks further send attempts until the playthrough is
reopened and inspected.

Narration links are a presentation pass over current canon. Opening and narration
paragraphs link exact unique character, location and item names, and character
mentions also link unique name parts such as first names. Titles and ambiguous
shared name parts remain plain text. This keeps saved prose unchanged while
making the right-hand information tabs reachable from story text.

Reloaded playthroughs now show the authored opening before recent turns. That is
a UI transcript decision only: the opening is still not stored as turn zero, and
Core's narrator memory still lets it fall out of the recent window. The Library
also gained a Recent view, persisted in desktop preferences, so successful opens
can be reopened directly by workspace, world and save id.

Validation:

- `dotnet build StoryWeaver.sln --no-restore -c Release`
- `git diff --check`

