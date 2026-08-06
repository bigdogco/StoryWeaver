using System.Text;
using System.Text.RegularExpressions;

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
public static partial class ContextAssembler
{
    /// <summary>
    /// Prose-facing view. Names only — no ids anywhere, including in connections. Anything
    /// id-shaped that appears here can end up in the story.
    /// </summary>
    public static string ForNarration(
        WorldState world,
        LoreBook? lore = null,
        IReadOnlyDictionary<string, CharacterSheet>? sheets = null) =>
        Build(world, lore ?? LoreBook.Empty, sheets ?? Empty, withIds: false);

    /// <summary>
    /// Bookkeeping view. Ids beside every name, plus an explicit roster of known ids.
    ///
    /// The roster targets two observed failures: re-introducing a location that was merely
    /// mentioned, and inventing a fresh slug for something already in canon. The validator
    /// catches both, but catching them means dropping the delta — better that the model does
    /// not need correcting.
    /// </summary>
    public static string ForExtraction(
        WorldState world,
        LoreBook? lore = null,
        IReadOnlyDictionary<string, CharacterSheet>? sheets = null) =>
        Build(world, lore ?? LoreBook.Empty, sheets ?? Empty, withIds: true);

    private static readonly Dictionary<string, CharacterSheet> Empty =
        new(StringComparer.OrdinalIgnoreCase);

    private static string Build(
        WorldState world,
        LoreBook lore,
        IReadOnlyDictionary<string, CharacterSheet> sheets,
        bool withIds)
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
            builder.AppendLine(EntityReferences.Resolve(here.Description, world));

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

