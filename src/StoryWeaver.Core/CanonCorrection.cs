using System.Text.Json;

namespace StoryWeaver.Core;

public enum CanonKind { Character, Location, Fact, Item }
public sealed record CanonTarget(CanonKind Kind, string Key, string Id);
public abstract record CanonFields;
public sealed record CharacterFields(string Name, string Description, string? LocationId,
    string Status, string Mood, Relationship Relationship, IReadOnlyList<string> Knowledge) : CanonFields;
public sealed record LocationFields(string Name, string Description, string Status,
    IReadOnlyList<string> Connections) : CanonFields;
public sealed record FactFields(string Text, IReadOnlyList<string> KnownBy) : CanonFields;
public sealed record ItemFields(string Name, string Description, string Status,
    string? LocationId, string? HolderId) : CanonFields;
public sealed record CanonMetadata(int? LastSeenTurn = null, int? EstablishedTurn = null, string? SourceId = null);

/// <summary>Detached values and an opaque baseline, never a mutable canon entity.</summary>
public sealed class CanonEditSnapshot
{
    public CanonTarget Target { get; }
    public CanonFields Fields { get; }
    public CanonMetadata Metadata { get; }
    internal string Revision { get; }
    internal CanonEditSnapshot(CanonTarget target, CanonFields fields, CanonMetadata metadata, string revision)
        => (Target, Fields, Metadata, Revision) = (target, fields, metadata, revision);
}

