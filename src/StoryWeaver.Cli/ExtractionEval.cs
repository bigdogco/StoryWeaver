using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;
using StoryWeaver.Llm.Story;

namespace StoryWeaver.Cli;

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
/// </summary>
internal static class ExtractionEval
{
    public static async Task<int> RunAsync(
        StoryWeaverSettings settings,
        string[] models,
        int runs,
        IReadOnlyList<EvalScenario> scenarios,
        bool showDeltas = false,
        string[]? providers = null)
    {
        FileLlmLog log = new(settings.Logging);

        // A null entry means "let routing decide", which is what play does.
        string?[] targets = providers is { Length: > 0 } ? [.. providers] : [null];

        Console.WriteLine(
            $"Scenarios: {scenarios.Count}, models: {models.Length}, " +
            $"providers: {(providers is { Length: > 0 } ? string.Join("/", providers) : "routed")}, " +
            $"runs each: {runs}");
        Console.WriteLine($"Calls: {scenarios.Count * models.Length * targets.Length * runs}");
        Console.WriteLine($"Logging to {log.FilePath}");
        Console.WriteLine();

        List<ModelReport> reports = [];

        foreach (string model in models)
        {
            foreach (string? provider in targets)
            {
                reports.Add(await ScoreModelAsync(settings, log, model, runs, scenarios, showDeltas, provider)
                    .ConfigureAwait(false));
            }
        }

        PrintSummary(reports);
        return 0;
    }

    private static async Task<ModelReport> ScoreModelAsync(
        StoryWeaverSettings settings,
        ILlmLog log,
        string model,
        int runs,
        IReadOnlyList<EvalScenario> scenarios,
        bool showDeltas,
        string? provider)
    {
        string label = provider is null ? model : $"{model} via {provider}";
        Console.WriteLine($"=== {label} ===");

        StoryWeaverSettings scoped = WithExtractionModel(settings, model, provider);
        using OpenRouterClient client = new(scoped, log);
        IStateExtractor extractor = new LlmStateExtractor(client);

        ModelReport report = new(label);

        foreach (EvalScenario scenario in scenarios)
        {
            ScenarioReport scenarioReport = new(scenario.Name);

            for (int run = 0; run < runs; run++)
            {
                RunScore score = await ScoreRunAsync(extractor, scenario).ConfigureAwait(false);
                scenarioReport.Runs.Add(score);

                if (showDeltas)
                {
                    PrintDeltas(scenario.Name, run, score);
                }
            }

            report.Scenarios.Add(scenarioReport);
            Console.WriteLine($"  {scenarioReport.Describe()}");
        }

        Console.WriteLine();
        return report;
    }

    /// <summary>
    /// Dump what the model actually proposed. A score says whether a rule fired; on an open
    /// design question the interesting part is what it did *instead*, which no pass/fail
    /// number can show.
    /// </summary>
    private static void PrintDeltas(string scenario, int run, RunScore score)
    {
        Console.WriteLine($"    [{scenario} run {run + 1}]");

        if (score.Failed)
        {
            Console.WriteLine($"      ERROR: {score.Note}");
            return;
        }

        if (score.Proposed.Count == 0)
        {
            Console.WriteLine("      (no deltas proposed)");
            return;
        }

        foreach (StateDelta delta in score.Proposed)
        {
            string mark = score.RejectedDeltas.Contains(delta) ? "REJECTED" : "ok      ";
            Console.WriteLine($"      {mark} {Describe(delta)}");
        }
    }

    private static string Describe(StateDelta delta) => delta switch
    {
        CharacterMoved d => $"character_moved     {d.CharacterId} -> {d.ToLocationId}",
        PlayerMoved d => $"player_moved        -> {d.ToLocationId}",
        StatusChanged d => $"status_changed      {d.CharacterId} = {d.Status}",
        MoodChanged d => $"mood_changed        {d.CharacterId} = {d.Mood}",
        RelationshipChanged d => $"relationship_changed {d.CharacterId} = {d.Standing} ({d.Summary})",
        FactEstablished d => $"fact_established    {d.FactId}: {d.Text}",
        FactLearned d => $"fact_learned        {d.CharacterId} <- {d.FactId}",
        CharacterIntroduced d => $"character_introduced {d.CharacterId} ({d.Name}) @ {d.LocationId}",
        CharacterRenamed d => $"character_renamed   {d.CharacterId} -> {d.Name}",
        LocationIntroduced d => $"location_introduced {d.LocationId} ({d.Name})",
        _ => delta.GetType().Name,
    };

