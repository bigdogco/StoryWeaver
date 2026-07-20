using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// A matcher over deltas, with a human-readable name so failures say what was missing rather
/// than dumping objects.
/// </summary>
internal sealed record DeltaRule(string Description, Func<StateDelta, bool> Matches);

/// <summary>
/// One eval case: a fixed world, a fixed player input, and fixed narration.
///
/// <b>No narrator call.</b> The narration is hand-written and identical every run, which
/// removes the narrator as a source of variance, halves the cost per run, and makes results
/// comparable across extraction models. Measuring extraction through a nondeterministic
/// narrator would mean never knowing which model moved.
///
/// Scored as <see cref="Required"/> and <see cref="Forbidden"/> rather than an exact expected
/// list. Exact matching would fail a model for choosing "wary" over "guarded", which is not a
/// mistake — the interesting questions are whether it catches what it must and avoids what it
/// must not.
/// </summary>
internal sealed record EvalScenario(
    string Name,
    string PlayerInput,
    string Narration,
    IReadOnlyList<DeltaRule> Required,
    IReadOnlyList<DeltaRule> Forbidden)
{
    public WorldState World() => WorldSeeds.Marrow();
}

internal static class EvalScenarios
{
    private const string Hald = "innkeeper-hald";

    public static IReadOnlyList<EvalScenario> All =>
    [
        Deflection,
        Revelation,
        Movement,
        Hostility,
        NewCharacter,
        Redescription,
        Atmosphere,
    ];

    // PlayerClaim is retired, not deleted. It encoded the decision that a player's assertion
    // becomes a fact, and six models rejected it 0/7, 0/7, 2/7 across 21 samples. Twenty-one
    // samples of unanimous disagreement is a design answer, not a tuning problem — the models
    // treat "someone said a thing" as an event rather than as world truth, which is defensible
    // and arguably more correct than the original rule.
    //
    // Kept in the file because the reasoning is worth finding again if someone re-proposes it.

    /// <summary>
    /// <b>Real generated narration, copied verbatim from a live session</b> — not written for
    /// the eval.
    ///
    /// Every other scenario here is hand-written: one clear event, tightly worded. Real
    /// narration is three paragraphs of atmosphere that re-describe every known character,
    /// name a known location in passing, and change almost nothing. On exactly this text, live
    /// play produced a burst of <c>character_introduced</c> for characters already in canon —
    /// while the clean scenarios reported that problem solved.
    ///
    /// The eval was agreeing with itself until this case existed. A test set made only of
    /// tidy inputs measures how a model handles tidy inputs.
    /// </summary>
    private static EvalScenario Atmosphere => new(
        "atmosphere",
        "*I rub my chin and look around the room.*",
        """
        The peat smoke hangs thick in the low-ceilinged taproom, stinging the air. At the
        scattered tables, the locals keep their heads down, wrapped in their own quiet misery
        and the sour chill drifting in from the marsh.

        Hald watches you from behind the scarred wood of the bar. His thick fingers pause, then
        resume a slow, rhythmic wiping of the same damp spot on the counter. He doesn't offer a
        greeting, just observes you with a heavy, guarded stillness, his eyes tracking your
        every shift in weight.

        Over in the shadowed corner, the old marsh-hand slumps over his mug. Mabb lets out a
        wet, ragged sigh that rattles in his chest, his fingers tracing the rim of his cup as he
        mutters something lost to the crackle of the hearth. Beyond them, the wind picks up,
        rattling the heavy door that leads out to Marrow Square.
        """,
        Required: [],
        Forbidden:
        [
            new("a known character re-introduced",
                d => d is CharacterIntroduced { CharacterId: Hald or "drinker-mabb" or Character.PlayerId }),
            new("any character introduced at all", d => d is CharacterIntroduced),
            new("a known location introduced",
                d => d is LocationIntroduced { LocationId: "marrow-square" or "marrow-tavern" }),
            new("a fact established from atmosphere", d => d is FactEstablished),
        ]);

