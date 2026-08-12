# TODO — a second world pack

Marrow now ships an authored player, which is the right default and cost the manual
character-creation path its only shipped coverage. A second pack restores that, and does
something Marrow structurally cannot: **prove a pack is portable.**

Every pack rule so far was designed against Marrow and validated against Marrow. Different ids,
different lore, different tone, nothing shared — that is the first real evidence the format
works for anything but the world it grew out of.

---

## The pack: `ashfall`

A waystation on a volcanic pass, the road closed by falling ash, travellers stuck inside. Chosen
to be tonally and structurally unlike Marrow: no marsh, no cult, no well, and **the player
arrives as a stranger**, which is what makes a blank slate coherent rather than an omission.

- [x] `worlds/ashfall/seed.json` — 2 locations, 3 characters, 1 fact, 1 item
- [x] `lore/` — 3 entries, one of them `common: true`
- [x] `characters/` — 2 sheets, **no `player.md`**
- [x] Attitudes toward lore entries, toward another sheeted character, and toward `{{player}}`
      — phrased as a stranger-appropriate stance, since an authored fondness for someone who
      walked in ten minutes ago would be incoherent

## What it is built to exercise

| | Marrow | Ashfall |
|---|---|---|
| character creation prompts | skipped (`player.md`) | **runs** |
| `{{player}}` resolving | authored name | **a name typed at the prompt** |
| `common: true` lore | yes | yes |
| NPC-to-NPC attitude | yes | yes |
| ids sharing no prefix with the other pack | — | **yes** |

The `{{player}}` row is the interesting one. Marrow resolves it to a name the pack author wrote,
which is the easy case. Ashfall resolves it to whatever the player types thirty seconds earlier
— and that is the case the whole mechanism was justified by: *a pack author cannot know the
player's name.* It has never actually been run.

## Code — done

- [x] **`--pack <id>`**, defaulting to `marrow`. `PackId` and `SaveId` are `const` threaded
      through ~14 sites in `PlaySession`; the comment on `SaveId` already anticipates this —
      *"a pack supporting several saves is only a matter of choosing this at startup"*
- [x] **The save defaults to the pack id**, with `--save <id>` to override. Two packs sharing
      one save is not a configuration but a corruption: the ids written by one world do not
      exist in the other
- [x] **`CheckShippedPackLoads` iterates every folder in `worlds/`** instead of hardcoding
      marrow, and reports whether each pack authors its player. This is what actually verified
      `ashfall`, and it found a real problem on the first run

## Found while building: decision 1 has never been true

`seed.json` was supposed to drop `name` and `description` for any character with a sheet —
"nothing is written twice, so nothing can disagree". **`Entity.Name` is `required`.** A seed
that omits a name is refused by the deserializer, and always has been. Marrow carries names for
Hald and Mabb, which read as harmless duplication and was actually the format quietly complying
with the type while the design said otherwise.

Behaviour is fine — `ApplySheets` overwrites, so the sheet wins deterministically. What is
wrong is that two files can state a name and only one is read: rename someone in their sheet
and the seed keeps the old one, silently. That is the precise failure decision 1 was written to
prevent.

- [ ] **Make `Name` optional on the seed path, then refuse a seed that names a character who
      has a sheet.** Turns the duplication into a load error rather than a trap. Touches a Core
      type, so it wants its own decision — not folded in here

**How it surfaced is worth keeping.** The pack was pre-flighted with a script that
reimplemented the load rules, and it passed. The real loader failed it in one line. *A check
written from the same understanding as the thing it checks cannot find a misunderstanding* —
which is the same lesson as `CheckShippedPackLoads` itself, arriving from the other direction.

## Verify

- [x] `dotnet build` clean, self-tests pass, **both** packs load:
      `ashfall` 3 seated / 2 sheets / 3 lore / blank slate, `marrow` 4 / 4 / 3 / authored player
- [ ] A short play session on `ashfall` — the point is to see the opening prompts and watch
      `{{player}}` resolve to a name that was not in any file

## Out of scope

- **Pack selection at runtime** (a menu, `/pack`). A flag is enough until there is a UI
- **Sharing content between packs.** Lore inheritance, common characters, a shared bestiary —
  all plausible, none needed, and each is a way for two packs to stop being independent, which
  is the property this pack exists to test