    private static async Task<RunScore> ScoreRunAsync(IStateExtractor extractor, EvalScenario scenario)
    {
        WorldState world = scenario.World();
        string context = ContextAssembler.ForExtraction(world);

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

        ValidationOutcome validation = DeltaValidator.Validate(world, result.Deltas);

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

    private static void PrintSummary(List<ModelReport> reports)
    {
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("SUMMARY");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine();
        Console.WriteLine($"{"model",-34} {"required",9} {"forbidden",10} {"rejects",8} {"tokens",8}");
        Console.WriteLine(new string('-', 78));

        foreach (ModelReport report in reports.OrderByDescending(r => r.RequiredRate))
        {
            Console.WriteLine(
                $"{Shorten(report.Model),-34} " +
                $"{report.RequiredRate,8:P0} " +
                $"{report.ForbiddenPerRun,10:F2} " +
                $"{report.RejectsPerRun,8:F2} " +
                $"{report.AverageCompletionTokens,8:F0}");
        }

        Console.WriteLine();
        Console.WriteLine("required  = share of must-have deltas produced (higher is better)");
        Console.WriteLine("forbidden = must-not-happen deltas per run    (lower is better)");
        Console.WriteLine("rejects   = deltas the validator threw out    (lower is better)");
        Console.WriteLine("tokens    = mean completion tokens per call   (cost proxy)");
        Console.WriteLine();

        foreach (ModelReport report in reports)
        {
            Console.WriteLine($"--- {report.Model} ---");

            foreach (ScenarioReport scenario in report.Scenarios)
            {
                Console.WriteLine($"  {scenario.Describe()}");

                foreach (string problem in scenario.Problems())
                {
                    Console.WriteLine($"      {problem}");
                }
            }

            PrintProviderBreakdown(report);
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Clean-run rate split by upstream provider.
    ///
    /// One model id is routed across several providers and they do not behave the same. A
    /// scored sweep that does not show this reports an average over a mix it did not choose
    /// and cannot reproduce — which is how a scenario can read 7/7 one day and 0/7 the next
    /// with nothing in the repository having changed.
    /// </summary>
    private static void PrintProviderBreakdown(ModelReport report)
    {
        List<RunScore> runs = [.. report.Scenarios.SelectMany(s => s.Runs)];

        if (runs.Count == 0)
        {
            return;
        }

        Console.WriteLine("  by provider:");

        foreach (IGrouping<string, RunScore> group in runs
            .GroupBy(r => r.Provider ?? "(unreported)")
            .OrderByDescending(g => g.Count()))
        {
            int clean = group.Count(r => r.Clean);
            double forbidden = group.Average(r => (double)r.ForbiddenHit);

            Console.WriteLine(
                $"    {group.Key,-22} {group.Count(),3} run(s), " +
                $"clean {clean}/{group.Count()}, forbidden/run {forbidden:F2}");
        }
    }

    private static string Shorten(string model) =>
        model.Length <= 34 ? model : "…" + model[^33..];

    private sealed class ModelReport(string model)
    {
        public string Model { get; } = model;

        public List<ScenarioReport> Scenarios { get; } = [];

        private IEnumerable<RunScore> AllRuns => Scenarios.SelectMany(s => s.Runs);

        public double RequiredRate
        {
            get
            {
                int total = AllRuns.Sum(r => r.RequiredTotal);
                return total == 0 ? 1 : (double)AllRuns.Sum(r => r.RequiredHit) / total;
            }
        }

        public double ForbiddenPerRun => Average(r => r.ForbiddenHit);

        public double RejectsPerRun => Average(r => r.Rejected);

        public double AverageCompletionTokens => Average(r => r.CompletionTokens);

        private double Average(Func<RunScore, int> select)
        {
            List<RunScore> runs = [.. AllRuns];
            return runs.Count == 0 ? 0 : runs.Average(r => (double)select(r));
        }
    }

    private sealed class ScenarioReport(string name)
    {
        public string Name { get; } = name;

        public List<RunScore> Runs { get; } = [];

        public string Describe()
        {
            int required = Runs.Sum(r => r.RequiredHit);
            int requiredTotal = Runs.Sum(r => r.RequiredTotal);
            int forbidden = Runs.Sum(r => r.ForbiddenHit);
            int failed = Runs.Count(r => r.Failed);

            string requiredPart = requiredTotal == 0 ? "n/a" : $"{required}/{requiredTotal}";
            string failedPart = failed > 0 ? $", {failed} call(s) failed" : string.Empty;

            return $"{Name,-16} required {requiredPart,-7} forbidden {forbidden}{failedPart}";
        }

        /// <summary>Distinct problems seen, with how many runs hit each — the detail that
        /// says whether a failure is systematic or occasional.</summary>
        public IEnumerable<string> Problems()
        {
            foreach (IGrouping<string, string> group in Runs
                .SelectMany(r => r.MissingRules.Select(m => $"MISSED: {m}"))
                .Concat(Runs.SelectMany(r => r.ViolatedRules.Select(v => $"DID:    {v}")))
                .GroupBy(x => x))
            {
                yield return $"{group.Key} ({group.Count()}/{Runs.Count})";
            }

            foreach (RunScore run in Runs.Where(r => r.Failed))
            {
                yield return $"ERROR:  {run.Note}";
            }
        }
    }

    private sealed class RunScore
    {
        public bool Failed { get; init; }

        public string? Note { get; init; }

        public int RequiredHit { get; init; }

        public int RequiredTotal { get; init; }

        public int ForbiddenHit { get; init; }

        public int Rejected { get; init; }

        public int PromptTokens { get; init; }

        public int CompletionTokens { get; init; }

        public int ReasoningTokens { get; init; }

        public IReadOnlyList<string> MissingRules { get; init; } = [];

        public IReadOnlyList<string> ViolatedRules { get; init; } = [];

        /// <summary>Everything the model proposed, before validation. Kept for
        /// <c>--show-deltas</c>; scoring uses the fields above.</summary>
        public IReadOnlyList<StateDelta> Proposed { get; init; } = [];

        public IReadOnlyList<StateDelta> RejectedDeltas { get; init; } = [];

        /// <summary>Upstream provider that served this run. The whole point of recording it is
        /// that runs of the "same model" are not interchangeable.</summary>
        public string? Provider { get; init; }

        /// <summary>A run counts as clean when it produced every required delta and violated
        /// nothing. Used only for the per-provider breakdown.</summary>
        public bool Clean => !Failed && RequiredHit == RequiredTotal && ForbiddenHit == 0;
    }
}
