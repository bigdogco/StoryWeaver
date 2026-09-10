using System.Text.Json;

namespace StoryWeaver.Core;

public sealed class CanonRemovalPlan
{
    public CanonTarget Target { get; }
    public string Label { get; }
    public IReadOnlyList<string> Consequences { get; }
    public string? RefusedBecause { get; }
    internal string Revision { get; }
    internal Guid Session { get; }
    internal IReadOnlyList<RemovalEffect> Effects { get; }
    internal object Identity { get; }
    internal IReadOnlyList<object> Dependencies { get; }
    internal CanonRemovalPlan(CanonTarget target, string label, IEnumerable<string> consequences,
        string? refusedBecause, string revision, Guid session, IEnumerable<RemovalEffect> effects,
        object identity, IEnumerable<object> dependencies)
        => (Target, Label, Consequences, RefusedBecause, Revision, Session, Effects, Identity, Dependencies) =
            (target, label, Array.AsReadOnly(consequences.ToArray()), refusedBecause, revision, session,
                Array.AsReadOnly(effects.ToArray()), identity, Array.AsReadOnly(dependencies.ToArray()));
}

internal sealed record RemovalEffect(string Operation, string Key, string? Value = null);
public sealed record CanonRemovalOutcome(EditReport? Report, CanonRemovalPlan? UpdatedPlan)
{
    public bool Removed => Report is not null;
}

