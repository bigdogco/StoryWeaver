using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;

namespace StoryWeaver.Llm.OpenRouter;

/// <summary>
/// OpenRouter implementation of <see cref="ILlmClient"/>.
///
/// Ported from the AI-Lord client, with the same core structure: a single retry budget
/// shared by transient HTTP failures and content-validation failures, so one call can never
/// cost more than <see cref="MaxTotalAttempts"/> HTTP requests regardless of how it fails.
/// Nesting those as separate loops is the obvious implementation and quietly allows a
/// multiplicative worst case.
/// </summary>
public sealed class OpenRouterClient : ILlmClient, IDisposable
{
    /// <summary>Total HTTP calls one <see cref="CompleteAsync"/> may make, across all
    /// failure causes.</summary>
    private const int MaxTotalAttempts = 4;

    /// <summary>
    /// How many of those attempts may be spent on timeouts.
    ///
    /// Timeouts are retried — they usually mean the request landed on an overloaded upstream,
    /// and OpenRouter routes the retry somewhere else — but they are the one failure that costs
    /// the full timeout before failing, so the general budget is the wrong bound. Four attempts
    /// at 120s would leave a player staring at a blank console for eight minutes.
    /// </summary>
    private const int MaxTimeoutAttempts = 2;

    private static readonly int[] RetryDelaysMs = [1000, 3000, 5000];

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly StoryWeaverSettings _settings;
    private readonly ILlmLog _log;

    /// <summary>
    /// The engine's prompts. Only the repair instructions are used here — the narrator and
    /// extractor own theirs — but the client is where a failed response is corrected, so this is
    /// where that text has to be reachable.
    /// </summary>
    private readonly PromptLibrary _prompts;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public OpenRouterClient(StoryWeaverSettings settings, ILlmLog? log = null, HttpClient? httpClient = null)
    {
        _settings = settings;
        _log = log ?? NullLlmLog.Instance;
        _prompts = PromptLibrary.Load();

        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient();

        // Timeouts are applied per request instead, via a linked token in SendAsync. A single
        // HttpClient.Timeout would force narration and extraction to share one budget, and
        // they should not: narration writes paragraphs, extraction returns ~140 tokens and has
        // no business waiting two minutes for them.
        _http.Timeout = Timeout.InfiniteTimeSpan;
    }