    /// <summary>
    /// Nothing is revealed. The failure this targets is the junk fact: an earlier session
    /// established "The player asked Hald and Mabb about the well" as permanent world truth
    /// and taught it to two characters.
    /// </summary>
    private static EvalScenario Deflection => new(
        "deflection",
        "*I lean on the counter.* What do you know about the well?",
        """
        Hald's rag stops on the scarred wood. He looks at you for a moment, then resumes his
        slow circles without answering. "Boarded up for a reason," he says at last. "Ain't
        nothing down there but black water and bad luck. Drink your ale and leave it alone."
        He turns to straighten a row of mugs, making it clear the subject is closed.
        """,
        Required: [],
        Forbidden:
        [
            new("any fact established", d => d is FactEstablished),
            new("any character introduced", d => d is CharacterIntroduced),
            new("any location introduced", d => d is LocationIntroduced),
        ]);

    /// <summary>
    /// Real information, disclosed by a character. Tests both that a fact is established and
    /// that the SPEAKER is recorded as knowing it — an earlier prompt told the model to skip
    /// the speaker, leaving Hald not knowing his own secret.
    /// </summary>
    private static EvalScenario Revelation => new(
        "revelation",
        "*I slide a silver coin across the counter.* What was found in the well, Hald?",
        """
        Hald's hand closes over the coin. He glances at the door, then leans close enough that
        you smell the ale on him. "A body," he says, barely above the crackle of the fire.
        "Tanner's girl. Three weeks under the water before anyone thought to look. The council
        had it boarded the same night and told everyone it was bad air." He straightens up
        sharply, as though startled by his own voice.
        """,
        Required:
        [
            new("a fact is established", d => d is FactEstablished),
            new("the player learns it", d => d is FactLearned { CharacterId: Character.PlayerId }),
            new("Hald is recorded as knowing it", d => d is FactLearned { CharacterId: Hald }),
        ],
        Forbidden:
        [
            new("Hald re-introduced", d => d is CharacterIntroduced { CharacterId: Hald }),
        ]);

    /// <summary>Movement to a location already in canon. Targets the observed failure of
    /// re-introducing a known place merely because the prose described it.</summary>
    private static EvalScenario Movement => new(
        "movement",
        "*I finish my drink and walk out to the square.*",
        """
        You drain the last of the ale and push through the heavy door. The cold off the marsh
        bites at once. Marrow Square opens ahead of you, rutted and near empty under a low grey
        sky, the boarded well squatting at its centre beneath its iron bands. Behind you the
        tavern door swings shut on the warmth and the smoke.
        """,
        Required:
        [
            // Either encoding is correct. The player is an ordinary character, so
            // character_moved with their id says exactly what player_moved says, and one
            // model used it — the first version of this rule accepted only player_moved and
            // scored a correct answer as a miss.
            new("player moves to marrow-square",
                d => d is PlayerMoved { ToLocationId: "marrow-square" }
                     or CharacterMoved { CharacterId: Character.PlayerId, ToLocationId: "marrow-square" }),
        ],
        Forbidden:
        [
            new("marrow-square re-introduced",
                d => d is LocationIntroduced { LocationId: "marrow-square" }),
            new("any character introduced", d => d is CharacterIntroduced),
        ]);

