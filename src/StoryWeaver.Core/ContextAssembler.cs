using System.Text;

namespace StoryWeaver.Core;

/// <summary>
/// Renders world state into the text the models see.
///
/// <b>Two renderings, not one.</b> The narrator and the extractor want opposite things: the
/// extractor must emit exact ids and cannot work without them, while the narrator has no use
/// for them at all and will happily put one in the prose if it sees one. That is not
/// hypothetical — a session produced "the heavy oak door of the marrow-tavern flies outward"
/// because connections were listed as bare ids and the narrator read them as names.
///
/// Naive on purpose beyond that: the current scene is dumped in full, with no budgeting,
/// ranking, or lorebook retrieval. Each of those introduces a way to *silently* omit
/// something, which is the biggest source of "why did the AI forget the Duke existed" in
/// existing tools. Not worth building before there is something to measure.
/// </summary>
public static class ContextAssembler
{
    /// <summary>
    /// Prose-facing view. Names only — no ids anywhere, including in connections. Anything
    /// id-shaped that appears here can end up in the story.
    /// </summary>
    public static string ForNarration(WorldState world) => Build(world, withIds: false);

    /// <summary>
    /// Bookkeeping view. Ids beside every name, plus an explicit roster of known ids.
    ///
    /// The roster targets two observed failures: re-introducing a location that was merely
    /// mentioned, and inventing a fresh slug for something already in canon. The validator
    /// catches both, but catching them means dropping the delta — better that the model does
    /// not need correcting.
    /// </summary>
    public static string ForExtraction(WorldState world) => Build(world, withIds: true);

    private static string Build(WorldState world, bool withIds)
    {
        StringBuilder builder = new();

        Location? here = world.PlayerLocationId is { } id ? world.FindLocation(id) : null;

        builder.AppendLine("## Current scene");
        builder.AppendLine();

        if (here is null)
        {
            builder.AppendLine("The player is nowhere yet; the story has not opened.");
        }
        else
        {
            builder.AppendLine($"Location: {Label(here.Name, here.Id, withIds)}");
            builder.AppendLine(here.Description);

            if (here.Connections.Count > 0)
            {
                // Resolved to names for narration. An unresolvable connection is skipped
                // rather than printed as a raw id, which is how the id leak happened.
                IEnumerable<string> exits = here.Connections
                    .Select(c => (Connection: c, Location: world.FindLocation(c)))
                    .Where(x => withIds || x.Location is not null)
                    .Select(x => x.Location is null
                        ? x.Connection
                        : Label(x.Location.Name, x.Location.Id, withIds));

                string joined = string.Join(", ", exits);

                if (joined.Length > 0)
                {
                    builder.AppendLine($"Leads to: {joined}");
                }
            }
        }

        builder.AppendLine();
        AppendPlayer(builder, world, withIds);
        AppendNpcs(builder, world, withIds);

        if (withIds)
        {
            AppendKnownIds(builder, world);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendPlayer(StringBuilder builder, WorldState world, bool withIds)
    {
        if (world.Player is not { } player)
        {
            return;
        }

        builder.AppendLine("## The player");
        builder.AppendLine();
        builder.AppendLine($"{Label(player.Name, player.Id, withIds)} — {player.Status}, {player.Mood}");

        // The one knowledge set that constrains narration rather than dialogue: the prose
        // must not reveal something the player has not learned.
        AppendKnowledge(builder, world, player, withIds, whenEmpty: null);
        builder.AppendLine();
    }

    private static void AppendNpcs(StringBuilder builder, WorldState world, bool withIds)
    {
        List<Character> npcs = [.. world.NpcsWithPlayer()];

        builder.AppendLine("## Present");
        builder.AppendLine();

        if (npcs.Count == 0)
        {
            builder.AppendLine("Nobody else is here.");
            builder.AppendLine();
            return;
        }

        foreach (Character npc in npcs)
        {
            builder.AppendLine($"### {Label(npc.Name, npc.Id, withIds)}");
            builder.AppendLine(npc.Description);
            builder.AppendLine($"State: {npc.Status}, {npc.Mood}");
            builder.AppendLine(
                $"Toward the player: {npc.RelationshipToPlayer.Summary} " +
                $"({npc.RelationshipToPlayer.Standing:+#;-#;0})");

            // Stated explicitly when empty. An absent section reads as "not mentioned"; this
            // has to read as "knows nothing", or the model fills the gap itself.
            AppendKnowledge(builder, world, npc, withIds, whenEmpty: "Knows: nothing recorded.");
            builder.AppendLine();
        }
    }

    private static void AppendKnowledge(
        StringBuilder builder,
        WorldState world,
        Character character,
        bool withIds,
        string? whenEmpty)
    {
        List<Fact> known = [.. world.KnownFacts(character)];

        if (known.Count == 0)
        {
            if (whenEmpty is not null)
            {
                builder.AppendLine(whenEmpty);
            }

            return;
        }

        builder.AppendLine("Knows:");

        foreach (Fact fact in known)
        {
            builder.AppendLine(withIds ? $"  - ({fact.Id}) {fact.Text}" : $"  - {fact.Text}");
        }
    }

    private static void AppendKnownIds(StringBuilder builder, WorldState world)
    {
        builder.AppendLine("## Known ids");
        builder.AppendLine();
        builder.AppendLine(
            "These already exist. Reference them by id. Do not re-introduce them, and do not " +
            "re-establish a fact that is already listed.");
        builder.AppendLine();
        builder.AppendLine($"Characters: {Join(world.Characters.Keys)}");
        builder.AppendLine($"Locations:  {Join(world.Locations.Keys)}");
        builder.AppendLine($"Facts:      {Join(world.Facts.Keys)}");
    }

    private static string Label(string name, string id, bool withIds) =>
        withIds ? $"{name} (id: {id})" : name;

    private static string Join(IEnumerable<string> ids)
    {
        List<string> list = [.. ids];
        return list.Count == 0 ? "(none)" : string.Join(", ", list);
    }
}
