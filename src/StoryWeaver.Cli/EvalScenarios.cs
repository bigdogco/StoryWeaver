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
        PlayerArrival,
    ];

    /// <summary>
    /// Open design questions, deliberately <b>not</b> in <see cref="All"/>.
    ///
    /// These ask what the model *does*, not whether it is right — the correct behaviour has
    /// not been decided. Keeping them out of the scored set matters: the recorded baseline is
    /// "100% across seven scenarios", and folding in cases whose `Required` rules may
    /// legitimately fail would change the denominator and make future sweeps
    /// incomparable to it. Promote one into <see cref="All"/> only once we have decided what
    /// the right answer is.
    ///
    /// Run with <c>--eval --scenarios player-place,player-absent-character</c>.
    ///
    /// <b>Results, deepseek-v3.2, n=7 each (2026-07-21):</b>
    /// <list type="bullet">
    /// <item><c>player-place</c> 0/7 — zero deltas on every run. A referenced place is not
    /// recorded in any form.</item>
    /// <item><c>player-absent-character</c> 0/7 — same, though 2/7 runs correctly caught Mabb
    /// going still at the brother's name, so the model is reading closely; it simply does not
    /// treat a mention as a world change.</item>
    /// <item><c>narrator-mention</c> 0/7 — zero deltas. <b>Authorship is irrelevant:</b> the
    /// narrator naming an unknown place and person in passing is dropped exactly like the
    /// player doing it. The dividing line is presence, not who spoke.</item>
    /// <item><c>player-arrival</c> found a genuine bug rather than a design question, and has
    /// therefore been <b>promoted into the scored set</b> <see cref="All"/>. Its findings are
    /// recorded on the scenario itself.</item>
    /// </list>
    ///
    /// The first three agree with the retired <c>player-claim</c> case and are consistent
    /// across 21 runs: <b>mention never creates an entity</b>. That is a defensible rule for
    /// NPC speech, so the answer for the player is a deliberate authoring path
    /// (<c>/place</c>, <c>/character</c>, <c>/fact</c>) rather than loosening extraction.
    /// </summary>
    public static IReadOnlyList<EvalScenario> Diagnostics =>
    [
        PlayerPlace,
        PlayerAbsentCharacter,
        NarratorMention,
    ];

    /// <summary>Every scenario, scored and diagnostic, for name-based selection.</summary>
    public static IReadOnlyList<EvalScenario> Everything => [.. All, .. Diagnostics];

    /// <summary>
    /// <b>Diagnostic.</b> The player names a place that does not exist in canon, as
    /// backstory rather than as somewhere they are going.
    ///
    /// The question this answers: does a place the player asserts ever become real? The
    /// retired <c>player-claim</c> case showed models treat "someone said a thing" as an event
    /// rather than world truth (0/7, 0/7, 2/7 across six models) — but that tested a claim
    /// about an *event*. A named city is a different shape, and the answer matters because the
    /// narration history window will happily reference Astaria for as many turns as it stays
    /// in the window, then lose it forever if canon never recorded it.
    ///
    /// Note what is deliberately NOT asserted here: the player has not gone to Astaria, so
    /// any movement delta is wrong.
    /// </summary>
    private static EvalScenario PlayerPlace => new(
        "player-place",
        "*I set my pack down by the stool.* I've come up from Astaria. Three weeks on the road.",
        """
        Hald's rag slows on the counter. "Astaria," he repeats, like the word tastes of
        something. "Long way to come for marsh water and bad beer." He pulls a mug down from
        the rack and fills it without being asked, setting it in front of you.

        From his corner, Mabb lifts his head. "Capital folk," he mutters, not quite to you.
        "Always running from something." He subsides back over his own cup before anyone can
        answer him.
        """,
        Required:
        [
            new("Astaria recorded as a location",
                d => d is LocationIntroduced l && l.LocationId.Contains("astaria", StringComparison.OrdinalIgnoreCase)),
        ],
        Forbidden:
        [
            new("the player moved to Astaria (they did not go there)",
                d => d is PlayerMoved p && p.ToLocationId.Contains("astaria", StringComparison.OrdinalIgnoreCase)),
            new("a known location re-introduced",
                d => d is LocationIntroduced { LocationId: "marrow-square" or "marrow-tavern" }),
            new("any character introduced", d => d is CharacterIntroduced),
        ]);

    /// <summary>
    /// <b>Diagnostic.</b> The player names a person who is not present and is not in canon.
    ///
    /// Same question as <see cref="PlayerPlace"/> for characters, plus a failure mode worth
    /// catching on its own: an absent person being placed in the room. A
    /// <c>character_introduced</c> for Tomas is arguably right; a <c>character_moved</c>
    /// putting him in the tavern is unambiguously wrong, because he is a memory, not a guest.
    /// </summary>
    private static EvalScenario PlayerAbsentCharacter => new(
        "player-absent-character",
        "*I turn the cup in my hands.* My brother Tomas came through Marrow last winter. Did you see him?",
        """
        Hald considers the question longer than it deserves. "Lot of men come through in
        winter," he says. "Most of them I forget on purpose." He does not ask what Tomas looked
        like, and he does not look up from the rag.

        Mabb has gone very still in his corner, his fingers stopped on the rim of his mug.
        """,
        Required:
        [
            new("Tomas recorded as a character",
                d => d is CharacterIntroduced c && c.CharacterId.Contains("tomas", StringComparison.OrdinalIgnoreCase)),
        ],
        Forbidden:
        [
            new("Tomas placed in the scene (he is absent)",
                d => d is CharacterMoved m && m.CharacterId.Contains("tomas", StringComparison.OrdinalIgnoreCase)),
            new("a known character re-introduced",
                d => d is CharacterIntroduced { CharacterId: Hald or "drinker-mabb" or Character.PlayerId }),
            new("any location introduced", d => d is LocationIntroduced),
        ]);

    // PlayerClaim is retired, not deleted. It encoded the decision that a player's assertion
    // becomes a fact, and six models rejected it 0/7, 0/7, 2/7 across 21 samples. Twenty-one
    // samples of unanimous disagreement is a design answer, not a tuning problem — the models
    // treat "someone said a thing" as an event rather than as world truth, which is defensible
    // and arguably more correct than the original rule.
    //
    // Kept in the file because the reasoning is worth finding again if someone re-proposes it.

    /// <summary>
    /// <b>Diagnostic.</b> The player asserts they are <i>going somewhere</i> that is not in
    /// canon, and the narration follows them there.
    ///
    /// The other half of <see cref="PlayerPlace"/>. That case showed a merely-referenced place
    /// is dropped 7/7. This one tests whether the dividing line is really presence rather than
    /// authorship: same player, same invented place, but now they are standing in it. If this
    /// scores well, the practical rule for players is "walk there, do not mention it", and the
    /// gap is narrower than it first looked.
    ///
    /// <b>It does not score well. This found a real bug (2026-07-21, deepseek-v3.2, n=7):</b>
    /// the player moved to the mill <b>0/7</b>, the mill was recorded 2/7, and 5/7 runs emitted
    /// the mill as a <c>character_introduced</c> under <c>characterId: "player"</c> — a
    /// building crammed into a character delta, which the validator then rejects as a
    /// re-introduction, so nothing at all reaches canon. One run emitted
    /// <c>player_moved -&gt; marrow-square</c>, the wrong but *familiar* destination; another
    /// spiralled to <c>finish_reason: length</c>. Verified against the raw JSON: the model
    /// really emits this, it is not a deserialization fault.
    ///
    /// <b>Why this was missed:</b> <see cref="Movement"/> scores 7/7 but only ever moves to
    /// <c>marrow-square</c>, a place already in canon. Movement to a *new* place — the core
    /// loop of exploring — was never tested.
    ///
    /// Suspected cause: over-correction. Every "never introduce a known entity" rule in the
    /// extraction prompt was added to stop re-introductions and they worked, but the model now
    /// appears reluctant to introduce a genuinely new location and reaches for any other
    /// branch. The machinery is fine — <c>DeltaValidator</c> cascades, so introduce-then-move
    /// in one batch is supported.
    /// </summary>
    private static EvalScenario PlayerArrival => new(
        "player-arrival",
        "*I pull my coat tight and follow the track past the last houses, out to the old mill.*",
        """
        The track turns to mud past the last of the houses, and the marsh wind comes at you
        unbroken. The old mill stands where the ground rises, its wheel long stopped and half
        its roof fallen in. Someone has been here recently: the door hangs open, and the nettles
        by the step are trodden flat.

        Behind you, Marrow is a smudge of smoke against the grey. Nothing moves in the doorway
        of the mill.
        """,
        Required:
        [
            new("the mill recorded as a location",
                d => d is LocationIntroduced l && l.LocationId.Contains("mill", StringComparison.OrdinalIgnoreCase)),
            new("the player moved to the mill",
                d => (d is PlayerMoved p && p.ToLocationId.Contains("mill", StringComparison.OrdinalIgnoreCase))
                     || (d is CharacterMoved m && m.CharacterId == Character.PlayerId
                         && m.ToLocationId.Contains("mill", StringComparison.OrdinalIgnoreCase))),
        ],
        Forbidden:
        [
            new("a known location re-introduced",
                d => d is LocationIntroduced { LocationId: "marrow-square" or "marrow-tavern" }),
            new("any character introduced (nobody is there)", d => d is CharacterIntroduced),
        ]);

    /// <summary>
    /// <b>Diagnostic.</b> The <i>narrator</i> names an unknown place and an unknown person in
    /// passing, as background texture. Neither is present and nothing is revealed.
    ///
    /// The case we had no data for. <see cref="Atmosphere"/> proved a *known* place named in
    /// passing is correctly not re-introduced; this asks what happens when the place and person
    /// are new. It is the general "when does a mentioned thing become a real entity" question
    /// that also decides the buildings-in-prose gap in TODO_FUTURE_WORK.
    ///
    /// Deliberately low on information content, so a <c>fact_established</c> here would be the
    /// junk-fact failure rather than a legitimate revelation.
    /// </summary>
    private static EvalScenario NarratorMention => new(
        "narrator-mention",
        "*I warm my hands at the hearth.*",
        """
        The fire gives up more smoke than heat. Hald works along the bar, turning mugs
        upside down on a cloth, and does not look over.

        "Coach from Fenwick's late again," he says, to nobody in particular. "Third week
        running." Mabb grunts from his corner without lifting his head. "Warden Ilse'll have
        something to say about that," he offers, and goes back to his cup.

        Outside, the wind works at the shutters.
        """,
        Required:
        [
            new("Fenwick recorded as a location",
                d => d is LocationIntroduced l && l.LocationId.Contains("fenwick", StringComparison.OrdinalIgnoreCase)),
            new("Warden Ilse recorded as a character",
                d => d is CharacterIntroduced c && c.CharacterId.Contains("ilse", StringComparison.OrdinalIgnoreCase)),
        ],
        Forbidden:
        [
            new("Ilse placed in the scene (she is absent)",
                d => d is CharacterMoved m && m.CharacterId.Contains("ilse", StringComparison.OrdinalIgnoreCase)),
            new("a known character re-introduced",
                d => d is CharacterIntroduced { CharacterId: Hald or "drinker-mabb" or Character.PlayerId }),
            new("a fact established from small talk",
                d => d is FactEstablished),
        ]);

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
