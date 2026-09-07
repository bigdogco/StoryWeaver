using StoryWeaver.Desktop.Presentation;

namespace StoryWeaver.Desktop.Preview;

/// <summary>Illustrative display data. Never loaded into a StorySession or saved as canon.</summary>
internal static class PreviewScene
{
    public static readonly EntityReference Mona = new(EntityKind.Character, "inspector-mona");
    public static readonly EntityReference Hald = new(EntityKind.Character, "innkeeper-hald");
    public static readonly EntityReference Square = new(EntityKind.Location, "marrow-square");
    public static readonly EntityReference Tavern = new(EntityKind.Location, "marrow-tavern");

    public static IReadOnlyList<EntityDetails> Entities =>
    [
        new(Mona, "Inspector Mona", "Present · King's Investigator",
            "An investigator with a measured voice and a habit of watching the room before speaking.",
            [new("Location", "The Drowned Crow"), new("Mood", "Watchful"),
             new("Knowledge", "The seal matches the mark found in the square.")]),
        new(Hald, "Innkeeper Hald", "Present · Innkeeper",
            "The keeper of the Drowned Crow. Tonight his attention keeps returning to the investigators' table.",
            [new("Location", "The Drowned Crow"), new("Mood", "Uneasy")]),
        new(Tavern, "The Drowned Crow", "Current location",
            "A low-beamed tavern sheltering a handful of late drinkers from the rain.",
            [new("Connections", "Marrow Square"), new("Present", "You, Inspector Mona, Innkeeper Hald")]),
        new(Square, "Marrow Square", "Connected location",
            "A rain-soaked square outside the tavern. A strange mark was discovered here.",
            [new("Connections", "The Drowned Crow")]),
        new(new(EntityKind.Fact, "matching-mark"), "The letter's seal matches the mark in the square.",
            "Known by you and Mona", "The same mark appears in both places.",
            [new("Established", "Turn 12 · illustrative scene"), new("Known by", "You, Inspector Mona")]),
        new(new(EntityKind.Item, "sealed-letter"), "Sealed letter", "On the tavern table",
            "A dry letter bearing a seal that resembles the mark in the square.",
            [new("Location", "The Drowned Crow"), new("Condition", "Intact")])
    ];

    public static IReadOnlyList<NarrativeParagraph> Paragraphs =>
    [
        new("THE DROWNED CROW · ILLUSTRATIVE SCENE",
            [new("The tavern door shuts out the rain. At the far end of the room, "),
             new("Inspector Mona", Mona), new(" rests a folded letter beside her untouched drink.")]),
        new("YOU", [new("I sit opposite Mona and ask what she found at the square.")]),
        new("NARRATION", [new("“Someone was waiting for us,” she says. Her eyes move towards "),
            new("Hald", Hald), new(", who has stopped polishing the same glass.")]),
        new("", [new("She slides the letter across the table. “The seal matches the mark at "),
            new("Marrow Square", Square), new(". But this was delivered before we arrived.”")]),
        new("", [new("The paper is still dry. Whoever brought it has not been outside tonight.")])
    ];
}
