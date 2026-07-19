# Devlog — LLM client and first live calls

**Date:** 2026-07-19
**Scope:** TODO_BOOTSTRAP §4 (LLM layer)

---

## What was built

`StoryWeaver.Llm` now talks to OpenRouter. Seven new files:

| File | Purpose |
|---|---|
| `LlmMessage.cs` | role + content, with `System`/`User`/`Assistant` factories |
| `LlmCall.cs` | one request: role, messages, optional schema, optional validator |
| `LlmResult.cs` | success/failure, content, model, usage, attempt count |
| `ILlmClient.cs` | the single provider-facing abstraction |
| `OpenRouter/OpenRouterWire.cs` | wire DTOs, `internal`, never leak past the client |
| `Logging/ILlmLog.cs` | log sink + a null implementation |
| `Logging/FileLlmLog.cs` | one file per session |
| `OpenRouter/OpenRouterClient.cs` | the port from AI-Lord |

Ported from `D:\Hobbies\Coding\AI-Lord\src\AILord\LLM\` as planned, rather than written
fresh. The valuable part was never the HTTP — it was the retry and repair structure,
which had already been through the "why did this cost nine calls" debugging pass.

## Decisions worth recording

**Failures are returned, not thrown.** A model timing out mid-session is an expected
outcome of a game loop, not an exceptional one. Making the turn loop wrap every call in
`try`/`catch` would put error handling in the wrong place.

**One retry budget, not two.** `MaxTotalAttempts = 4` covers transient HTTP failures
*and* content-validation failures together. The obvious implementation — a retry loop
around a validation loop — quietly permits a multiplicative worst case. AI-Lord carries a
comment about exactly this; the nested version allowed nine calls for one request.

**`onChunk` callback over `IAsyncEnumerable<string>`.** Streaming is not implemented, but
the interface allows it without a rewrite. A pure text stream would mean returning usage,
serving model, and error state some other way, complicating every caller to serve a
feature we do not have. Today the callback fires once with the full text.

**Logging is a first-class dependency, not a debug afterthought.** Debugging extraction
means reading exactly what was sent and exactly what came back. That is not
reconstructable after the fact, so it is an interface with a real implementation from the
start. Write failures are swallowed deliberately — a failed log line is worth less than
the session it would take down.

## Smoke test

Behind a `--smoke` flag rather than running on startup, since it spends real credits.
Two live calls: narration, then schema-constrained extraction over that narration's
output.

Result — both succeeded, one attempt each, no repair round-trip:

```
[1/2] Narration ... served by qwen/qwen3.7-plus, 1522 tokens
[2/2] Extraction ... served by deepseek/deepseek-v4-flash, 821 tokens
  {"location":"a tavern in Marrow","characters":["patrons"],"mood":"grim"}
```

**This closes the open question from the research pass.** `deepseek-v4-flash` honours
`json_schema` through OpenRouter with `require_parameters: true`. The validator+repair
loop stays as the fallback it was designed to be, rather than being promoted to the
primary mechanism.

Two observations logged to CHALLENGES rather than left in a terminal buffer:

- Extraction was **~35% of the turn's tokens**, not the 5–10% the design assumed. Both
  models are reasoners, so much of it is thinking rather than output — but "extraction is
  a rounding error" does not survive first contact.
- The extraction was correct but **lossy**: `"characters": ["patrons"]` flattened a crowd
  into one unnamed entity. Harmless here, and exactly the shape of thing that silently
  degrades an entity graph over a long session.

## Problems hit

- **`json_schema` would not deserialize.** `JsonStringEnumConverter` matches
  case-insensitively but not across underscores. Fixed by registering
  `new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)` — deliberately keeping
  the config file in OpenRouter's own vocabulary rather than bending it to PascalCase.
- **All four roles reported missing while plainly present in the file.** System.Text.Json
  *replaces* an `init` collection rather than populating it, silently discarding the
  `StringComparer.OrdinalIgnoreCase` it was constructed with, so `"narration"` never
  matched `"Narration"`. Fixed by making `Roles` settable and rebuilding the dictionary in
  the loader — with a comment, because it will read like a typo next time.
- **`System.Threading.Lock` is .NET 9-only.** We target net8.0. Plain `object`.
- **`Path` as a property name** collides with `System.IO.Path`. Renamed to `FilePath`.

## Next

§5 — the minimal domain model. `Entity`, `Character`, `Location`, `Fact`, `WorldState`,
`StateDelta`. Deliberately small; expand only when a turn needs it.
