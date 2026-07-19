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

    private static readonly int[] RetryDelaysMs = [1000, 3000, 5000];

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly StoryWeaverSettings _settings;
    private readonly ILlmLog _log;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public OpenRouterClient(StoryWeaverSettings settings, ILlmLog? log = null, HttpClient? httpClient = null)
    {
        _settings = settings;
        _log = log ?? NullLlmLog.Instance;

        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(settings.Provider.TimeoutSeconds);
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
        int attempts = 0;

        for (int attempt = 1; attempt <= MaxTotalAttempts; attempt++)
        {
            bool isLastAttempt = attempt == MaxTotalAttempts;
            string json = JsonSerializer.Serialize(request, SerializeOptions);
            _log.Prompt(call.Role, json);
            attempts++;

            HttpOutcome outcome;
            try
            {
                outcome = await SendAsync(json, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller-initiated. Distinct from a timeout, and not something to retry.
                throw;
            }
            catch (TaskCanceledException ex)
            {
                // HttpClient surfaces its own timeout as TaskCanceledException.
                _log.Error("Request timed out", ex);
                return LlmResult.Failure(
                    $"Request timed out after {_settings.Provider.TimeoutSeconds}s.", attempts);
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
                return LlmResult.Success(content, outcome.Model, outcome.Usage, attempts);
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

            // Only sent when the role asks for it. Omitted otherwise so we do not needlessly
            // constrain routing for calls that do not depend on optional parameters.
            Provider = role.RequireParameters ? new WireProvider { RequireParameters = true } : null,
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
    private static OpenRouterRequest BuildRepairRequest(OpenRouterRequest original, string badContent)
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

    private static string RepairInstruction(string badContent)
    {
        if (string.IsNullOrWhiteSpace(badContent))
        {
            return
                "Your previous response was empty. Do not continue the scene, do not add new " +
                "narration, and do not explain the mistake. Return the required JSON object now. " +
                "The first character must be `{` and the last must be `}`. Return JSON only.";
        }

        return
            "Your previous response failed validation because it was not in the required JSON " +
            "shape. Do not continue the scene, do not add new narration, and do not explain the " +
            "mistake. Convert your previous answer into the requested JSON object now. " +
            "The first character must be `{` and the last must be `}`. Return JSON only.";
    }

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

    private async Task<HttpOutcome> SendAsync(string json, CancellationToken cancellationToken)
    {
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

        if (parsed.Content is null)
        {
            return new HttpOutcome
            {
                StatusCode = (int)response.StatusCode,
                Body = body,
                Error = "Response contained no message content.",
            };
        }

        return new HttpOutcome
        {
            StatusCode = (int)response.StatusCode,
            Body = body,
            Content = parsed.Content,
            Model = parsed.Model,
            Usage = parsed.Usage is null
                ? null
                : new LlmUsage(parsed.Usage.PromptTokens, parsed.Usage.CompletionTokens, parsed.Usage.TotalTokens),
        };
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

        public LlmUsage? Usage { get; init; }

        public string? Error { get; init; }
    }
}