/// <summary>The preview and application share one typed plan. References resolve through dictionary keys.</summary>
internal static class CanonRemoval
{
    internal static CanonRemovalPlan? Capture(WorldState world, LoreBook lore, CanonTarget target, Guid session)
    {
        var selected = CanonCorrection.Capture(world, target);
        if (selected is null) return null;
        List<string> messages = [], revision = [selected.Revision];
        messages.Add("Player observations and aliases are retained as memories. Removal does not reveal itself in Player view. Reusing this ID retains its remembered identity; review Player knowledge explicitly.");
        List<RemovalEffect> effects = [];
        List<object> dependencies = [];
        string key = target.Key;
        object identity = target.Kind switch
        {
            CanonKind.Character => world.Characters[key], CanonKind.Location => world.Locations[key],
            CanonKind.Fact => world.Facts[key], CanonKind.Item => world.Items[key],
            _ => throw new InvalidOperationException("Unknown canon kind.")
        };
        string label = selected.Fields switch
        {
            CharacterFields c => c.Name, LocationFields l => l.Name, ItemFields i => i.Name,
            FactFields f => f.Text.Length > 100 ? f.Text[..100] + "…" : f.Text, _ => target.Id
        };
        string? refusal = target.Kind == CanonKind.Character
            && (Same(key, Character.PlayerId) || Same(target.Id, Character.PlayerId))
            ? "The playthrough needs its player character. Use Edit to correct them." : null;

        void Depend(string kind, string entryKey, object entry)
        {
            dependencies.Add(entry);
            revision.Add(kind + ":" + entryKey + ":" + JsonSerializer.Serialize(entry, entry.GetType(), StoryJson.Options));
        }
        string Place(string id)
        {
            if (world.FindLocation(id) is not { } location) return $"missing location ({id})";
            Depend("destination", id, location); return $"{location.Name} ({id})";
        }
        string Identity(string entryKey, string id) => entryKey == id ? id : $"key {entryKey}; stored ID {id}";
        if (key != target.Id)
            messages.Add($"This entry is stored under key '{key}' with ID '{target.Id}'. Only references resolving through key '{key}' are changed; other references remain untouched.");

        if (target.Kind == CanonKind.Character)
        {
            var character = world.Characters[key];
            foreach (var pair in world.Items.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var item = pair.Value;
                if (!Same(item.HolderId, key)) continue;
                Depend("item", pair.Key, item);
                string before = $"holder {item.HolderId ?? "none"}, location {item.LocationId ?? "none"}";
                if (character.LocationId is { } location)
                {
                    messages.Add($"{item.Name} ({Identity(pair.Key, item.Id)}): {before} → holder none, location {Place(location)}.");
                    effects.Add(new("drop-item", pair.Key, location));
                }
                else messages.Add($"{item.Name} ({Identity(pair.Key, item.Id)}): {before} remains unchanged. It will still refer to the removed holder; the character is offstage.");
            }
            foreach (var pair in world.Facts.OrderBy(p => p.Key, StringComparer.Ordinal).Where(p => Same(p.Value.SourceId, key)))
            {
                Depend("source-fact", pair.Key, pair.Value);
                messages.Add($"Fact {Identity(pair.Key, pair.Value.Id)}: {pair.Value.Text}\nIts source '{pair.Value.SourceId}' and the fact itself remain.");
            }
        }
        else if (target.Kind == CanonKind.Location)
        {
            foreach (var pair in world.Characters.OrderBy(p => p.Key, StringComparer.Ordinal).Where(p => Same(p.Value.LocationId, key)))
            {
                Depend("occupant", pair.Key, pair.Value);
                messages.Add($"{pair.Value.Name} ({Identity(pair.Key, pair.Value.Id)}) becomes offstage. Their held items remain held.");
                effects.Add(new("offstage", pair.Key));
            }
            foreach (var pair in world.Locations.OrderBy(p => p.Key, StringComparer.Ordinal).Where(p => !Same(p.Key, key) && p.Value.Connections.Contains(key)))
            {
                Depend("connection", pair.Key, pair.Value);
                messages.Add($"{pair.Value.Name} ({Identity(pair.Key, pair.Value.Id)}): remove its outgoing connection to '{key}'.");
                effects.Add(new("disconnect", pair.Key, key));
            }
            foreach (var pair in world.Items.OrderBy(p => p.Key, StringComparer.Ordinal).Where(p => Same(p.Value.LocationId, key)))
            {
                Depend("lying-item", pair.Key, pair.Value);
                messages.Add($"{pair.Value.Name} ({Identity(pair.Key, pair.Value.Id)}): location '{key}', holder {pair.Value.HolderId ?? "none"} remain unchanged. It will still refer to the removed location.");
            }
        }
        else if (target.Kind == CanonKind.Fact)
        {
            foreach (var pair in world.Characters.OrderBy(p => p.Key, StringComparer.Ordinal).Where(p => p.Value.Knows.Contains(key)))
            {
                Depend("knower", pair.Key, pair.Value);
                messages.Add($"{pair.Value.Name} ({Identity(pair.Key, pair.Value.Id)}): remove explicit knowledge of '{key}'.");
                effects.Add(new("forget", pair.Key, key));
            }
            if (lore.Find(key) is { } entry)
                messages.Add($"Pack lore '{entry.Title}' also uses this key and remains. Explicit learning is removed as listed."
                    + (entry.Common ? " It is common lore, so everyone still knows it through the pack." : ""));
        }
        if (messages.Count == 0) messages.Add("No other structured canon entries will change.");
        messages.Insert(0, $"Remove {target.Kind.ToString().ToLowerInvariant()} '{label}' ({Identity(key, target.Id)}).");
        messages.Add("Past narration, authored character sheets and pack lore remain. This corrects canon; it does not narrate a death or destruction. Retained story or pack text may mention the entry again. There is no Undo action.");
        // Preview text participates too: changes to destination names and resolution must be reviewed.
        string stamp = JsonSerializer.Serialize(new { revision, messages, effects, refusal });
        return new(target, label, messages, refusal, stamp, session, effects, identity, dependencies);
    }

    internal static void Apply(WorldState world, CanonRemovalPlan plan)
    {
        foreach (var effect in plan.Effects)
            switch (effect.Operation)
            {
                case "drop-item": (world.Items[effect.Key].HolderId, world.Items[effect.Key].LocationId) = (null, effect.Value); break;
                case "offstage": world.Characters[effect.Key].LocationId = null; break;
                case "disconnect": world.Locations[effect.Key].Connections.Remove(effect.Value!); break;
                case "forget": world.Characters[effect.Key].Knows.Remove(effect.Value!); break;
            }
        switch (plan.Target.Kind)
        {
            case CanonKind.Character: world.Characters.Remove(plan.Target.Key); break;
            case CanonKind.Location: world.Locations.Remove(plan.Target.Key); break;
            case CanonKind.Fact: world.Facts.Remove(plan.Target.Key); break;
            case CanonKind.Item: world.Items.Remove(plan.Target.Key); break;
        }
    }
    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
