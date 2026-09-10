using System.Text.Json;
using System.Text.Json.Serialization;

namespace StoryWeaver.Core;

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryKind>))]
public enum DiscoveryKind { Character, Location, Item }
[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryProvenance>))]
public enum DiscoveryProvenance { Observed, Reported, Starting }
[JsonConverter(typeof(JsonStringEnumConverter<WhereaboutsKind>))]
public enum WhereaboutsKind { Unknown, Location, Holder }

public sealed record Whereabouts(WhereaboutsKind Kind, string? TargetId = null, string? DisclosedLabel = null);
public sealed record Observation<T>(T Value, DiscoveryProvenance Provenance = DiscoveryProvenance.Observed, string? FactId = null);
public sealed record Learned<T>(T Value, DiscoveryProvenance Provenance, int Turn, string Evidence = "",
    string? FactId = null, long Revision = 0);

/// <summary>Direct/starting knowledge and the latest report coexist; rumours never replace sightings.</summary>
public sealed class MemoryAspect<T>
{
    public bool AuthorProtected { get; set; }
    public int ProtectedThroughTurn { get; set; }
    public Learned<T>? Known { get; set; }
    public Learned<T>? Report { get; set; }
}

public sealed class EntityMemory
{
    public DiscoveryKind Kind { get; set; }
    public string Id { get; set; } = "";
    public bool IdentityKnown { get; set; } = true;
    public bool AuthorProtected { get; set; }
    public int ProtectedThroughTurn { get; set; }
    public MemoryAspect<string> Name { get; set; } = new();
    public MemoryAspect<string> Description { get; set; } = new();
    public MemoryAspect<string> Condition { get; set; } = new();
    public MemoryAspect<Whereabouts> Whereabouts { get; set; } = new();
    public Learned<Whereabouts>? LastSighting { get; set; }
    public List<string> Aliases { get; set; } = [];
    public Dictionary<string, MemoryAspect<string>> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record PresentationRule(bool RequiresDiscovery = false, string PrivateInstruction = "");

public sealed class DiscoveryState
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public long Revision { get; set; }
    public Dictionary<string, EntityMemory> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    // Entity keys: Kind:id. Directed route keys: Route:from:to.
    public Dictionary<string, PresentationRule> Rules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public static string Key(DiscoveryKind kind, string id) => $"{kind}:{id}";
    public EntityMemory? Find(DiscoveryKind kind, string id) => Entries.GetValueOrDefault(Key(kind, id));
    public static DiscoveryState Clone(DiscoveryState value)
    {
        var copy = JsonSerializer.Deserialize<DiscoveryState>(JsonSerializer.Serialize(value, StoryJson.Options), StoryJson.Options)!;
        copy.Entries = new(copy.Entries, StringComparer.OrdinalIgnoreCase);
        copy.Rules = new(copy.Rules, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in copy.Entries.Values) entry.Connections = new(entry.Connections, StringComparer.OrdinalIgnoreCase);
        return copy;
    }
    public static void RequireSupported(WorldState world)
    {
        if (world.Discovery is { Version: not CurrentVersion })
            throw new InvalidDataException("This discovery save format is unsupported. Open it with a compatible version.");
        if (world.Discovery is not { } state) return;
        if (state.Entries is null || state.Rules is null || state.Rules.Values.Any(r => r is null)
            || state.Entries.Values.Any(m => m is null || !Enum.IsDefined(m.Kind) || m.Name is null || m.Description is null
                || m.Condition is null || m.Whereabouts is null || m.Connections is null || m.Aliases is null
                || m.Connections.Values.Any(a => a is null)
                || m.Whereabouts.Known is { Value: null } || m.Whereabouts.Report is { Value: null } || m.LastSighting is { Value: null }))
            throw new InvalidDataException("Discovery data contains an invalid shape. Correct it in the JSON file before opening this playthrough.");
    }
    public static DiscoveryState Minimal(WorldState world)
    {
        var state = new DiscoveryState();
        if (world.Player is { } player)
            state.Entries[Key(DiscoveryKind.Character, player.Id)] = new EntityMemory
            {
                Kind = DiscoveryKind.Character, Id = player.Id,
                Name = new() { Known = new(player.Name, DiscoveryProvenance.Starting, 0) }
            };
        return state;
    }
}

public abstract record EntityObserved : StateDelta
{
    public string TargetId { get; init; } = "";
    [JsonPropertyName("identity")] public Observation<string>? Name { get; init; }
    [JsonPropertyName("descriptionObservation")] public Observation<string>? Description { get; init; }
    public Observation<string>? Condition { get; init; }
    public Observation<Whereabouts>? Whereabouts { get; init; }
    [JsonIgnore] public abstract DiscoveryKind EntityKind { get; }
}
public sealed record CharacterObserved : EntityObserved
{ [JsonIgnore] public override DiscoveryKind EntityKind => DiscoveryKind.Character; }
public sealed record ItemObserved : EntityObserved
{ [JsonIgnore] public override DiscoveryKind EntityKind => DiscoveryKind.Item; }
public sealed record RouteObservation(string DestinationId, Observation<string> Label);
public sealed record LocationObserved : EntityObserved
{
    public IReadOnlyList<RouteObservation>? Connections { get; init; }
    [JsonIgnore] public override DiscoveryKind EntityKind => DiscoveryKind.Location;
}
