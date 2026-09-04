using StoryWeaver.Core;
using StoryWeaver.Llm;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;
using StoryWeaver.Llm.Story;

namespace StoryWeaver.Harness;

/// <summary>
/// Scores extraction models against fixed scenarios.
///
/// Exists because a whole session was spent comparing single runs of different prompts and
/// attributing the differences to wording, when the same configuration re-run produced
/// results just as different. Without repeated runs and a score, every change to extraction
/// is unfalsifiable.
///
/// <b>Each scenario runs N times per model and the spread is reported</b>, because
/// consistency is a selection criterion in its own right. OpenRouter routes one model id
/// across several upstream providers, so "how much does this model vary when it lands on a
/// different backend" is a property of the model we have to live with — pinning a provider
/// would hide exactly the thing worth knowing, and would not survive moving to another proxy.
///
/// <b>Scores, does not print.</b> The result is a <see cref="EvalReport"/> a client renders,
/// and live progress goes through an <see cref="IEvalObserver"/> the client supplies. The eval
/// is going into the UI, so its output is UI-bound and must be separable from how it is drawn.
/// </summary>
public static class ExtractionEval
{
    public static async Task<EvalReport> RunAsync(
        StoryWeaverSettings settings,
        string[] models,
        int runs,
        IReadOnlyList<EvalScenario> scenarios,
        string[]? providers = null,
        IEvalObserver? observer = null)
    {
        FileLlmLog log = new(settings.Logging);

        // A null entry means "let routing decide", which is what play does.
        string?[] targets = providers is { Length: > 0 } ? [.. providers] : [null];

        observer?.Starting(new EvalPlan(
            ScenarioCount: scenarios.Count,
            ModelCount: models.Length,
            Providers: providers is { Length: > 0 } ? [.. providers] : [],
            RunsPer: runs,
            TotalCalls: scenarios.Count * models.Length * targets.Length * runs,
            LogPath: log.FilePath));

        List<ModelReport> reports = [];

        foreach (string model in models)
        {
            foreach (string? provider in targets)
            {
                reports.Add(await ScoreModelAsync(settings, log, model, runs, scenarios, provider, observer)
                    .ConfigureAwait(false));
            }
        }

        return new EvalReport
        {
            Models = reports,
            PromptFingerprint = PromptLibrary.Load().Fingerprint,
        };
    }

    private static async Task<ModelReport> ScoreModelAsync(
        StoryWeaverSettings settings,
        ILlmLog log,
        string model,
        int runs,
        IReadOnlyList<EvalScenario> scenarios,
        string? provider,
        IEvalObserver? observer)
    {
        string label = provider is null ? model : $"{model} via {provider}";
        observer?.ModelStarting(label);

        StoryWeaverSettings scoped = WithExtractionModel(settings, model, provider);
        using OpenRouterClient client = new(scoped, log);
        IStateExtractor extractor = new LlmStateExtractor(client, PromptLibrary.Load());

        ModelReport report = new(label);

        foreach (EvalScenario scenario in scenarios)
        {
            ScenarioReport scenarioReport = new(scenario.Name);

            for (int run = 0; run < runs; run++)
            {
                RunScore score = await ScoreRunAsync(extractor, scenario).ConfigureAwait(false);
                scenarioReport.Runs.Add(score);
                observer?.RunScored(scenario.Name, run, score);
            }

            report.Scenarios.Add(scenarioReport);
            observer?.ScenarioScored(scenarioReport);
        }

        observer?.ModelScored(report);
        return report;
    }

    private static async Task<RunScore> ScoreRunAsync(IStateExtractor extractor, EvalScenario scenario)
    {
        WorldState world = scenario.World();
        LoreBook lore = scenario.LoreBook();
        string context = ContextAssembler.ForExtraction(world, lore);

        ExtractionResult result;
        try
        {
            result = await extractor
                .ExtractAsync(context, scenario.PlayerInput, scenario.Narration)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new RunScore { Failed = true, Note = ex.Message };
        }

        ValidationOutcome validation = DeltaValidator.Validate(world, result.Deltas, lore);

        // Required and forbidden are measured at deliberately different points.
        //
        // Required is scored AFTER validation: a delta the validator rejects never reaches
        // canon, so crediting it would score a model for output the world never sees.
        //
        // Forbidden is scored on the RAW output, before validation. Scoring it after was a
        // real mistake in the first version — models emitted location_introduced for a place
        // already in canon, the validator rejected it, and the scoreboard proudly reported
        // "forbidden 0". That reads as "the model behaved" when it means "the validator
        // saved us", and it is precisely why the eval looked clean while live play was full
        // of re-introductions. The validator is a safety net, not evidence of good output.
        List<StateDelta> effective = [.. validation.Accepted, .. validation.NoOps];

        // Apply what was accepted, so outcome rules can be judged against the world the turn
        // would actually have produced. Safe to mutate: every run builds a fresh world.
        DeltaApplier.Apply(world, validation.Accepted);

        IReadOnlyList<StateRule> expected = scenario.Expected ?? [];
        List<string> unmetOutcomes = [.. expected.Where(r => !r.Holds(world)).Select(r => r.Description)];

        return new RunScore
        {
            RequiredHit = scenario.Required.Count(rule => effective.Any(rule.Matches))
                          + (expected.Count - unmetOutcomes.Count),
            RequiredTotal = scenario.Required.Count + expected.Count,
            ForbiddenHit = scenario.Forbidden.Count(rule => result.Deltas.Any(rule.Matches)),
            Rejected = validation.Rejected.Count,
            PromptTokens = result.Usage?.PromptTokens ?? 0,
            CompletionTokens = result.Usage?.CompletionTokens ?? 0,
            ReasoningTokens = result.Usage?.ReasoningTokens ?? 0,
            MissingRules =
            [
                .. scenario.Required.Where(r => !effective.Any(r.Matches)).Select(r => r.Description),
                .. unmetOutcomes,
            ],
            ViolatedRules = [.. scenario.Forbidden.Where(r => result.Deltas.Any(r.Matches)).Select(r => r.Description)],
            Proposed = result.Deltas,
            RejectedDeltas = [.. validation.Rejected.Select(r => r.Delta)],
            Provider = result.Provider,
        };
    }

    /// <summary>
    /// A copy of the settings with only the extraction role's model swapped. Copied rather
    /// than mutated so a failure partway through a sweep cannot leave the caller's settings
    /// pointing at whichever model happened to be under test.
    /// </summary>
    private static StoryWeaverSettings WithExtractionModel(
        StoryWeaverSettings settings,
        string model,
        string? provider = null)
    {
        RoleSettings existing = settings.GetRole(LlmRole.Extraction);

        StoryWeaverSettings copy = new()
        {
            Provider = settings.Provider,
            Logging = settings.Logging,
            Roles = new Dictionary<string, RoleSettings>(settings.Roles, StringComparer.OrdinalIgnoreCase),
        };

        copy.Roles["extraction"] = new RoleSettings
        {
            Model = model,
            Temperature = existing.Temperature,
            MaxTokens = existing.MaxTokens,
            RequireParameters = existing.RequireParameters,
            ResponseFormat = existing.ResponseFormat,
            Reasoning = existing.Reasoning,
            TimeoutSeconds = existing.TimeoutSeconds,
            ProviderIgnore = existing.ProviderIgnore,

            // Sampling one upstream at a time. Only the eval does this — see WireProvider.Order.
            ProviderOrder = provider is null ? null : [provider],
        };

        return copy;
    }
}
