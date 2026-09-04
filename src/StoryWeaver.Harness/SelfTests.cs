using StoryWeaver.Llm.OpenRouter;

namespace StoryWeaver.Harness;

/// <summary>
/// The one entry point behind <c>--selftest</c>.
///
/// The suites moved out of the CLI when it became a thin client; a client should not have to
/// know the order they run in or how their exit codes combine. So the harness owns that, and
/// the CLI calls this.
///
/// <b>Console is fine here.</b> These are dev-only pass/fail checks that no game UI renders —
/// unlike the eval, whose output is UI-bound and therefore returns data for a client to draw.
/// The rule is "output a UI renders must be separable from rendering", and self-test output is
/// not that.
///
/// <see cref="ResponseSelfTest"/> is the one suite that stays in <c>Llm</c> — its wire types are
/// internal and should remain so — but it runs from here like the rest, so one call still covers
/// everything.
/// </summary>
public static class SelfTests
{
    /// <summary>
    /// Runs every suite and returns the worst exit code, so one failure cannot be hidden by the
    /// others passing. Order and spacing match what <c>--selftest</c> printed before the move.
    /// </summary>
    public static int RunAll()
    {
        int json = JsonSelfTest.Run();
        Console.WriteLine();
        int lore = LoreSelfTest.Run();
        Console.WriteLine();
        int wire = ResponseSelfTest.Run();
        Console.WriteLine();
        int reroll = RerollSelfTest.Run();
        Console.WriteLine();
        int authoring = AuthoringSelfTest.Run();
        Console.WriteLine();
        int refresh = CanonRefreshSelfTest.Run();
        Console.WriteLine();
        int session = StorySessionSelfTest.Run();
        Console.WriteLine();
        int opener = SessionOpenerSelfTest.Run();
        Console.WriteLine();
        int edits = CanonEditsSelfTest.Run();

        return new[] { json, lore, wire, reroll, authoring, refresh, session, opener, edits }.Max();
    }
}
