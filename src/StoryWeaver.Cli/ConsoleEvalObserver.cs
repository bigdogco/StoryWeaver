using StoryWeaver.Core;
using StoryWeaver.Harness;

namespace StoryWeaver.Cli;

/// <summary>
/// The console's view of a running eval — the live commentary the Harness reports as it scores.
///
/// This is client code: the Harness scores and hands back numbers, and how the sweep *looks*
/// while it runs is the CLI's business. A window would implement <see cref="IEvalObserver"/>
/// differently — a progress bar, a filling table — against the same events.
/// </summary>
internal sealed class ConsoleEvalObserver(bool showDeltas) : IEvalObserver
{
    public void Starting(EvalPlan plan)
    {
        string providers = plan.Providers.Count > 0 ? string.Join("/", plan.Providers) : "routed";

        Console.WriteLine(
            $"Scenarios: {plan.ScenarioCount}, models: {plan.ModelCount}, " +
            $"providers: {providers}, runs each: {plan.RunsPer}");
        Console.WriteLine($"Calls: {plan.TotalCalls}");
        Console.WriteLine($"Logging to {plan.LogPath}");
        Console.WriteLine();
    }

    public void ModelStarting(string label) => Console.WriteLine($"=== {label} ===");

    /// <summary>
    /// On <c>--show-deltas</c>, dump what the model actually proposed. A score says whether a
    /// rule fired; on an open design question the interesting part is what it did *instead*,
    /// which no pass/fail number can show.
    /// </summary>
    public void RunScored(string scenarioName, int runIndex, RunScore score)
    {
        if (!showDeltas)
        {
            return;
        }

        Console.WriteLine($"    [{scenarioName} run {runIndex + 1}]");

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
            Console.WriteLine($"      {mark} {EvalFormat.Delta(delta)}");
        }
    }

    public void ScenarioScored(ScenarioReport scenario) =>
        Console.WriteLine($"  {EvalFormat.ScenarioLine(scenario)}");

    public void ModelScored(ModelReport model) => Console.WriteLine();
}
