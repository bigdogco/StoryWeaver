using System.Text.Json;

namespace StoryWeaver.Core;

public static class DiscoveryEngine
{
    public static bool EvidencePresent(string? narration, string? evidence) => !string.IsNullOrWhiteSpace(evidence)
        && narration is not null && System.Text.RegularExpressions.Regex.Replace(narration, @"\s+", " ").Contains(
            System.Text.RegularExpressions.Regex.Replace(evidence.Trim(), @"\s+", " "), StringComparison.Ordinal);
    public static bool Exists(WorldState world, DiscoveryKind kind, string id) => kind switch
    {
        DiscoveryKind.Character => world.FindCharacter(id) is not null,
        DiscoveryKind.Location => world.FindLocation(id) is not null,
        DiscoveryKind.Item => world.FindItem(id) is not null, _ => false
    };

    internal static void ValidateBatch(WorldState world, IReadOnlyList<EntityObserved> observations,
        List<StateDelta> accepted, List<StateDelta> noOps, List<RejectedDelta> rejected, string? narration, int turn)
    {
        if (observations.Count == 0) return;
        var candidate = CanonCreation.Copy(world);
        DeltaApplier.Apply(candidate, accepted);
        candidate.Discovery ??= DiscoveryState.Minimal(candidate);
        // Conflicts are rejected as a group; response order cannot choose a remembered value.
        foreach (var group in observations.GroupBy(o => DiscoveryState.Key(o.EntityKind, o.TargetId), StringComparer.OrdinalIgnoreCase))
        {
            var values = group.ToList();
            bool conflict = values.Any(a => values.Any(b => Conflicts(a, b)));
            foreach (var observation in values)
            {
                string? error = conflict ? "Conflicting observations of the same aspect in this turn. Review in Author view."
                    : Check(candidate, observation, narration, turn);
                if (error is not null) { rejected.Add(new(observation, error)); continue; }
                var before = JsonSerializer.Serialize(candidate.Discovery, StoryJson.Options);
                Apply(candidate, observation, turn);
                if (before == JsonSerializer.Serialize(candidate.Discovery, StoryJson.Options)) noOps.Add(observation);
                else accepted.Add(observation);
            }
        }
    }

    private static bool Different<T>(Observation<T>? a, Observation<T>? b) => a is not null && b is not null
        && a.Provenance == b.Provenance && a != b;
    private static bool Conflicts(EntityObserved a, EntityObserved b) =>
        Different(a.Name, b.Name) || Different(a.Description, b.Description) || Different(a.Condition, b.Condition)
        || Different(a.Whereabouts, b.Whereabouts)
        || (a is LocationObserved x && b is LocationObserved y && (x.Connections ?? []).Any(r =>
            (y.Connections ?? []).Any(s => r is not null && s is not null && string.Equals(r.DestinationId, s.DestinationId, StringComparison.OrdinalIgnoreCase)
                && Different(r.Label, s.Label))));

