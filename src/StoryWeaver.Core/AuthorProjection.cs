

namespace StoryWeaver.Core;

/// <summary>Read-only values rendered from a live world. It does not retain or modify canon.</summary>
public static class AuthorProjection
{
    public static IReadOnlyList<WorldViewEntry> Characters(WorldState world) => world.Characters
        .OrderBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase)
        .Select(pair => { var character = pair.Value; return new WorldViewEntry(
            CanonKind.Character, character.Id, character.Name,
            $"{character.Status} · {character.Mood}", character.Description,
            [new("Location", Name(world.FindLocation(character.LocationId ?? string.Empty))),
             new("Status", character.Status), new("Mood", character.Mood),
             new("Relationship", character.RelationshipToPlayer.Summary),
             new("Knowledge", Join(world.KnownFacts(character).Select(fact => fact.Text)))], pair.Key); })
        .ToList();

    public static IReadOnlyList<WorldViewEntry> Locations(WorldState world) => world.Locations
        .OrderBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase)
        .Select(pair => { var location = pair.Value; return new WorldViewEntry(
            CanonKind.Location, location.Id, location.Name,
            string.IsNullOrWhiteSpace(location.Status) ? "Location" : location.Status, location.Description,
            [new("Status", EmptyAsNone(location.Status)),
             new("Connections", Join(location.Connections.Select(id => Name(world.FindLocation(id))))),
             new("Present", Join(world.CharactersIn(location.Id).Select(character => character.Name)))], pair.Key); })
        .ToList();

    public static IReadOnlyList<WorldViewEntry> Facts(WorldState world) => world.Facts
        .OrderBy(pair => pair.Value.EstablishedTurn).ThenBy(pair => pair.Value.Text, StringComparer.OrdinalIgnoreCase)
        .Select(pair => { var fact = pair.Value; return new WorldViewEntry(
            CanonKind.Fact, fact.Id, fact.Text,
            $"Established on turn {fact.EstablishedTurn}", fact.Text,
            [new("Established", $"Turn {fact.EstablishedTurn}"),
             new("Source", string.IsNullOrWhiteSpace(fact.SourceId) ? "Narration" : Name(world.FindCharacter(fact.SourceId))),
             new("Known by", Join(world.Characters.Values.Where(character => character.Knows.Contains(fact.Id)).Select(character => character.Name)))], pair.Key); })
        .ToList();

    public static IReadOnlyList<WorldViewEntry> Items(WorldState world) => world.Items
        .OrderBy(pair => pair.Value.Name, StringComparer.OrdinalIgnoreCase)
        .Select(pair => { var item = pair.Value; return new WorldViewEntry(
            CanonKind.Item, item.Id, item.Name, item.Status, item.Description,
            [new("Location", item.IsPlaced ? Name(world.FindLocation(item.LocationId ?? string.Empty)) : "None"),
             new("Holder", item.IsHeld ? Name(world.FindCharacter(item.HolderId ?? string.Empty)) : "None"),
             new("Condition", item.Status)], pair.Key); })
        .ToList();

    private static string Name(Entity? entity) => entity?.Name ?? "Unknown";
    private static string EmptyAsNone(string value) => string.IsNullOrWhiteSpace(value) ? "None" : value;
    private static string Join(IEnumerable<string?> values) => string.Join(", ", values.Where(value => !string.IsNullOrWhiteSpace(value))) switch
    {
        "" => "None",
        string joined => joined,
    };
}
