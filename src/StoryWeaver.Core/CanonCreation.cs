using System.Text.Json;

namespace StoryWeaver.Core;

/// <summary>A private catalog baseline; callers receive a disposable copy for rendering choices.</summary>
public sealed class CanonCreationSnapshot
{
    internal WorldState Catalog { get; }
    internal Guid Session { get; }
    internal LoreBook Lore { get; }
    private readonly Dictionary<string, object> _identities = new(StringComparer.OrdinalIgnoreCase);
    public CanonKind Kind { get; }
    public CanonEditSnapshot Form { get; }
    public WorldState CopyCatalog() => CanonCreation.Copy(Catalog);

    internal CanonCreationSnapshot(WorldState world, LoreBook lore, CanonKind kind, Guid session)
    {
        Catalog = CanonCreation.Copy(world); Lore = lore; Kind = kind; Session = session;
        foreach (var pair in world.Characters) _identities["character:" + pair.Key] = pair.Value;
        foreach (var pair in world.Locations) _identities["location:" + pair.Key] = pair.Value;
        foreach (var entry in lore.All) _identities["knowledge:" + entry.Id] = entry;
        foreach (var pair in world.Facts) _identities["knowledge:" + pair.Key] = pair.Value;
        CanonFields fields = kind switch
        {
            CanonKind.Character => new CharacterFields("", "", null, "normal", "neutral", Relationship.Neutral, []),
            CanonKind.Location => new LocationFields("", "", "", []),
            CanonKind.Fact => new FactFields("", []),
            CanonKind.Item => new ItemFields("", "", "intact", null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        Form = new(new(kind, "", ""), fields, new(LastSeenTurn: world.TurnNumber,
            EstablishedTurn: world.TurnNumber), "");
    }

    public string? IdError(string id) => CanonCreation.IdError(Catalog, Lore, id);
    public string? InputError(string id, CanonFields fields) => CanonCreation.InputError(Catalog, Lore, Kind, id, fields);
    internal bool SameIdentity(string kind, string key, object? current) =>
        _identities.TryGetValue(kind + ":" + key, out var original) && ReferenceEquals(original, current);
}

/// <summary>Complete-form authoring. Validation precedes all live mutations; persistence belongs to the session.</summary>
public static class CanonCreation
{
    // Keep the collection comparers as well as the values. Model-facing JSON options
    // do not contain Storage's case-insensitive dictionary/set converters.
    internal static WorldState Copy(WorldState world)
    {
        var copy = new WorldState { TurnNumber = world.TurnNumber };
        foreach (var (key, c) in world.Characters)
            copy.Characters.Add(key, new Character
            {
                Id = c.Id, Name = c.Name, Description = c.Description, LocationId = c.LocationId,
                Status = c.Status, Mood = c.Mood, RelationshipToPlayer = c.RelationshipToPlayer,
                LastSeenTurn = c.LastSeenTurn, Knows = new(c.Knows, StringComparer.OrdinalIgnoreCase)
            });
        foreach (var (key, l) in world.Locations)
            copy.Locations.Add(key, new Location
            {
                Id = l.Id, Name = l.Name, Description = l.Description, Status = l.Status,
                Connections = new(l.Connections, StringComparer.OrdinalIgnoreCase)
            });
        foreach (var (key, f) in world.Facts)
            copy.Facts.Add(key, new Fact { Id = f.Id, Text = f.Text, SourceId = f.SourceId, EstablishedTurn = f.EstablishedTurn });
        foreach (var (key, i) in world.Items)
            copy.Items.Add(key, new Item
            {
                Id = i.Id, Name = i.Name, Description = i.Description, Status = i.Status,
                LocationId = i.LocationId, HolderId = i.HolderId
            });
        return copy;
    }

    public static string? IdError(WorldState world, LoreBook lore, string id)
    {
        if (!EntityId.IsWellFormed(id)) return "ID: use lowercase letters and digits joined by single hyphens, such as innkeeper-hald.";
        if (id == Character.PlayerId) return "The ID 'player' is reserved for the playthrough's player character.";
        var keys = world.Characters.Keys.Concat(world.Locations.Keys).Concat(world.Facts.Keys).Concat(world.Items.Keys);
        var ids = world.Characters.Values.Select(c => c.Id).Concat(world.Locations.Values.Select(l => l.Id))
            .Concat(world.Facts.Values.Select(f => f.Id)).Concat(world.Items.Values.Select(i => i.Id));
        return keys.Concat(ids).Concat(lore.Ids).Contains(id, StringComparer.OrdinalIgnoreCase)
            ? $"ID '{id}' is already used by canon or pack lore. Choose another." : null;
    }

    internal static string? InputError(WorldState world, LoreBook lore, CanonKind kind, string id, CanonFields fields)
    {
        if (IdError(world, lore, id) is { } error) return error;
        bool matches = (kind, fields) is (CanonKind.Character, CharacterFields) or (CanonKind.Location, LocationFields)
            or (CanonKind.Fact, FactFields) or (CanonKind.Item, ItemFields);
        if (!matches) return "The form does not match the kind being added.";
        string name = fields switch { CharacterFields c => c.Name, LocationFields l => l.Name, ItemFields i => i.Name, FactFields f => f.Text, _ => "" };
        if (string.IsNullOrWhiteSpace(name)) return fields is FactFields ? "Fact text is required." : "Name is required.";
        if (fields is CharacterFields character && character.Relationship.Standing is < -100 or > 100)
            return "Relationship standing must be between -100 and 100.";
        if (fields is ItemFields item && (item.LocationId is null) == (item.HolderId is null))
            return "Select exactly one holder or location.";
        foreach (var reference in References(fields))
            if (Resolve(world, lore, reference.Kind, reference.Key) is null)
                return $"The selected {reference.Kind} reference '{reference.Key}' does not exist.";
        return null;
    }

    internal static string? Validate(WorldState world, LoreBook lore, CanonCreationSnapshot baseline, string id, CanonFields fields)
    {
        if (InputError(world, lore, baseline.Kind, id, fields) is { } error) return error;
        foreach (var reference in References(fields))
        {
            var original = Resolve(baseline.Catalog, baseline.Lore, reference.Kind, reference.Key);
            var current = Resolve(world, lore, reference.Kind, reference.Key);
            if (original is null || original != current || !baseline.SameIdentity(reference.Kind, reference.Key,
                ResolveEntity(world, lore, reference.Kind, reference.Key)))
                return $"The selected reference '{reference.Key}' changed after this form opened. Your draft is retained; reopen Add to review current canon.";
        }
        return null;
    }

    private static IEnumerable<(string Kind, string Key)> References(CanonFields fields)
    {
        switch (fields)
        {
            case CharacterFields c:
                if (c.LocationId is { } place) yield return ("location", place);
                foreach (string fact in c.Knowledge) yield return ("knowledge", fact);
                break;
            case LocationFields l:
                foreach (string destination in l.Connections) yield return ("location", destination);
                break;
            case FactFields f:
                foreach (string knower in f.KnownBy) yield return ("character", knower);
                break;
            case ItemFields i:
                if (i.LocationId is { } location) yield return ("location", location);
                if (i.HolderId is { } holder) yield return ("character", holder);
                break;
        }
    }

    private static string? Resolve(WorldState world, LoreBook lore, string kind, string key)
    {
        object? value = ResolveEntity(world, lore, kind, key);
        return value is null ? null : JsonSerializer.Serialize(value, value.GetType(), StoryJson.Options);
    }
    private static object? ResolveEntity(WorldState world, LoreBook lore, string kind, string key) => kind switch
        {
            "location" => world.FindLocation(key), "character" => world.FindCharacter(key),
            "knowledge" => (object?)world.FindFact(key) ?? lore.Find(key), _ => null
        };

    internal static IReadOnlyList<StateDelta> Deltas(string id, CanonFields fields)
    {
        List<StateDelta> deltas = [];
        switch (fields)
        {
            case CharacterFields c:
                deltas.Add(new CharacterIntroduced(id, c.Name, c.Description, c.LocationId));
                if (!string.IsNullOrWhiteSpace(c.Status)) deltas.Add(new StatusChanged(id, c.Status));
                if (!string.IsNullOrWhiteSpace(c.Mood)) deltas.Add(new MoodChanged(id, c.Mood));
                deltas.Add(new RelationshipChanged(id, c.Relationship.Standing, c.Relationship.Summary));
                foreach (string fact in c.Knowledge.Distinct(StringComparer.OrdinalIgnoreCase)) deltas.Add(new FactLearned(id, fact));
                break;
            case LocationFields l:
                deltas.Add(new LocationIntroduced(id, l.Name, l.Description));
                if (!string.IsNullOrWhiteSpace(l.Status)) deltas.Add(new LocationStatusChanged(id, l.Status));
                break;
            case FactFields f:
                deltas.Add(new FactEstablished(id, f.Text));
                foreach (string knower in f.KnownBy.Distinct(StringComparer.OrdinalIgnoreCase)) deltas.Add(new FactLearned(knower, id));
                break;
            case ItemFields i:
                deltas.Add(new ItemIntroduced(id, i.Name, i.Description, i.LocationId, i.HolderId));
                if (!string.IsNullOrWhiteSpace(i.Status)) deltas.Add(new ItemStatusChanged(id, i.Status));
                break;
        }
        return deltas;
    }

    internal static void ApplyFields(WorldState world, string id, CanonFields fields)
    {
        switch (fields)
        {
            case CharacterFields c:
                world.Characters[id].Status = c.Status; world.Characters[id].Mood = c.Mood;
                world.Characters[id].RelationshipToPlayer = c.Relationship;
                world.Characters[id].Knows.UnionWith(c.Knowledge);
                break;
            case LocationFields l:
                world.Locations[id].Status = l.Status; world.Locations[id].Connections.UnionWith(l.Connections); break;
            case ItemFields i: world.Items[id].Status = i.Status; break;
            case FactFields f:
                // An old dangling membership must not silently teach the newly authored truth
                // to someone omitted from Known by. The complete form owns this membership set.
                var knowers = f.KnownBy.ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in world.Characters)
                    if (knowers.Contains(pair.Key)) pair.Value.Knows.Add(id); else pair.Value.Knows.Remove(id);
                break;
        }
    }
}
