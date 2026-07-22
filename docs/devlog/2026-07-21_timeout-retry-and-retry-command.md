# Devlog — the one transient failure we did not retry

**Date:** 2026-07-21
**Scope:** timeout handling, `/retry`, and why reroll is a separate problem

---

## Reported

Two turns in a row died with `Extraction failed: Request timed out after 120s`, followed by
`Canon did not change this turn`.

The guess offered alongside it — that this happens because we pin the provider — was worth
correcting: **play does not pin anything.** `providerOrder` is a test instrument used only by
the eval; play sets `providerIgnore: ["AtlasCloud"]`, so nine providers and full failover
remain. If we *were* pinning, timeouts would be far more likely, which is precisely why
pinning stayed out of the play path.

## The bug

`OpenRouterClient` retried `HttpRequestException`, and retried `429`/`5xx`. It did **not**
retry a timeout:

```csharp
catch (TaskCanceledException ex)
{
    _log.Error("Request timed out", ex);
    return LlmResult.Failure($"Request timed out after {...}s.", attempts);  // returns immediately
}
```

A timeout is arguably the *most* transient failure available — it usually means the request
landed on an overloaded upstream, and OpenRouter routes a retry independently. Three attempts
of the budget sat unused while a recoverable blip became a lost turn. No reason for the
asymmetry appears anywhere; it looks like an oversight from the original port.

**Fixed, with a separate bound.** Timeouts now retry, but at most `MaxTimeoutAttempts = 2`,
because a timeout is the one failure that costs its full deadline before failing — four
attempts at 120s would leave a player staring at a blank console for eight minutes.

## Per-role timeouts

120s was always the wrong number for extraction, which returns about 140 tokens and normally
answers in seconds. Waiting two minutes only delays the retry that was going to fix it.

`timeoutSeconds` is now settable per role, falling back to the provider default. Extraction is
set to 45s. Implementing it meant moving off `HttpClient.Timeout` — a single client-wide value
would force narration and extraction to share one budget — to a linked `CancellationTokenSource`
per request. Caller cancellation is still distinguished from a deadline by checking which token
fired.

## `/retry`

The narrower half of what was asked for. When extraction fails, the prose was fine and only
the bookkeeping broke: re-narrating would waste a call on the expensive model *and change the
story the player already read*.

`/retry` re-runs extraction against the stored `PlayerInput` and `Narration` from the last
turn. Everything needed is already in the `TurnRecord` — one cheap call against the small
model.

Two decisions:

- **It repairs the record rather than appending one.** The story did not happen twice.
  Appending would duplicate the narration in the log and therefore in the narrator's memory
  window, which is built from it. `IWorldRepository.ReplaceLastTurnAsync` is the one deliberate
  exception to history being append-only.
- **Validation runs again from scratch.** Canon may have moved on since the failed turn, and a
  delta that was valid when first proposed can be a no-op or a conflict later.

The failure message now points at it, since a silent "canon did not change" is the drift this
architecture exists to prevent.

## Why reroll is a bigger job

Rerolling — discard the turn, narrate again — is what chat-RP sites offer and what people
actually want when prose is wrong rather than absent. It cannot be bolted on, because **our
deltas are not invertible**: `MoodChanged(hald, "wary")` does not record the previous mood, so
no undo can be computed from the turn log.

Carrying previous values on every delta was considered and rejected — it doubles the schema
surface the extraction model must fill in correctly, to serve a feature the model should not be
thinking about. A snapshot of canon taken before each apply keeps the cost in storage, where it
is cheap and testable. Logged with design notes.

## Verification

Offline, with a fake `HttpMessageHandler` injected into the client — no credits, no flakiness.
16 assertions:

- a permanently hung upstream is retried exactly twice, then fails naming the timeout
- **a timeout that then succeeds recovers on the retry** — the reported scenario
- the per-role deadline is used rather than the provider default, confirmed by elapsed time
- `ReplaceLastTurnAsync` repairs in place: two turns stay two, the earlier turn is untouched,
  the narration is preserved, deltas and raw extraction are updated
- replacing on a world with no history is a no-op rather than a crash

Build clean, 0 warnings.

## Next

The reroll snapshot, when it is wanted. And §9 — with timeouts recoverable, a long session is
less likely to be interrupted by a blip that was always survivable.
