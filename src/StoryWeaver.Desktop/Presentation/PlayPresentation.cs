namespace StoryWeaver.Desktop.Presentation;

// UI values, not a second canon model. No object here can write to the world graph.
public enum EntityKind { Character, Location, Fact, Item }
public sealed record EntityReference(EntityKind Kind, string Id);
public sealed record DetailField(string Label, string Value);
public sealed record EntityDetails(
    EntityReference Reference, string Name, string Summary, string Description,
    IReadOnlyList<DetailField> Fields);
public sealed record NarrativeSpan(string Text, EntityReference? Target = null);
public sealed record NarrativeParagraph(string Label, IReadOnlyList<NarrativeSpan> Spans);