    private static string? Check(WorldState world, EntityObserved delta, string? narration, int turn)
    {
        if (string.IsNullOrWhiteSpace(delta.TargetId) || !Exists(world, delta.EntityKind, delta.TargetId)) return "Observation target does not exist in accepted canon.";
        if (!EvidencePresent(narration, delta.Evidence))
            return "Observation evidence must quote the current narration exactly.";
        var memory = world.Discovery?.Find(delta.EntityKind, delta.TargetId);
        if (memory?.AuthorProtected == true && turn <= memory.ProtectedThroughTurn) return "Player knowledge is protected from retry of older evidence.";
        if (delta.Name is null && (memory is null || !memory.IdentityKnown || SafeName(memory) is null)) return "Observation needs a disclosed safe identity.";
        if (delta is LocationObserved && delta.Whereabouts is not null) return "Locations cannot have observed whereabouts.";
        if (delta.Name is null && delta.Description is null && delta.Condition is null && delta.Whereabouts is null
            && (delta is not LocationObserved l || l.Connections is not { Count: > 0 })) return "Observation has no aspects.";
        string? error = CheckAspect(world, delta.Name, memory?.Name, turn)
            ?? CheckAspect(world, delta.Description, memory?.Description, turn)
            ?? CheckAspect(world, delta.Condition, memory?.Condition, turn)
            ?? CheckAspect(world, delta.Whereabouts, memory?.Whereabouts, turn);
        if (error is not null) return error;
        if (delta.Whereabouts?.Value is { } where)
        {
            if (!Enum.IsDefined(where.Kind)) return "Invalid whereabouts kind.";
            if (where.Kind == WhereaboutsKind.Unknown && where.TargetId is not null) return "Unknown whereabouts cannot name a destination.";
            if (where.Kind == WhereaboutsKind.Holder && delta.EntityKind != DiscoveryKind.Item) return "Only items have holders.";
            if (where.Kind != WhereaboutsKind.Unknown && (string.IsNullOrWhiteSpace(where.TargetId)
                || !Exists(world, where.Kind == WhereaboutsKind.Holder ? DiscoveryKind.Character : DiscoveryKind.Location, where.TargetId)))
                return "Observed whereabouts names an invalid target.";
        }
        if (delta is LocationObserved location)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var route in location.Connections ?? [])
            {
                if (route is null || string.IsNullOrWhiteSpace(route.DestinationId) || !seen.Add(route.DestinationId)
                    || world.FindLocation(route.DestinationId) is null || route.Label is null) return "Invalid or duplicate discovered route.";
                error = CheckAspect(world, route.Label, memory?.Connections.GetValueOrDefault(route.DestinationId), turn);
                if (error is not null) return error;
            }
        }
        return null;
    }

    private static string? CheckAspect<T>(WorldState world, Observation<T>? next, MemoryAspect<T>? previous, int turn)
    {
        if (next is null) return null;
        if (previous?.AuthorProtected == true && turn <= previous.ProtectedThroughTurn) return "This aspect is protected from retry of older evidence. Clear protection to allow it.";
        if (next.Value is null || next.Value is string text && string.IsNullOrWhiteSpace(text)) return "An observed value is empty.";
        if (next.Provenance is not (DiscoveryProvenance.Observed or DiscoveryProvenance.Reported)) return "Extraction cannot author starting knowledge.";
        if (next.Provenance == DiscoveryProvenance.Reported)
        {
            if (string.IsNullOrWhiteSpace(next.FactId) || world.FindFact(next.FactId) is not { SourceId: { Length: > 0 } }
                || world.Player?.Knows.Contains(next.FactId) != true) return "A reported aspect needs an attributed fact learned by the player.";
        }
        else if (next.FactId is not null) return "Direct observations cannot claim reported provenance.";
        var old = next.Provenance == DiscoveryProvenance.Reported ? previous?.Report : previous?.Known;
        if (old is null) return null;
        if (old.Turn > turn) return "Older evidence cannot overwrite a newer observation.";
        if (old.Turn == turn && (!EqualityComparer<T>.Default.Equals(old.Value, next.Value) || old.Provenance != next.Provenance || old.FactId != next.FactId))
            return "Conflicting same-turn observation; review in Author view.";
        return null;
    }

    /// <summary>
    /// The label the player may be shown for an entity.
    ///
    /// A direct sighting wins over a report, no matter which came later. PLAYER_DISCOVERY.md:
    /// preserve the latest sighting separately from the latest report and present both, rather
    /// than "letting a rumour overwrite a witnessed encounter". This resolved once by recency,
    /// so an NPC who lied about a name on a later turn silently relabelled someone the player
    /// had already seen and identified. Both values are still stored; only the label prefers
    /// the sighting, and a report fills in when the name was never directly witnessed.
    /// </summary>
    public static string? SafeName(EntityMemory memory) => !memory.IdentityKnown ? null
        : memory.Name.Known?.Value ?? memory.Name.Report?.Value;

    public static void Apply(WorldState world, EntityObserved delta, int turn)
    {
        var state = world.Discovery ??= DiscoveryState.Minimal(world);
        var key = DiscoveryState.Key(delta.EntityKind, delta.TargetId);
        if (!state.Entries.TryGetValue(key, out var memory)) state.Entries[key] = memory = new() { Kind = delta.EntityKind, Id = delta.TargetId };
        string? oldName = SafeName(memory);
        if (delta.Name is not null) memory.IdentityKnown = true;
        Put(state, memory.Name, delta.Name, turn, delta.Evidence!);
        if (oldName is not null && oldName != SafeName(memory) && !memory.Aliases.Contains(oldName)) memory.Aliases.Add(oldName);
        Put(state, memory.Description, delta.Description, turn, delta.Evidence!);
        Put(state, memory.Condition, delta.Condition, turn, delta.Evidence!);
        Put(state, memory.Whereabouts, delta.Whereabouts, turn, delta.Evidence!);
        if (delta.Whereabouts is { Provenance: DiscoveryProvenance.Observed, Value.Kind: not WhereaboutsKind.Unknown }
            && memory.Whereabouts.Known is { } sighting)
            memory.LastSighting = sighting;
        if (delta is LocationObserved location)
            foreach (var route in location.Connections ?? [])
            {
                if (!memory.Connections.TryGetValue(route.DestinationId, out var aspect)) memory.Connections[route.DestinationId] = aspect = new();
                Put(state, aspect, route.Label, turn, delta.Evidence!);
            }
    }

    private static void Put<T>(DiscoveryState state, MemoryAspect<T> aspect, Observation<T>? observation, int turn, string evidence)
    {
        if (observation is null) return;
        var old = observation.Provenance == DiscoveryProvenance.Reported ? aspect.Report : aspect.Known;
        if (old is not null && old.Turn == turn && EqualityComparer<T>.Default.Equals(old.Value, observation.Value)
            && old.Provenance == observation.Provenance && old.FactId == observation.FactId) return;
        var learned = new Learned<T>(observation.Value, observation.Provenance, turn, evidence, observation.FactId, ++state.Revision);
        if (observation.Provenance == DiscoveryProvenance.Reported) aspect.Report = learned; else aspect.Known = learned;
    }
}
