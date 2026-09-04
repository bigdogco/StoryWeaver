using StoryWeaver.Core;
using StoryWeaver.Harness;

namespace StoryWeaver.Cli;

/// <summary>
/// How an eval reads on the console — the wording the Harness deliberately does not own.
///
/// The Harness scores and returns numbers; turning a scenario's tallies into a line, a delta
/// into text, and a long model id into something that fits a column is presentation, and lives
/// with the client. Shared here because the live observer and the final summary format the same
/// things and must format them the same way.
/// </summary>
internal static class EvalFormat
{
    /// <summary>The one-line verdict for a scenario: required hits, forbidden count, any failures.</summary>
    public static string ScenarioLine(ScenarioReport scenario)
    {
        string requiredPart = scenario.RequiredTotal == 0
            ? "n/a"
            : $"{scenario.RequiredHit}/{scenario.RequiredTotal}";

        string failedPart = scenario.FailedCount > 0
            ? $", {scenario.FailedCount} call(s) failed"
            : string.Empty;

        return $"{scenario.Name,-16} required {requiredPart,-7} forbidden {scenario.ForbiddenHit}{failedPart}";
    }

    /// <summary>A problem line, prefixed by what kind of problem it is.</summary>
    public static string Problem(EvalProblem problem) => problem.Kind switch
    {
        EvalProblemKind.Missed => $"MISSED: {problem.Description} ({problem.HitRuns}/{problem.TotalRuns})",
        EvalProblemKind.Violated => $"DID:    {problem.Description} ({problem.HitRuns}/{problem.TotalRuns})",
        EvalProblemKind.Error => $"ERROR:  {problem.Description}",
        _ => problem.Description,
    };

    /// <summary>Trim a model id to the summary column, keeping the tail — the part that differs.</summary>
    public static string Shorten(string model) =>
        model.Length <= 34 ? model : "…" + model[^33..];

    /// <summary>One line for a proposed delta, for the <c>--show-deltas</c> dump.</summary>
    public static string Delta(StateDelta delta) => delta switch
    {
        CharacterMoved d => $"character_moved     {d.CharacterId} -> {d.ToLocationId}",
        PlayerMoved d => $"player_moved        -> {d.ToLocationId}",
        StatusChanged d => $"status_changed      {d.CharacterId} = {d.Status}",
        MoodChanged d => $"mood_changed        {d.CharacterId} = {d.Mood}",
        RelationshipChanged d => $"relationship_changed {d.CharacterId} = {d.Standing} ({d.Summary})",
        FactEstablished d => $"fact_established    {d.FactId}: {d.Text}" + (d.SourceId is null ? "" : $"  [said by {d.SourceId}]"),
        FactLearned d => $"fact_learned        {d.CharacterId} <- {d.FactId}",
        CharacterIntroduced d => $"character_introduced {d.CharacterId} ({d.Name}) @ {d.LocationId}",
        CharacterRenamed d => $"character_renamed   {d.CharacterId} -> {d.Name}",
        LocationIntroduced d => $"location_introduced {d.LocationId} ({d.Name})",
        ItemIntroduced d => $"item_introduced     {d.ItemId} ({d.Name}) @ {d.LocationId ?? d.HolderId}",
        ItemMoved d => $"item_moved          {d.ItemId} -> {d.ToLocationId ?? d.ToHolderId}",
        ItemRenamed d => $"item_renamed        {d.ItemId} -> {d.Name}",
        ItemStatusChanged d => $"item_status_changed {d.ItemId} = {d.Status}",
        LocationStatusChanged d => $"location_status_changed {d.LocationId} = {d.Status}",
        ItemRevealedAsCharacter d => $"item_revealed_as_character {d.ItemId} is {d.Name}",
        ItemLost d => $"item_lost {d.ItemId} ({d.Reason})",
        _ => delta.GetType().Name,
    };
}