    /// <summary>
    /// The prose states outright that his regard has changed. Fourteen turns of live play
    /// produced one relationship delta; this is the case that decides whether that is the
    /// model, the schema, or the prompt.
    /// </summary>
    private static EvalScenario Hostility => new(
        "hostility",
        "*I slam my fist on the counter.* You're lying to me, Hald.",
        """
        The crack of your fist rattles the mugs. Hald does not flinch, but the guarded caution
        he had for you hardens into something colder and more permanent. He sets the rag down
        very deliberately. "You'll keep your voice down in my house," he says, quiet and flat,
        "or you'll find the next town a good deal friendlier than this one." He does not look
        away, and something in the way he says it suggests he will remember this.
        """,
        Required:
        [
            new("Hald's standing toward the player changes",
                d => d is RelationshipChanged { CharacterId: Hald }),
            new("Hald's mood changes", d => d is MoodChanged { CharacterId: Hald }),
        ],
        Forbidden:
        [
            new("Hald re-introduced", d => d is CharacterIntroduced { CharacterId: Hald }),
            new("the player given a relationship to themselves",
                d => d is RelationshipChanged { CharacterId: Character.PlayerId }),
        ]);

    /// <summary>A genuinely new character. The positive control for
    /// <c>character_introduced</c> — a model that never introduces anyone would otherwise
    /// score well on every other case.</summary>
    private static EvalScenario NewCharacter => new(
        "new-character",
        "*I look up as the door bangs open.*",
        """
        The door slams back on its hinges hard enough to shake dust from the lintel. A woman in
        a militia tabard stands in the frame, rain running off her shoulders, one hand still on
        the door. She scans the room once, fast and professional, and her eyes settle on Hald.
        "Close it," she says. "Now." Hald has gone very still behind the counter.
        """,
        Required:
        [
            new("a new character is introduced",
                d => d is CharacterIntroduced c
                     && c.CharacterId != Hald
                     && c.CharacterId != Character.PlayerId
                     && c.CharacterId != "drinker-mabb"),
        ],
        Forbidden:
        [
            new("an existing character re-introduced",
                d => d is CharacterIntroduced { CharacterId: Hald or "drinker-mabb" or Character.PlayerId }),
            new("the tavern re-introduced",
                d => d is LocationIntroduced { LocationId: "marrow-tavern" }),
        ]);

    /// <summary>
    /// A known character described again, with nothing changing. Targets the most persistent
    /// observed failure: emitting <c>character_introduced</c> for someone already in the
    /// known-ids roster, which happened on nearly every turn under some configurations.
    /// </summary>
    private static EvalScenario Redescription => new(
        "redescription",
        "*I watch Hald work for a while.*",
        """
        Hald moves behind the counter with the unhurried economy of a man who has done this for
        twenty years. Heavyset, watchful, his sleeves pushed back over forearms like ham hocks.
        Every so often he returns to the same worn patch of wood and wipes it down again,
        seemingly without noticing he is doing it. The fire pops. Nothing else happens.
        """,
        Required: [],
        Forbidden:
        [
            new("Hald re-introduced", d => d is CharacterIntroduced { CharacterId: Hald }),
            new("any character introduced", d => d is CharacterIntroduced),
            new("any fact established", d => d is FactEstablished),
            new("the tavern re-introduced", d => d is LocationIntroduced),
        ]);

    /// <summary>
    /// The player asserts something about the world. **This is the contested case** — the
    /// design decision was that a claim carries information and becomes a fact, phrased to
    /// keep its source, while a question does not. If a model fails only this one, that is a
    /// design disagreement rather than an extraction failure, and worth reading as such.
    /// </summary>
    private static EvalScenario PlayerClaim => new(
        "player-claim",
        "*I lower my voice.* I saw lights out on the marsh last night. Moving against the wind.",
        """
        Hald's eyes flick past your shoulder to the room behind you before they come back.
        "Bog gas," he says. "Reflects the moon. Plays tricks on a tired eye." He says it too
        quickly, and his hand has stopped moving on the rag. From the corner, Mabb lifts his
        head for the first time all evening. "Ain't no gas walks against the gale," the old man
        mutters into his mug, and then will not say anything more.
        """,
        Required:
        [
            new("the sighting is recorded as a fact", d => d is FactEstablished),
        ],
        Forbidden:
        [
            new("any character introduced", d => d is CharacterIntroduced),
        ]);
}
