using StoryWeaver.Core;

namespace StoryWeaver.Harness;

/// <summary>
/// The result of an extraction eval — pure data, no formatting.
///
/// The eval is UI-bound now: the console renders it today and the window will render it later,
/// each its own way. So the runner returns this and a client draws it, rather than printing.
/// The numbers a client would otherwise recompute — required rate, forbidden-per-run, the
/// per-provider split — live here as computed properties, because they are measurements, not
/// presentation. What stays with the client is only the wording: table layout, labels, and how
/// a delta reads on screen.
/// </summary>
public sealed class EvalReport
{
    public required IReadOnlyList<ModelReport> Models { get; init; }

    /// <summary>
    /// The prompt set these numbers were measured against. Printed beside the score because a
    /// prompt file can be edited between two runs leaving no trace in the result — so a number
    /// without the prompt it was measured against is not a measurement.
    /// </summary>
    public required string PromptFingerprint { get; init; }
}

/// <summary>
/// What the run is about to do, handed to an observer before any calls are made, so a client
/// can show the shape and cost of the sweep up front.
/// </summary>
public sealed record EvalPlan(
    int ScenarioCount,
    int ModelCount,
    IReadOnlyList<string> Providers,
    int RunsPer,
    int TotalCalls,
    string LogPath);

/// <summary>
/// One model (or one model-via-provider) scored across every scenario. The label carries the
/// provider suffix when the sweep pinned one, because runs of the "same model" on different
/// upstreams are not interchangeable.
/// </summary>
public sealed class ModelReport(string label)
{
    public string Label { get; } = label;

    public List<ScenarioReport> Scenarios { get; } = [];

    private IEnumerable<RunScore> AllRuns => Scenarios.SelectMany(s => s.Runs);

    /// <summary>Share of must-have deltas produced across every run. Higher is better.</summary>
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

    /// <summary>
    /// Clean-run rate split by upstream provider. One model id is routed across several
    /// providers and they do not behave the same; an average over a mix the sweep did not
    /// choose and cannot reproduce is how a scenario reads 7/7 one day and 0/7 the next with
    /// nothing changed. Ordered by how many runs each provider served.
    /// </summary>
    public IReadOnlyList<ProviderStat> ProviderBreakdown =>
    [
        .. AllRuns
            .GroupBy(r => r.Provider ?? "(unreported)")
            .OrderByDescending(g => g.Count())
            .Select(g => new ProviderStat(
                g.Key,
                g.Count(),
                g.Count(r => r.Clean),
                g.Average(r => (double)r.ForbiddenHit))),
    ];

    private double Average(Func<RunScore, int> select)
    {
        List<RunScore> runs = [.. AllRuns];
        return runs.Count == 0 ? 0 : runs.Average(r => (double)select(r));
    }
}

/// <summary>One scenario's runs under one model, with the aggregates a client draws from.</summary>
public sealed class ScenarioReport(string name)
{
    public string Name { get; } = name;

    public List<RunScore> Runs { get; } = [];

    public int RequiredHit => Runs.Sum(r => r.RequiredHit);

    public int RequiredTotal => Runs.Sum(r => r.RequiredTotal);

    public int ForbiddenHit => Runs.Sum(r => r.ForbiddenHit);

    public int FailedCount => Runs.Count(r => r.Failed);

    /// <summary>
    /// Distinct problems seen, each with how many runs hit it — the detail that says whether a
    /// failure is systematic or occasional. Structured rather than pre-formatted, so a client
    /// decides how a missed rule, a violated rule and an error each read.
    /// </summary>
    public IReadOnlyList<EvalProblem> Problems
    {
        get
        {
            List<EvalProblem> problems = [];

            foreach (IGrouping<string, string> group in Runs
                .SelectMany(r => r.MissingRules)
                .GroupBy(x => x))
            {
                problems.Add(new EvalProblem(EvalProblemKind.Missed, group.Key, group.Count(), Runs.Count));
            }

            foreach (IGrouping<string, string> group in Runs
                .SelectMany(r => r.ViolatedRules)
                .GroupBy(x => x))
            {
                problems.Add(new EvalProblem(EvalProblemKind.Violated, group.Key, group.Count(), Runs.Count));
            }

            foreach (RunScore run in Runs.Where(r => r.Failed))
            {
                problems.Add(new EvalProblem(EvalProblemKind.Error, run.Note ?? "(no detail)", 1, Runs.Count));
            }

            return problems;
        }
    }
}

/// <summary>The kind of thing that went wrong, so a client can mark each differently.</summary>
public enum EvalProblemKind
{
    /// <summary>A required rule the model did not satisfy.</summary>
    Missed,

    /// <summary>A forbidden rule the model triggered.</summary>
    Violated,

    /// <summary>The call itself failed.</summary>
    Error,
}

/// <summary>A distinct problem and how many of a scenario's runs hit it.</summary>
public sealed record EvalProblem(EvalProblemKind Kind, string Description, int HitRuns, int TotalRuns);

/// <summary>Clean-run count for one upstream provider, within one model's runs.</summary>
public sealed record ProviderStat(string Provider, int Runs, int Clean, double ForbiddenPerRun);

/// <summary>
/// One scored run — everything the client needs and nothing formatted. The scoring fields are
/// set by the runner; <see cref="Proposed"/> and <see cref="RejectedDeltas"/> are kept for the
/// delta dump a client shows on request.
/// </summary>
public sealed class RunScore
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

    /// <summary>Everything the model proposed, before validation. Kept for the delta dump;
    /// scoring uses the fields above.</summary>
    public IReadOnlyList<StateDelta> Proposed { get; init; } = [];

    public IReadOnlyList<StateDelta> RejectedDeltas { get; init; } = [];

    /// <summary>Upstream provider that served this run. The whole point of recording it is that
    /// runs of the "same model" are not interchangeable.</summary>
    public string? Provider { get; init; }

    /// <summary>A run counts as clean when it produced every required delta and violated
    /// nothing. Used for the per-provider breakdown.</summary>
    public bool Clean => !Failed && RequiredHit == RequiredTotal && ForbiddenHit == 0;
}

/// <summary>
/// Live progress from a running eval, so a client can show the sweep as it happens across
/// minutes of real API calls. The final <see cref="EvalReport"/> is returned from the run; this
/// is only the running commentary. A caller that wants no live output passes none.
/// </summary>
public interface IEvalObserver
{
    /// <summary>Before any calls — the shape and cost of the sweep.</summary>
    void Starting(EvalPlan plan);

    /// <summary>A model (or model-via-provider) is about to be scored.</summary>
    void ModelStarting(string label);

    /// <summary>One run has been scored. Fires for every run, in order.</summary>
    void RunScored(string scenarioName, int runIndex, RunScore score);

    /// <summary>A scenario's runs are all in.</summary>
    void ScenarioScored(ScenarioReport scenario);

    /// <summary>A model's scenarios are all in.</summary>
    void ModelScored(ModelReport model);
}
