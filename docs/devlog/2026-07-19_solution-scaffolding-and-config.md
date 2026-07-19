# Devlog — 2026-07-19 — Solution scaffolding and configuration

**Status:** Bootstrap plan sections 1–3 complete. Builds clean, runs, validates config.
No domain code and no LLM client yet.

## What was built

```
StoryWeaver.sln
├── src/StoryWeaver.Core      (empty — references nothing)
├── src/StoryWeaver.Llm       → Core   ·  Configuration/
├── src/StoryWeaver.Storage   → Core   (empty)
└── src/StoryWeaver.Cli       → all three
```

`dotnet build`: 0 warnings, 0 errors, with `TreatWarningsAsErrors` and nullable enabled.

### Decisions made during the work

**`net8.0`.** Installed SDKs are 8.0 (LTS) and 9.0 (STS) — no 10. .NET 8's LTS window ends
around Nov 2026; bumping is a one-line change in `Directory.Build.props`.

**`StoryWeaver.Cli`, not `StoryWeaver.Console`.** A `StoryWeaver.Console` namespace shadows
`System.Console`, so every unqualified `Console.WriteLine` inside the project breaks or
needs qualifying. Cheaper to avoid than to work around.

**`Directory.Build.props` for shared settings** rather than repeating target framework,
nullable, and warning settings across four `.csproj` files.

**Warnings as errors.** Aggressive for a hobby project, but nullable-reference warnings are
worth enforcing while the domain model is still forming — a null reaching the entity graph
is exactly the class of bug that surfaces 200 turns later.

### Configuration

`SettingsLoader` reads `settings.local.json`, applies environment overrides for the API key
(`STORYWEAVER_API_KEY`, then `OPENROUTER_API_KEY`), and validates before returning.

Design points worth recording:

- **Aggregates all validation errors** rather than throwing on the first, so one run tells
  you everything that needs fixing.
- **Enforces the `json_schema` ⇒ `requireParameters` coupling.** This is the guard against
  the OpenRouter routing hazard from CHALLENGES.md, and it turns an intermittent
  wrong-output failure into a startup error with instructions. The highest-value thing in
  this commit.
- **Walks up from the executable** to find the settings file, so the real file stays at the
  repo root where it is gitignored and editable without a rebuild.
- **Roles are a dictionary**, not fixed properties, so adding a role later is a config entry
  rather than a refactor.
- **API key is masked** wherever it is printed.

## Two bugs the build did not catch

Both found by actually running the harness. Recording them because the project's testing
policy leans on `dotnet build`, and neither of these produces a compiler diagnostic.

**1. Enum values with underscores.** Config uses `"json_schema"` to match OpenRouter's own
vocabulary, but `JsonStringEnumConverter` only does case-insensitive matching, not
underscore handling. Fixed by registering the converter with `JsonNamingPolicy.SnakeCaseLower`
rather than changing the config vocabulary — the file should read in OpenRouter's terms.

**2. System.Text.Json replaces `init` collections.** It does not populate the existing
instance, so the `StringComparer.OrdinalIgnoreCase` the dictionary was constructed with was
silently discarded. `"narration"` in the file never matched the `"Narration"` lookup key and
every role reported as missing — while plainly present in the file. Fixed by rebuilding the
dictionary in the loader, with a comment, because it will look like a typo next time.

## Verified

- Valid config loads; key masked as `sk-o...xxxx`
- Missing values produce one aggregated list, exit code 1
- `json_schema` without `require_parameters` is rejected with a message naming both fixes
- Exit codes 0 / 1 correct

## Configured models

Narration on `qwen/qwen3.7-plus`, extraction on `deepseek/deepseek-v4-flash` — capable model
for prose, cheap one for the per-turn structured pass. Whether deepseek-v4-flash actually
honours `json_schema` through OpenRouter is unverified; `requireParameters: true` should make
an unsupported combination surface as a routing failure rather than silent prose, but that is
untested until the client exists.

## Next

Section 4: port the LLM client from AI-Lord — decouple from `MCMSettings`, extend
`ResponseFormat` to `json_schema`, add `provider.require_parameters`, swap Newtonsoft for
System.Text.Json, add per-role resolution.