            AppendLooseItems(builder, world, here, withIds);
        }

        builder.AppendLine();
        AppendPlayer(builder, world, lore, sheets, withIds);
        AppendNpcs(builder, world, lore, sheets, withIds);
        AppendLore(builder, world, lore, withIds);

        if (withIds)
        {
            AppendKnownIds(builder, world, lore);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Things lying in the room that nobody is holding.
    ///
    /// Listed in the scene because a capstone left on a counter is part of what is there, and
    /// an item that vanished from the narrator's view while still being in canon is exactly the
    /// quiet inconsistency this architecture exists to prevent.
    ///
    /// The cost to watch: a room accumulating dropped objects grows this block on every turn,
    /// which is the budgeting problem already deferred for lore. Worth measuring before it is
    /// worth solving.
    /// </summary>
    private static void AppendLooseItems(
        StringBuilder builder,
        WorldState world,
        Location here,
        bool withIds)
    {
        List<Item> loose = [.. world.ItemsIn(here.Id)];

        if (loose.Count == 0)
        {
            return;
        }

        builder.AppendLine("Here:");

        foreach (Item item in loose)
        {
            string state = item.Status is "intact" or "" ? string.Empty : $" — {item.Status}";
            builder.AppendLine($"  - {Label(item.Name, item.Id, withIds)}{state}");
        }
    }

    /// <summary>
    /// How a character feels about groups and about other people, from their sheet.
    ///
    /// **The permanent why, distinct from the standing directly above it.** "Dislikes him — he
    /// stole his sword, years ago now" is history and stays true however the relationship
    /// develops; `Toward the player` is a number that moves every turn. He may stop disliking
    /// you, and he will always be the man whose sword you stole.
    ///
    /// Targets are rendered by name, never by id — the same rule that keeps ids out of the
    /// prose everywhere else.
    /// </summary>
    private static void AppendAttitudes(
        StringBuilder builder,
        WorldState world,
        LoreBook lore,
        IReadOnlyDictionary<string, CharacterSheet> sheets,
        Character character,
        bool withIds)
    {
        if (!sheets.TryGetValue(character.Id, out CharacterSheet? sheet) || sheet.Attitudes.Count == 0)
        {
            return;
        }

        builder.AppendLine("Feels about:");

        foreach ((string target, string phrase) in sheet.Attitudes.OrderBy(a => a.Key, StringComparer.Ordinal))
        {
            string name = world.FindCharacter(target)?.Name
                          ?? lore.Find(target)?.Title
                          ?? target;

            builder.AppendLine(
                $"  - {Label(name, target, withIds)}: {EntityReferences.Resolve(phrase, world)}");
        }
    }

    /// <summary>
    /// Pushes an authored body's headings below the ones this document already uses.
    ///
    /// A sheet writes <c>## Manner</c> because that is natural markdown for its own file. Pasted
    /// in unchanged it lands at the same level as <c>## Present</c> and <c>## World lore</c>, so
    /// a character's sections read as top-level sections of the prompt and the structure the
    /// model relies on quietly stops meaning anything.
    ///
    /// Authors should not have to know what depth their prose will be rendered at.
    /// </summary>
    private static string Indent(string body) =>
        HeadingLine().Replace(body, "####");

    [GeneratedRegex("(?m)^#{1,3}(?=[ ])")]
    private static partial Regex HeadingLine();

    /// <summary>Things a character is carrying, listed under them.</summary>
    private static void AppendCarried(
        StringBuilder builder,
        WorldState world,
        Character character,
        bool withIds)
    {
        List<Item> carried = [.. world.ItemsHeldBy(character.Id)];

        if (carried.Count == 0)
        {
            return;
        }

        builder.Append("Carrying: ");
        builder.AppendLine(string.Join(", ", carried.Select(i =>
        {
            string state = i.Status is "intact" or "" ? string.Empty : $" ({i.Status})";
            return Label(i.Name, i.Id, withIds) + state;
        })));
    }

    private static void AppendPlayer(
        StringBuilder builder,
        WorldState world,
        LoreBook lore,
        IReadOnlyDictionary<string, CharacterSheet> sheets,
        bool withIds)
    {
        if (world.Player is not { } player)
        {
            return;
        }

        builder.AppendLine("## The player");
        builder.AppendLine();
        builder.AppendLine($"{Label(player.Name, player.Id, withIds)} — {player.Status}, {player.Mood}");
        AppendCarried(builder, world, player, withIds);

        // The one knowledge set that constrains narration rather than dialogue: the prose
        // must not reveal something the player has not learned.
        AppendKnowledge(builder, world, lore, player, withIds, whenEmpty: null);
        builder.AppendLine();
    }

    private static void AppendNpcs(
        StringBuilder builder,
        WorldState world,
        LoreBook lore,
        IReadOnlyDictionary<string, CharacterSheet> sheets,
        bool withIds)
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
            builder.AppendLine(Indent(EntityReferences.Resolve(npc.Description, world)));
            builder.AppendLine($"State: {npc.Status}, {npc.Mood}");
            AppendCarried(builder, world, npc, withIds);
            builder.AppendLine(
                $"Toward the player: {npc.RelationshipToPlayer.Summary} " +
                $"({npc.RelationshipToPlayer.Standing:+#;-#;0})");

            // Stated explicitly when empty. An absent section reads as "not mentioned"; this
            // has to read as "knows nothing", or the model fills the gap itself.
            AppendAttitudes(builder, world, lore, sheets, npc, withIds);
            AppendKnowledge(builder, world, lore, npc, withIds, whenEmpty: "Knows: nothing recorded.");
            builder.AppendLine();
        }
    }

    private static void AppendKnowledge(
        StringBuilder builder,
        WorldState world,
        LoreBook lore,
        Character character,
        bool withIds,
        string? whenEmpty)
    {
        List<Fact> known = [.. world.KnownFacts(character)];

        // Lore a character has heard of, by title only. The body belongs in one place — the
        // lore section — and repeating it per character would multiply the largest text in
        // the prompt by the size of the cast.
        List<LoreEntry> heardOf = [.. lore.KnownBy(character)];

        if (known.Count == 0 && heardOf.Count == 0)
        {
            if (whenEmpty is not null)
            {
                builder.AppendLine(whenEmpty);
            }

            return;
        }

        if (known.Count > 0)
        {
            builder.AppendLine("Knows:");

            foreach (Fact fact in known)
            {
                // Attribution matters more to the narrator than to the extractor: a character
                // who cannot tell "the stone went to the bog" from "Mabb said the stone went
                // to the bog" will state a drunk's contradiction as settled truth.
                string said = Attribution(world, fact, withIds);
                builder.AppendLine(withIds ? $"  - ({fact.Id}) {fact.Text}{said}" : $"  - {fact.Text}{said}");
            }
        }

        if (heardOf.Count > 0)
        {
            builder.AppendLine("Has heard of:");

            foreach (LoreEntry entry in heardOf)
            {
                builder.AppendLine(withIds ? $"  - ({entry.Id}) {entry.Title}" : $"  - {entry.Title}");
            }
        }
    }

    /// <summary>
    /// The lore block.
    ///
    /// <b>Bodies for the narrator, titles for the extractor.</b> The narrator needs the prose;
    /// the extractor needs only enough to emit <c>fact_learned</c> against an id, and handing
    /// it several paragraphs of reference material invites exactly the invention the
    /// extraction prompt spends most of its length suppressing. The same split as ids
    /// themselves, introduced for a different reason and still paying out.
    ///
    /// Position matters and is already solved: this sits in the volatile block that narration
    /// keeps in the *last* message, so the system prompt and replayed history stay a stable,
    /// cacheable prefix. Injecting lore mid-prompt is the usual way a lorebook becomes
    /// expensive — see CHALLENGES.md — and an earlier decision avoided it by accident.
    /// </summary>
    private static void AppendLore(StringBuilder builder, WorldState world, LoreBook lore, bool withIds)
    {
        List<LoreEntry> selected = [.. lore.Selected()];

        if (selected.Count == 0)
        {
            return;
        }

        builder.AppendLine("## World lore");
        builder.AppendLine();

        if (withIds)
        {
            builder.AppendLine(
                "Reference topics that exist in this world. A character may learn one the same " +
                "way they learn a fact. You may never establish one — they are authored, not " +
                "discovered.");
            builder.AppendLine();
            builder.AppendLine(
                "The words after each topic are what it is called and what it is known by. A " +
                "scene can be about a topic without ever naming it: people speak of the thing " +
                "itself rather than its label. Match on the subject, not the title.");
            builder.AppendLine();

            foreach (LoreEntry entry in selected)
            {
                // Keys, not the body. They are short authored strings — exactly the "what does
                // this topic sound like" signal — where a body is several paragraphs of prose
                // that would invite the invention the extraction prompt spends its length
                // suppressing.
                //
                // Without them the extractor sees "(cult-of-the-blind) The Cult of the Blind"
                // and nothing else, so "the Drowned Father took his tithe" is an unrelated
                // string. Measured: a 51-turn session about that cult taught it to nobody.
                string aka = entry.Keys.Count > 0
                    ? $" — also: {string.Join(", ", entry.Keys)}"
                    : string.Empty;

                builder.AppendLine($"  - ({entry.Id}) {entry.Title}{aka}");
            }

            builder.AppendLine();
            return;
        }

        builder.AppendLine(
            "Background for your own use. Do not recite it. A character may only refer to a " +
            "topic they have heard of.");
        builder.AppendLine();

        foreach (LoreEntry entry in selected)
        {
            builder.AppendLine($"### {entry.Title}");
            builder.AppendLine(Indent(EntityReferences.Resolve(entry.Body, world)));
            builder.AppendLine();
        }
    }

    private static void AppendKnownIds(StringBuilder builder, WorldState world, LoreBook lore)
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

        if (world.Items.Count > 0)
        {
            builder.AppendLine($"Items:      {Join(world.Items.Keys)}");
        }

        if (lore.Count > 0)
        {
            builder.AppendLine($"Lore:       {Join(lore.Ids)}");
        }
    }

    /// <summary>
    /// " — said by Hald", or nothing when the fact is plain world truth.
    ///
    /// Rendered as a suffix rather than folded into the text so the claim itself stays one
    /// clean sentence, which is what the extraction prompt asks the model to write.
    /// </summary>
    private static string Attribution(WorldState world, Fact fact, bool withIds)
    {
        if (fact.SourceId is not { } sourceId)
        {
            return string.Empty;
        }

        // An unresolvable source is dropped rather than printed as a raw id — the same rule
        // that stopped location ids leaking into prose.
        return world.FindCharacter(sourceId) is { } speaker
            ? $" — said by {Label(speaker.Name, speaker.Id, withIds)}"
            : string.Empty;
    }

    private static string Label(string name, string id, bool withIds) =>
        withIds ? $"{name} (id: {id})" : name;

    private static string Join(IEnumerable<string> ids)
    {
        List<string> list = [.. ids];
        return list.Count == 0 ? "(none)" : string.Join(", ", list);
    }
}
