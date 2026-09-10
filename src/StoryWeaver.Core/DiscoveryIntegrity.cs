namespace StoryWeaver.Core;

public static class DiscoveryIntegrity
{
    public static IReadOnlyList<string> Check(WorldState world, bool seed = false)
    {
        List<string> errors = [];
        if (world.Discovery is not { } state) return errors;
        if (state.Version != DiscoveryState.CurrentVersion) errors.Add("Unsupported discovery version.");
        if (state.Entries is null || state.Rules is null) return ["Discovery entries/rules cannot be null."];
        foreach (var (key, memory) in state.Entries)
        {
            if (memory is null) { errors.Add($"Discovery {key} is null."); continue; }
            if (!Enum.IsDefined(memory.Kind) || !EntityId.IsWellFormed(memory.Id)
                || !string.Equals(key, DiscoveryState.Key(memory.Kind, memory.Id), StringComparison.OrdinalIgnoreCase)) errors.Add($"Discovery key/kind/ID disagree: {key}.");
            if (seed && !DiscoveryEngine.Exists(world, memory.Kind, memory.Id)) errors.Add($"Starting discovery {key} has no canonical target.");
            if (memory.Name is null || memory.Description is null || memory.Condition is null || memory.Whereabouts is null
                || memory.Connections is null || memory.Aliases is null) { errors.Add($"Discovery {key} has null aspects."); continue; }
            void CheckLearned<T>(Learned<T>? value)
            {
                if (value is null) return;
                if (value.Value is null || value.Value is string text && string.IsNullOrWhiteSpace(text)
                    || value.Turn < 0 || value.Turn > world.TurnNumber || !Enum.IsDefined(value.Provenance)) errors.Add($"Discovery {key} has an invalid value, turn or provenance.");
                if (value.Provenance == DiscoveryProvenance.Reported && (value.FactId is null
                    || world.FindFact(value.FactId) is not { SourceId: { Length: > 0 } }
                    || world.Player?.Knows.Contains(value.FactId) != true)) errors.Add($"Discovery {key} has a broken learned-claim reference.");
            }
            void Aspect<T>(MemoryAspect<T> value) { CheckLearned(value.Known); CheckLearned(value.Report);
                if (value.Known?.Provenance == DiscoveryProvenance.Reported || value.Report is { Provenance: not DiscoveryProvenance.Reported }) errors.Add($"Discovery {key} has misplaced provenance."); }
            Aspect(memory.Name); Aspect(memory.Description); Aspect(memory.Condition); Aspect(memory.Whereabouts); CheckLearned(memory.LastSighting);
            foreach (var aspect in memory.Connections.Values) if (aspect is null) errors.Add($"Discovery {key} has a null route."); else Aspect(aspect);
            if (seed && memory.Connections.Keys.Any(id => world.FindLocation(id) is null)) errors.Add($"Starting discovery {key} references a missing route destination.");
            if (memory.IdentityKnown && string.IsNullOrWhiteSpace(DiscoveryEngine.SafeName(memory))) errors.Add($"Discovery {key} has no safe identity label.");
            foreach (var where in new[] { memory.Whereabouts.Known, memory.Whereabouts.Report, memory.LastSighting })
            {
                if (where is not null && (where.Value is null || !Enum.IsDefined(where.Value.Kind)
                    || where.Value.Kind == WhereaboutsKind.Unknown && where.Value.TargetId is not null
                    || where.Value.Kind != WhereaboutsKind.Unknown && string.IsNullOrWhiteSpace(where.Value.TargetId)
                    || where.Value.Kind == WhereaboutsKind.Holder && memory.Kind != DiscoveryKind.Item)) errors.Add($"Discovery {key} has invalid whereabouts.");
                else if (seed && where is { Value.Kind: not WhereaboutsKind.Unknown } && !DiscoveryEngine.Exists(world,
                    where.Value.Kind == WhereaboutsKind.Holder ? DiscoveryKind.Character : DiscoveryKind.Location, where.Value.TargetId!)) errors.Add($"Starting discovery {key} references a missing whereabouts target.");
            }
            // Missing canon targets and destinations may be legitimate historical memories.
            if (memory.Kind != DiscoveryKind.Location && memory.Connections.Count > 0
                || memory.Kind == DiscoveryKind.Location && (memory.Whereabouts.Known is not null || memory.Whereabouts.Report is not null)) errors.Add($"Discovery {key} has aspects for a different entity kind.");
        }
        foreach (var (key, rule) in state.Rules)
        {
            var parts = key.Split(':');
            bool route = parts.Length == 3 && parts[0].Equals("Route", StringComparison.OrdinalIgnoreCase);
            bool entity = parts.Length == 2 && Enum.TryParse<DiscoveryKind>(parts[0], true, out var kind) && Enum.IsDefined(kind);
            if (rule is null || !(route || entity) || parts.Skip(1).Any(p => !EntityId.IsWellFormed(p))) errors.Add($"Invalid presentation rule: {key}.");
            else if (seed && (route ? world.FindLocation(parts[1]) is null || world.FindLocation(parts[2]) is null
                : !DiscoveryEngine.Exists(world, Enum.Parse<DiscoveryKind>(parts[0], true), parts[1]))) errors.Add($"Starting rule {key} references a missing target.");
        }
        return errors;
    }
}
