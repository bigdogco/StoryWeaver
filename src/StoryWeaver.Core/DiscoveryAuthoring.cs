using System.Text.Json;

namespace StoryWeaver.Core;

public sealed record InitialDiscovery(string? SafeName = null, string? Description = null, string? Condition = null,
    Whereabouts? Whereabouts = null, bool RequiresDiscovery = false, string PrivateInstruction = "");

public sealed class DiscoveryEditSnapshot
{
    internal Guid Session { get; }
    internal string Revision { get; }
    private readonly WorldState _catalog;
    internal DiscoveryEditSnapshot(WorldState world, Guid session)
    { Session = session; Revision = Fingerprint(world); _catalog = CanonCreation.Copy(world); }
    public WorldState CopyCatalog() => CanonCreation.Copy(_catalog);
    public DiscoveryState CreateDraft() => _catalog.Discovery is { } d ? DiscoveryState.Clone(d) : DiscoveryState.Minimal(_catalog);
    internal static string Fingerprint(WorldState world) => JsonSerializer.Serialize(world, StoryJson.Options);
}

public static class DiscoveryAuthoring
{
    internal static void InitializeEntity(WorldState world, CanonKind kind, string id, InitialDiscovery? initial)
    {
        if (initial is null || kind == CanonKind.Fact) return;
        var discoveryKind = Enum.Parse<DiscoveryKind>(kind.ToString());
        var state = world.Discovery ??= DiscoveryState.Minimal(world);
        string key = DiscoveryState.Key(discoveryKind, id);
        state.Rules[key] = new(initial.RequiresDiscovery, initial.PrivateInstruction);
        if (string.IsNullOrWhiteSpace(initial.SafeName)) return;
        if (!state.Entries.TryGetValue(key, out var memory)) state.Entries[key] = memory = new() { Id = id, Kind = discoveryKind };
        memory.IdentityKnown = true;
        MemoryAspect<T> Aspect<T>(T value) => new() { Known = new(value, DiscoveryProvenance.Observed,
            world.TurnNumber, "Explicit author knowledge", Revision: ++state.Revision), AuthorProtected = true, ProtectedThroughTurn = world.TurnNumber };
        memory.Name = Aspect(initial.SafeName);
        if (!string.IsNullOrWhiteSpace(initial.Description)) memory.Description = Aspect(initial.Description);
        if (!string.IsNullOrWhiteSpace(initial.Condition)) memory.Condition = Aspect(initial.Condition);
        if (initial.Whereabouts is not null)
        {
            memory.Whereabouts = Aspect(initial.Whereabouts);
            if (initial.Whereabouts.Kind != WhereaboutsKind.Unknown) memory.LastSighting = memory.Whereabouts.Known;
        }
    }
    // Protect changed aspects, including explicit clearing. An empty protected slot remains
    // a tombstone so retrying older evidence cannot silently undo an author's correction.
    internal static DiscoveryState Prepare(WorldState world, DiscoveryState draft)
    {
        var result = DiscoveryState.Clone(draft);
        var old = world.Discovery;
        result.Revision = Math.Max(result.Revision, old?.Revision ?? 0);
        foreach (var (key, memory) in result.Entries)
        {
            var before = old?.Entries.GetValueOrDefault(key);
            if (memory.AuthorProtected && (before?.IdentityKnown != memory.IdentityKnown || before?.AuthorProtected != true))
                memory.ProtectedThroughTurn = world.TurnNumber;
            void Stamp<T>(MemoryAspect<T> aspect, MemoryAspect<T>? prior)
            {
                if (aspect.AuthorProtected && (aspect.Known != prior?.Known || aspect.Report != prior?.Report || prior?.AuthorProtected != true))
                    aspect.ProtectedThroughTurn = world.TurnNumber;
                Learned<T>? Update(Learned<T>? next, Learned<T>? previous)
                {
                    if (next == previous) return next;
                    return next is null ? null : next with { Revision = ++result.Revision };
                }
                aspect.Known = Update(aspect.Known, prior?.Known); aspect.Report = Update(aspect.Report, prior?.Report);
            }
            Stamp(memory.Name, before?.Name); Stamp(memory.Description, before?.Description);
            Stamp(memory.Condition, before?.Condition); Stamp(memory.Whereabouts, before?.Whereabouts);
            if (memory.Whereabouts.Known != before?.Whereabouts.Known
                && memory.Whereabouts.Known is { Provenance: DiscoveryProvenance.Observed, Value.Kind: not WhereaboutsKind.Unknown } sighting)
                memory.LastSighting = sighting;
            foreach (var (destination, aspect) in memory.Connections) Stamp(aspect, before?.Connections.GetValueOrDefault(destination));
        }
        return result;
    }
}