/// <summary>Direct corrections: clients supply values; Core owns identity and cross-entity effects.</summary>
public static class CanonCorrection
{
    public static CanonEditSnapshot? Capture(WorldState world, CanonTarget target)
    {
        object? entity = target.Kind switch
        {
            CanonKind.Character => world.FindCharacter(target.Key),
            CanonKind.Location => world.FindLocation(target.Key),
            CanonKind.Fact => world.FindFact(target.Key),
            CanonKind.Item => world.FindItem(target.Key),
            _ => null
        };
        string? id = entity is Entity named ? named.Id : (entity as Fact)?.Id;
        if (entity is null || id != target.Id) return null;
        CanonFields fields = entity switch
        {
            Character c => new CharacterFields(c.Name, c.Description, c.LocationId, c.Status, c.Mood,
                c.RelationshipToPlayer, Frozen(c.Knows)),
            Location l => new LocationFields(l.Name, l.Description, l.Status, Frozen(l.Connections)),
            Fact f => new FactFields(f.Text, Frozen(world.Characters.Where(pair => pair.Value.Knows.Contains(f.Id)).Select(pair => pair.Key))),
            Item i => new ItemFields(i.Name, i.Description, i.Status, i.LocationId, i.HolderId),
            _ => throw new InvalidOperationException("Unsupported canon kind.")
        };
        CanonMetadata metadata = entity switch
        {
            Character c => new(LastSeenTurn: c.LastSeenTurn),
            Fact f => new(EstablishedTurn: f.EstablishedTurn, SourceId: f.SourceId),
            _ => new()
        };
        // Include immutable/hidden fields too. Fact membership lives on characters;
        // include their keys/identities so a concurrent roster edit cannot be overwritten.
        string revision = JsonSerializer.Serialize(entity, entity.GetType(), StoryJson.Options);
        if (entity is Fact fact)
            revision += JsonSerializer.Serialize(world.Characters.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { p.Key, p.Value.Id, Knows = p.Value.Knows.Contains(fact.Id) }));
        return new(target, fields, metadata, revision);
    }

    public static bool Same(CanonFields before, CanonFields after) => (before, after) switch
    {
        (CharacterFields a, CharacterFields b) => a.Name == b.Name && a.Description == b.Description
            && a.LocationId == b.LocationId && a.Status == b.Status && a.Mood == b.Mood
            && a.Relationship == b.Relationship && SetSame(a.Knowledge, b.Knowledge),
        (LocationFields a, LocationFields b) => a.Name == b.Name && a.Description == b.Description
            && a.Status == b.Status && SetSame(a.Connections, b.Connections),
        (FactFields a, FactFields b) => a.Text == b.Text && SetSame(a.KnownBy, b.KnownBy),
        (ItemFields a, ItemFields b) => a == b,
        _ => false
    };

    internal static string? Validate(WorldState world, CanonEditSnapshot baseline, CanonFields fields)
    {
        var current = Capture(world, baseline.Target);
        if (current is null) return "The selected entity no longer exists with the same identity. Close and reopen the editor.";
        if (current.Revision != baseline.Revision)
            return "Canon changed after this editor was opened. Your draft is retained; close and reopen the editor to review the current state.";
        if (fields.GetType() != baseline.Fields.GetType()) return "The correction does not match the selected entity kind.";
        if (fields is CharacterFields c && baseline.Fields is CharacterFields original)
        {
            if (c.Relationship.Standing != original.Relationship.Standing && c.Relationship.Standing is < -100 or > 100)
                return "Relationship standing must be between -100 and 100.";
            if (world.FindCharacter(baseline.Target.Key)!.IsPlayer && c.Relationship != original.Relationship)
                return "The player's relationship to themselves is not editable.";
        }
        if (fields is FactFields f && f.KnownBy.Any(key => !world.Characters.ContainsKey(key)))
            return "A selected character no longer exists. Close and reopen the editor.";
        if (fields is ItemFields item && baseline.Fields is ItemFields old
            && (item.LocationId != old.LocationId || item.HolderId != old.HolderId)
            && (item.LocationId is null) == (item.HolderId is null))
            return "Choose either a holder or a location for the item.";
        return null;
    }

    internal static void Apply(WorldState world, CanonEditSnapshot baseline, CanonFields fields)
    {
        string key = baseline.Target.Key;
        switch (baseline.Fields, fields)
        {
            case (CharacterFields old, CharacterFields value):
                var c = world.Characters[key];
                if (old.Name != value.Name) c.Name = value.Name;
                if (old.Description != value.Description) c.Description = value.Description;
                if (old.Status != value.Status) c.Status = value.Status;
                if (old.Mood != value.Mood) c.Mood = value.Mood;
                if (old.LocationId != value.LocationId) c.LocationId = value.LocationId;
                if (old.Relationship != value.Relationship) c.RelationshipToPlayer = value.Relationship;
                UpdateSet(c.Knows, old.Knowledge, value.Knowledge);
                break;
            case (LocationFields old, LocationFields value):
                var l = world.Locations[key];
                if (old.Name != value.Name) l.Name = value.Name;
                if (old.Description != value.Description) l.Description = value.Description;
                if (old.Status != value.Status) l.Status = value.Status;
                UpdateSet(l.Connections, old.Connections, value.Connections);
                break;
            case (FactFields old, FactFields value):
                var f = world.Facts[key];
                if (old.Text != value.Text) f.Text = value.Text;
                foreach (string removed in old.KnownBy.Except(value.KnownBy, StringComparer.OrdinalIgnoreCase))
                    world.Characters[removed].Knows.Remove(f.Id);
                foreach (string added in value.KnownBy.Except(old.KnownBy, StringComparer.OrdinalIgnoreCase))
                    world.Characters[added].Knows.Add(f.Id);
                break;
            case (ItemFields old, ItemFields value):
                var i = world.Items[key];
                if (old.Name != value.Name) i.Name = value.Name;
                if (old.Description != value.Description) i.Description = value.Description;
                if (old.Status != value.Status) i.Status = value.Status;
                if (old.LocationId != value.LocationId || old.HolderId != value.HolderId)
                    (i.LocationId, i.HolderId) = (value.LocationId, value.HolderId);
                break;
        }
    }

    private static IReadOnlyList<string> Frozen(IEnumerable<string> values) => Array.AsReadOnly(values.ToArray());
    private static bool SetSame(IEnumerable<string> a, IEnumerable<string> b) => new HashSet<string>(a, StringComparer.OrdinalIgnoreCase).SetEquals(b);
    private static void UpdateSet(HashSet<string> target, IReadOnlyList<string> old, IReadOnlyList<string> value)
    {
        foreach (string removed in old.Except(value, StringComparer.OrdinalIgnoreCase)) target.Remove(removed);
        foreach (string added in value.Except(old, StringComparer.OrdinalIgnoreCase)) target.Add(added);
    }
}
