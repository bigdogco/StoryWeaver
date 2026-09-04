using StoryWeaver.Harness;

namespace StoryWeaver.Cli;

/// <summary>
/// Draws the final <see cref="EvalReport"/> on the console — the summary table, the per-model
/// detail, and the per-provider breakdown.
///
/// Every number here is read straight off the report; nothing is scored. This is the client half
/// of the seam: the Harness measures, the CLI decides how it reads. A window renders the same
/// report its own way.
/// </summary>
internal static class EvalRenderer
{
    public static void RenderSummary(EvalReport report)
    {
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("SUMMARY");
        Console.WriteLine(new string('=', 78));

        // Printed beside the score because prompts live in files, and a file can be edited
        // between two runs leaving no trace in the result — so a number without the prompt it
        // was measured against is not a measurement.
        Console.WriteLine($"prompts   {report.PromptFingerprint}");
        Console.WriteLine();
        Console.WriteLine($"{"model",-34} {"required",9} {"forbidden",10} {"rejects",8} {"tokens",8}");
        Console.WriteLine(new string('-', 78));

        foreach (ModelReport model in report.Models.OrderByDescending(m => m.RequiredRate))
        {
            Console.WriteLine(
                $"{EvalFormat.Shorten(model.Label),-34} " +
                $"{model.RequiredRate,8:P0} " +
                $"{model.ForbiddenPerRun,10:F2} " +
                $"{model.RejectsPerRun,8:F2} " +
                $"{model.AverageCompletionTokens,8:F0}");
        }

        Console.WriteLine();
        Console.WriteLine("required  = share of must-have deltas produced (higher is better)");
        Console.WriteLine("forbidden = must-not-happen deltas per run    (lower is better)");
        Console.WriteLine("rejects   = deltas the validator threw out    (lower is better)");
        Console.WriteLine("tokens    = mean completion tokens per call   (cost proxy)");
        Console.WriteLine();

        foreach (ModelReport model in report.Models)
        {
            Console.WriteLine($"--- {model.Label} ---");

            foreach (ScenarioReport scenario in model.Scenarios)
            {
                Console.WriteLine($"  {EvalFormat.ScenarioLine(scenario)}");

                foreach (EvalProblem problem in scenario.Problems)
                {
                    Console.WriteLine($"      {EvalFormat.Problem(problem)}");
                }
            }

            RenderProviderBreakdown(model);
            Console.WriteLine();
        }
    }

    private static void RenderProviderBreakdown(ModelReport model)
    {
        IReadOnlyList<ProviderStat> breakdown = model.ProviderBreakdown;

        if (breakdown.Count == 0)
        {
            return;
        }

        Console.WriteLine("  by provider:");

        foreach (ProviderStat stat in breakdown)
        {
            Console.WriteLine(
                $"    {stat.Provider,-22} {stat.Runs,3} run(s), " +
                $"clean {stat.Clean}/{stat.Runs}, forbidden/run {stat.ForbiddenPerRun:F2}");
        }
    }
}