    public async Task<LlmResult> CompleteAsync(
        LlmCall call,
        Action<string>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        RoleSettings role = _settings.GetRole(call.Role);

        if (role.ResponseFormat == LlmResponseFormat.JsonSchema && call.Schema is null)
        {
            return LlmResult.Failure(
                $"Role '{call.Role}' is configured for json_schema output but the call supplied " +
                "no schema. Set LlmCall.Schema, or change the role's responseFormat.");
        }

        OpenRouterRequest request = BuildRequest(call, role);
        int timeoutSeconds = role.TimeoutSeconds ?? _settings.Provider.TimeoutSeconds;
        int attempts = 0;
        int timeouts = 0;

        for (int attempt = 1; attempt <= MaxTotalAttempts; attempt++)
        {
            bool isLastAttempt = attempt == MaxTotalAttempts;
            string json = JsonSerializer.Serialize(request, SerializeOptions);
            _log.Prompt(call.Role, json);
            attempts++;

            HttpOutcome outcome;
            try
            {
                outcome = await SendAsync(json, timeoutSeconds, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller-initiated. Distinct from a timeout, and not something to retry.
                throw;
            }
            catch (OperationCanceledException ex)
            {
                // Our own per-request deadline. Retried like any other transient failure: it
                // almost always means the upstream that won this request's routing is
                // overloaded, and the retry is routed independently. Previously this returned
                // immediately with the whole retry budget unused, which turned a recoverable
                // blip into a lost turn.
                timeouts++;

                if (timeouts < MaxTimeoutAttempts && !isLastAttempt)
                {
                    _log.Info(
                        $"Timed out after {timeoutSeconds}s " +
                        $"(attempt {attempt}/{MaxTotalAttempts}). Retrying...");
                    await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                _log.Error($"Request timed out after {timeoutSeconds}s", ex);
                return LlmResult.Failure(
                    $"Request timed out after {timeoutSeconds}s ({timeouts} attempt(s)).", attempts);
            }
            catch (HttpRequestException ex)
            {
                if (!isLastAttempt)
                {
                    _log.Info($"HTTP error (attempt {attempt}/{MaxTotalAttempts}): {ex.Message}. Retrying...");
                    await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                _log.Error("Request failed after retries", ex);
                return LlmResult.Failure($"HTTP error: {ex.Message}", attempts);
            }

            _log.Response(call.Role, outcome.Body);

            if (outcome.ContentFromReasoning)
            {
                _log.Info(
                    $"Provider returned the payload in 'reasoning' with 'content' empty " +
                    $"(model {outcome.Model ?? "?"}). Used the reasoning field.");
            }

            if (outcome.Error is not null)
            {
                if (IsTransient(outcome.StatusCode) && !isLastAttempt)
                {
                    _log.Info($"Transient error (attempt {attempt}/{MaxTotalAttempts}): {outcome.Error}. Retrying...");
                    await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // 4xx and malformed responses are not recoverable by re-asking the same thing.
                return LlmResult.Failure(outcome.Error, attempts);
            }

            string content = outcome.Content ?? string.Empty;

            if (call.Validator is null || call.Validator(content))
            {
                onChunk?.Invoke(content);
                return LlmResult.Success(content, outcome.Model, outcome.Usage, attempts, outcome.Provider);
            }

            if (isLastAttempt)
            {
                _log.Info("Content still failed validation after retries.");
                return LlmResult.Failure(
                    "Model returned content that failed validation after retries.", attempts);
            }

            _log.Info($"Content failed validation (attempt {attempt}/{MaxTotalAttempts}). Requesting repair...");
            request = BuildRepairRequest(request, content);
        }

        return LlmResult.Failure("Exhausted retry budget.", attempts);
    }

    private OpenRouterRequest BuildRequest(LlmCall call, RoleSettings role)
    {
        return new OpenRouterRequest
        {
            Model = role.Model,
            Temperature = role.Temperature,
            MaxTokens = call.MaxTokens ?? role.MaxTokens,
            Messages = [.. call.Messages.Select(m => new WireMessage { Role = m.Role, Content = m.Content })],
            ResponseFormat = BuildResponseFormat(role, call.Schema),

            Provider = BuildProvider(role),
            Reasoning = BuildReasoning(role.Reasoning),
        };
    }

    /// <summary>
    /// The <c>provider</c> block, or null when the role asks for none of it — omitted rather
    /// than sent empty so routing is not needlessly constrained for calls that do not depend
    /// on optional parameters.
    /// </summary>
    private static WireProvider? BuildProvider(RoleSettings role)
    {
        bool pinned = role.ProviderOrder is { Count: > 0 };
        bool excluding = role.ProviderIgnore is { Count: > 0 };

        if (!role.RequireParameters && !pinned && !excluding)
        {
            return null;
        }

        return new WireProvider
        {
            RequireParameters = role.RequireParameters,
            Order = pinned ? [.. role.ProviderOrder!] : null,

            // Pinning that silently falls back to another provider would defeat the only
            // reason to pin: knowing which upstream produced the answer.
            AllowFallbacks = pinned ? false : null,
            Ignore = excluding ? [.. role.ProviderIgnore!] : null,
        };
    }

    private static WireReasoning? BuildReasoning(ReasoningSettings? reasoning)
    {
        if (reasoning is null)
        {
            return null;
        }

        // An object with every field null would still serialize as `"reasoning": {}`, which
        // is a different request from omitting it.
        if (reasoning.Effort is null && reasoning.MaxTokens is null && reasoning.Exclude is null)
        {
            return null;
        }

        return new WireReasoning
        {
            Effort = reasoning.Effort,
            MaxTokens = reasoning.MaxTokens,
            Exclude = reasoning.Exclude,
        };
    }

    private static WireResponseFormat? BuildResponseFormat(RoleSettings role, JsonSchemaSpec? schema)
    {
        switch (role.ResponseFormat)
        {
            case LlmResponseFormat.JsonObject:
                return new WireResponseFormat { Type = "json_object" };

            case LlmResponseFormat.JsonSchema when schema is not null:
                return new WireResponseFormat
                {
                    Type = "json_schema",
                    JsonSchema = new WireJsonSchema
                    {
                        Name = schema.Name,
                        Strict = schema.Strict,
                        Schema = JsonDocument.Parse(schema.Schema).RootElement.Clone(),
                    },
                };

            default:
                return null;
        }
    }

    /// <summary>
    /// Build a follow-up that shows the model its own bad output and asks for a conversion.
    /// Temperature is dropped to zero — this is a mechanical reformat, not a creative task.
    /// </summary>
    private OpenRouterRequest BuildRepairRequest(OpenRouterRequest original, string badContent)
    {
        List<WireMessage> messages = [.. original.Messages];
        messages.Add(new WireMessage { Role = "assistant", Content = Truncate(badContent) });
        messages.Add(new WireMessage { Role = "user", Content = RepairInstruction(badContent) });

        return new OpenRouterRequest
        {
            Model = original.Model,
            MaxTokens = original.MaxTokens,
            Temperature = 0f,
            ResponseFormat = original.ResponseFormat,
            Provider = original.Provider,
            Messages = messages,
        };
    }

    /// <summary>
    /// The corrective message for a repair round-trip, read from <c>prompts/repair.md</c>.
    ///
    /// Two variants because the two failures need different verbs: an empty response has nothing
    /// to convert, so it is asked to produce; a malformed one has its own words above it, so it
    /// is asked to convert them. Both live in the file beside the reasoning for each line, which
    /// is for a human and is deliberately not sent to a model.
    /// </summary>
    private string RepairInstruction(string badContent) =>
        PromptLibrary.Section(
            _prompts.Repair,
            string.IsNullOrWhiteSpace(badContent) ? "empty" : "malformed");

    private static string Truncate(string content)
    {
        const int maxChars = 4000;

        if (string.IsNullOrWhiteSpace(content))
        {
            return "(empty response)";
        }

        content = content.Trim();
        return content.Length <= maxChars
            ? content
            : string.Concat(content.AsSpan(0, maxChars).TrimEnd(), "\n\n[truncated]");
    }

    /// <summary>
    /// One HTTP round trip, bounded by its own deadline.
    ///
    /// The deadline is a linked token rather than <c>HttpClient.Timeout</c> so it can differ
    /// per role. The caller distinguishes the two cancellation sources by checking whether
    /// <paramref name="cancellationToken"/> is the one that fired.
    /// </summary>
    private async Task<HttpOutcome> SendAsync(
        string json,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource deadline =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        cancellationToken = deadline.Token;

        using HttpRequestMessage message = new(HttpMethod.Post, _settings.Provider.BaseUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Provider.ApiKey);

        if (!string.IsNullOrWhiteSpace(_settings.Provider.HttpReferer))
        {
            message.Headers.Add("HTTP-Referer", _settings.Provider.HttpReferer);
        }

        if (!string.IsNullOrWhiteSpace(_settings.Provider.XTitle))
        {
            message.Headers.Add("X-Title", _settings.Provider.XTitle);
        }

        using HttpResponseMessage response =
            await _http.SendAsync(message, cancellationToken).ConfigureAwait(false);

        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        OpenRouterResponse? parsed = null;
        try
        {
            parsed = JsonSerializer.Deserialize<OpenRouterResponse>(body);
        }
        catch (JsonException)
        {
            // Fall through — handled below so the raw body reaches the error message.
        }

        if (!response.IsSuccessStatusCode)
        {
            string detail = parsed?.Error?.Message ?? Summarize(body);
            return new HttpOutcome
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Error = $"HTTP {(int)response.StatusCode}: {detail}",
            };
        }

        if (parsed is null)
        {
            return new HttpOutcome
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Error = $"Response was not valid JSON: {Summarize(body)}",
            };
        }

        if (parsed.Error is not null)
        {
            // A 200 carrying an error object. OpenRouter does this for some provider failures.
            return new HttpOutcome
            {
                StatusCode = parsed.Error.Code,
                Body = body,
                Error = parsed.Error.Message ?? "Provider returned an unspecified error.",
            };
        }

        if (string.IsNullOrEmpty(parsed.Content))
        {
            return new HttpOutcome
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Error = DescribeEmptyContent(parsed),
            };
        }

        return new HttpOutcome
        {
            StatusCode = (int)response.StatusCode,
            Body = body,
            Content = parsed.Content,
            ContentFromReasoning = parsed.ContentCameFromReasoning,
            Model = parsed.Model,
            Provider = parsed.Provider,
            Usage = parsed.Usage is null
                ? null
                : new LlmUsage(
                    parsed.Usage.PromptTokens,
                    parsed.Usage.CompletionTokens,
                    parsed.Usage.TotalTokens,
                    parsed.Usage.CompletionDetails?.ReasoningTokens ?? 0),
        };
    }

    /// <summary>
    /// An empty response has several very different causes that all look identical from the
    /// call site. The expensive one to misdiagnose is a reasoning model exhausting
    /// <c>max_tokens</c> on thinking before it writes a single output token: there is no
    /// error, the finish reason is a bland "length", and the natural conclusion is that the
    /// prompt or schema was rejected. Name it explicitly.
    /// </summary>
    private static string DescribeEmptyContent(OpenRouterResponse parsed)
    {
        int reasoning = parsed.Usage?.CompletionDetails?.ReasoningTokens ?? 0;
        int completion = parsed.Usage?.CompletionTokens ?? 0;

        // Proportional, not >=. The first live sighting reported reasoning 1200 against
        // completion 1202 — the model emitted two tokens of nothing before the ceiling — and
        // an exact-or-greater test missed it by those two tokens, falling through to the bland
        // message this branch exists to avoid.
        if (parsed.FinishReason == "length" && reasoning > 0 && reasoning * 10 >= completion * 9)
        {
            return
                $"Model produced no usable output: {reasoning} of {completion} completion " +
                "tokens went to reasoning before max_tokens was reached. Raise the role's " +
                "maxTokens, or set reasoning.exclude for it — on a reasoning model the budget " +
                "must cover thinking as well as the answer. This is not a schema or prompt " +
                "rejection.";
        }

        if (parsed.FinishReason == "length")
        {
            return $"Model hit max_tokens after {completion} tokens without producing content.";
        }

        return $"Response contained no message content (finish_reason: {parsed.FinishReason ?? "none"}).";
    }

    private static string Summarize(string body)
    {
        const int maxChars = 500;

        if (string.IsNullOrWhiteSpace(body))
        {
            return "(empty body)";
        }

        body = body.Trim();
        return body.Length <= maxChars ? body : body[..maxChars] + "...";
    }

    /// <summary>Rate limits and server errors are worth re-sending; nothing else is.</summary>
    private static bool IsTransient(int statusCode) => statusCode == 429 || statusCode >= 500;

    private static Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        int index = Math.Min(attempt - 1, RetryDelaysMs.Length - 1);
        return Task.Delay(RetryDelaysMs[index], cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    private sealed class HttpOutcome
    {
        public int StatusCode { get; init; }

        public string Body { get; init; } = string.Empty;

        public string? Content { get; init; }

        public string? Model { get; init; }

        /// <summary>Upstream provider that served the request. See <see cref="LlmResult.Provider"/>.</summary>
        public string? Provider { get; init; }

        public LlmUsage? Usage { get; init; }

        /// <summary>The payload arrived in the reasoning field rather than content.</summary>
        public bool ContentFromReasoning { get; init; }

        public string? Error { get; init; }
    }
}
