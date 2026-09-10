namespace StoryWeaver.Core;

public sealed record WorldViewField(string Label, string Value);
public sealed record WorldViewEntry(CanonKind Kind, string Id, string Name, string Summary, string Description,
    IReadOnlyList<WorldViewField> Fields, string? CanonKey = null, IReadOnlyList<string>? Aliases = null, bool CanAuthor = true);
public sealed record WorldView(IReadOnlyList<WorldViewEntry> Entries, string? Notice = null);

/// <summary>The complete disclosure boundary. Clients receive detached values, never a filtered private graph.</summary>
public static class DiscoveryProjection
{
    public static WorldView Player(WorldState world, LoreBook lore)
    {
        var state = world.Discovery ?? DiscoveryState.Minimal(world);
        var entries = new List<WorldViewEntry>();
        string Label(DiscoveryKind kind, string? id) => id is not null && state.Find(kind, id) is { } m
            ? DiscoveryEngine.SafeName(m) ?? "Not identified" : "Not identified";
        string Source(string? factId) => factId is not null && world.FindFact(factId) is { } fact
            ? Label(DiscoveryKind.Character, fact.SourceId) : "Not identified";
        string Stamp<T>(Learned<T> learned) => learned.Provenance switch
        {
            DiscoveryProvenance.Reported => $"Reported by {Source(learned.FactId)} · turn {learned.Turn}",
            DiscoveryProvenance.Starting => "Starting knowledge · turn 0",
            _ => $"Observed · turn {learned.Turn}"
        };
        string Text(MemoryAspect<string> aspect) => string.Join("\n", new[]
        {
            aspect.Known is { } a ? $"{a.Value} · {Stamp(a)}" : null,
            aspect.Report is { } b ? $"{b.Value} · {Stamp(b)}" : null
        }.Where(x => x is not null)) is { Length: > 0 } value ? value : "Unknown";
        string Where(Learned<Whereabouts> value)
        {
            string place = value.Value.Kind == WhereaboutsKind.Unknown ? "Whereabouts now unknown"
                : Label(value.Value.Kind == WhereaboutsKind.Holder ? DiscoveryKind.Character : DiscoveryKind.Location, value.Value.TargetId);
            // A fallback label is intentionally disclosed text, never a live entity name.
            if (place == "Not identified" && !string.IsNullOrWhiteSpace(value.Value.DisclosedLabel)) place = value.Value.DisclosedLabel;
            return $"{place} · {Stamp(value)}";
        }
        foreach (var memory in state.Entries.Values)
        {
            if (DiscoveryEngine.SafeName(memory) is not { Length: > 0 } name) continue;
            var fields = new List<WorldViewField> { new("Identity", Text(memory.Name)), new("Condition / demeanour", Text(memory.Condition)) };
            if (memory.Kind != DiscoveryKind.Location)
            {
                fields.Add(new("Whereabouts", memory.Whereabouts.Known is { } at ? Where(at) : "Unknown"));
                if (memory.LastSighting is { } last && last != memory.Whereabouts.Known) fields.Add(new("Last sighting", Where(last)));
                if (memory.Whereabouts.Report is { } report) fields.Add(new("Reported whereabouts", Where(report)));
            }
            else
            {
                fields.Add(new("Discovered outgoing routes", string.Join("\n", memory.Connections.Select(p =>
                    $"{Label(DiscoveryKind.Location, p.Key)}: {Text(p.Value)}")) is { Length: > 0 } routes ? routes : "Unknown"));
                var sightings = state.Entries.Values.Where(m => DiscoveryEngine.SafeName(m) is not null
                    && m.Whereabouts.Known is { Value.Kind: WhereaboutsKind.Location } known
                    && string.Equals(known.Value.TargetId, memory.Id, StringComparison.OrdinalIgnoreCase))
                    .Select(m => $"{DiscoveryEngine.SafeName(m)} · {Stamp(m.Whereabouts.Known!)}");
                fields.Add(new("Recorded sightings (not a live census)", string.Join("\n", sightings) is { Length: > 0 } found ? found : "None recorded"));
            }
            entries.Add(new((CanonKind)Enum.Parse(typeof(CanonKind), memory.Kind.ToString()), memory.Id, name,
                "Player knowledge", Text(memory.Description), fields, memory.Id, [.. memory.Aliases], false));
        }
        foreach (var fact in world.Facts.Values.Where(f => world.Player?.Knows.Contains(f.Id) == true))
            entries.Add(new(CanonKind.Fact, fact.Id, fact.Text, fact.SourceId is null ? "Learned fact" : "Attributed claim", fact.Text,
                [new("Source", fact.SourceId is null ? "Narration" : Label(DiscoveryKind.Character, fact.SourceId))], fact.Id, CanAuthor: false));
        foreach (var entry in lore.All.Where(l => l.Common || world.Player?.Knows.Contains(l.Id) == true))
            entries.Add(new(CanonKind.Fact, entry.Id, entry.Title, entry.Common ? "Common lore" : "Known topic",
                entry.Common ? entry.Body : "You have heard of this topic. Its private reference text is not learned knowledge.", [], CanAuthor: false));
        return new(entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList(), world.Discovery is null
            ? "This playthrough has no discovery records. Player knowledge is limited; initialize it in Author view or start a new playthrough." : null);
    }
}
