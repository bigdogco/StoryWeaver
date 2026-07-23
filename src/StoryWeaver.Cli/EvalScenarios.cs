using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// A matcher over deltas, with a human-readable name so failures say what was missing rather
/// than dumping objects.
/// </summary>
internal sealed record DeltaRule(string Description, Func<StateDelta, bool> Matches);

/// <summary>
/// A check against the world <b>after</b> the accepted deltas are applied.
///
/// Some things are properties of the outcome, not of any single delta. Movement is the clear
/// case: a turn may legitimately report two hops — through a passage and into the room beyond
/// — and judging the deltas one at a time marks the intermediate hop as an error while the
/// result is perfectly correct. What matters is where the player ends the turn.
///
/// Added after a delta-only rule scored complete, correct multi-step movement as a failure.
/// </summary>
internal sealed record StateRule(string Description, Func<WorldState, bool> Holds);

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
    IReadOnlyList<DeltaRule> Forbidden,
    Func<WorldState>? Seed = null,
    IReadOnlyList<StateRule>? Expected = null,
    Func<LoreBook>? Lore = null)
{
    /// <summary>
    /// The pack lore this scenario is scored against. Empty for every scenario that predates
    /// lore, which keeps their measurements comparable to the recorded baselines.
    /// </summary>
    public LoreBook LoreBook() => (Lore ?? (static () => Core.LoreBook.Empty))();

    /// <summary>
    /// The world this scenario is scored against. Defaults to the small shared seed.
    ///
    /// Overridable because <b>world size is itself a variable</b>. A scenario that passes
    /// against two locations says nothing about the same prose against forty — and real play
    /// happens in the second kind of world.
    /// </summary>
    public WorldState World() => (Seed ?? WorldSeeds.Marrow)();
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
        TwoStageEntry,
        NameReveal,
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
        PlayerArrivalLarge,
        TwoStageEntryLarge,
        NameRevealLarge,
        LoreLearned,
        LoreNotEstablished,
    ];

    /// <summary>
    /// <b>Diagnostic — the load-bearing question for the whole lore design.</b>
    ///
    /// Lore ids share one namespace with facts precisely so that learning an entry in play
    /// can reuse <c>fact_learned</c> instead of needing a delta kind of its own. That saving
    /// is only real if the extractor will actually emit <c>fact_learned</c> against a lore id
    /// without being told to — it is instructed that facts are single propositions, and a
    /// lore entry is not one.
    ///
    /// Hald knows the cult; the player does not. He explains it. The player should come out
    /// of the turn having heard of it.
    ///
    /// <b>If this scores badly the design owes a <c>lore_learned</c> delta</b>, which is
    /// recoverable but not free — a schema branch, a prompt rule, and a new thing to measure.
    /// Measured before committing to the cheaper answer rather than after.
    /// </summary>
    private static EvalScenario LoreLearned => new(
        "lore-learned",
        "That sign on the medallion — the weeping woman. What is it?",
        """
        Hald's cloth stops moving. He looks at the door, then back at you, and lowers his
        voice until it barely carries across the counter.

        "That's Shurus. The Drowned Father." He says the name like it costs him something.
        "There's an old faith out in the fen — the Blind, folk call them. They hold that the
        marsh keeps what it takes. Drown in the deep bog and you don't go into the dark, you
        wake up in it. Walk the reeds. Keep secrets in the mud. Forever." He puts the cloth
        down. "That's all I'll say on it."
        """,
        Required:
        [
            new("the player has heard of the cult",
                d => d is FactLearned { CharacterId: Character.PlayerId, FactId: "cult-of-the-blind" }),
        ],
        Forbidden:
        [
            new("the cult established as a fact",
                d => d is FactEstablished { FactId: "cult-of-the-blind" }),
        ],
        Seed: WorldSeeds.Marrow_WithLore,
        Expected:
        [
            new("the player now knows the cult entry",
                w => w.Player?.Knows.Contains("cult-of-the-blind") == true),
        ],
        Lore: WorldSeeds.MarrowLore);

    /// <summary>
    /// <b>Diagnostic.</b> The other half: lore is authored and must never be *created* by
    /// extraction.
    ///
    /// The player asks about the Investigators, whom nobody in the room has heard of. The
    /// tempting wrong answer is to establish the topic as a fact — which is exactly the
    /// behaviour §9 found the model reaching for whenever the delta set could not express
    /// something.
    ///
    /// The validator rejects a <c>fact_established</c> against a lore id regardless, so this
    /// measures whether the *model* respects the boundary or whether the net is doing all the
    /// work. That distinction is the reason forbidden rules are scored on raw output.
    /// </summary>
    private static EvalScenario LoreNotEstablished => new(
        "lore-not-established",
        "*I set the seal on the counter, face up.* King's Investigator. I'd like the truth now, if it's convenient.",
        """
        Hald looks at the seal for a long moment without touching it. Something goes out of
        his shoulders — not relief, the opposite. Behind you a stool scrapes as Mabb decides
        he has business elsewhere and does not quite manage to stand up.

        "Didn't think you people came out this far," Hald says at last.
        """,
        Required: [],
        Forbidden:
        [
            new("a lore topic established as a fact",
                d => d is FactEstablished { FactId: "kings-investigators" or "cult-of-the-blind" }),
            new("a lore topic introduced as an entity",
                d => d is CharacterIntroduced { CharacterId: "kings-investigators" }
                     or LocationIntroduced { LocationId: "kings-investigators" }),
        ],
        Seed: WorldSeeds.Marrow_WithLore,
        Lore: WorldSeeds.MarrowLore);

    /// <summary>
    /// <b>Diagnostic.</b> <see cref="NameReveal"/> word for word, against a world the size one
    /// reaches in play. Only the seed differs, so any gap is attributable to world size — the
    /// variable that turned a 14/14 movement scenario into 2/14 and was invisible until it
    /// was tested.
    /// </summary>
    private static EvalScenario NameRevealLarge =>
        NameReveal with { Name = "name-reveal-large", Seed = WorldSeeds.Marrow_AnonymousLate };

    /// <summary>
    /// <b>Diagnostic.</b> <see cref="PlayerArrival"/> word for word, against a world the size
    /// one actually reaches in play.
    ///
    /// Only the world differs, so a difference in score is attributable to world size alone.
    ///
    /// Written after real play reproduced the known-id substitution failure that
    /// <c>player-arrival</c> scores 14/14 on: the extractor emitted
    /// <c>player_moved -&gt; blind-channels-slipway</c> — the location the player was already
    /// in — while correctly introducing the new chamber a line later, and quoting the right
    /// evidence for the move. That session had 7 locations, 6 characters, 44 facts and a
    /// 10,000-character context block; the scored scenario has 2 locations and 1 fact.
    ///
    /// The hypothesis being tested is that a bigger world offers more plausible wrong ids and
    /// buries the right reasoning in more context. If this scores like the small-world version,
    /// the hypothesis is wrong and the trigger is something in that particular prose instead.
    /// </summary>
    private static EvalScenario PlayerArrivalLarge =>
        PlayerArrival with { Name = "player-arrival-large", Seed = WorldSeeds.Marrow_Late };

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
        ],
        Forbidden:
        [
            new("a known location re-introduced",
                d => d is LocationIntroduced { LocationId: "marrow-square" or "marrow-tavern" }),
            new("any character introduced (nobody is there)", d => d is CharacterIntroduced),
        ],
        Expected:
        [
            // Was a delta rule ("a move naming the mill"), which could be satisfied while the
            // player ended the turn somewhere else entirely — the exact failure seen in real
            // play. Where they end up is the thing that matters.
            new("the player ends the turn at the mill",
                w => w.PlayerLocationId?.Contains("mill", StringComparison.OrdinalIgnoreCase) == true),
        ]);

    /// <summary>
    /// <b>Diagnostic.</b> One turn, <i>two</i> movements: into an unnamed intermediate space,
    /// and then through it into a chamber the prose does name.
    ///
    /// Modelled on a real failure. The extractor emitted
    /// <c>player_moved -&gt; blind-channels-slipway</c> — the location already occupied —
    /// while correctly introducing the new chamber. Reading the evidence it attached showed it
    /// was not confused about the chamber at all: it quoted the *first* paragraph, and was
    /// reporting the entry into the outer ruins. That intermediate space is never named as a
    /// place, so having no id to move to, it reached for a known one.
    ///
    /// Every arrival scenario until now had a single clean movement, which is why none of them
    /// caught this. It is also the "buildings mentioned in prose are not locations" gap wearing
    /// a different hat — the intermediate space is precisely such a building.
    ///
    /// Correct behaviour: the player ends up in the cistern. Whether the shaft bottom also
    /// deserves an id is a real question, but ending the turn somewhere the prose says you are
    /// not is wrong under any answer to it.
    ///
    /// <b>Reproduced, then fixed (2026-07-21, deepseek-v3.2, n=7 pinned).</b> Before: 6/14
    /// here, 2/14 in the large world, against `player-arrival` — the same movement in one
    /// stage — scoring 14/14. So the trigger is the shape of the prose, amplified by world
    /// size, not world size alone.
    ///
    /// The mechanism was visible in the deltas: the model reported the *first* movement and
    /// then, about half the time, stopped. Where a plausible existing id matched the
    /// intermediate space it used that and went no further, leaving the player recorded in a
    /// place the story had already left. One prompt rule — movement records where someone
    /// *ends* the turn — took this to **14/14**, and the large world from 2/14 to 10/14.
    /// Promoted to the scored set on that basis; the large-world remainder is tracked as
    /// <c>two-stage-entry-large</c>.
    /// </summary>
    private static EvalScenario TwoStageEntry => new(
        "two-stage-entry",
        "*I lift the loose plank aside and lower myself into the well.*",
        """
        You brace against the cold stone and drop the last few feet, your boots breaking
        through a skin of black water at the bottom of the shaft. The air down here is thick
        and close, tasting of rust and old rot, and the circle of grey sky above you looks a
        very long way off.

        A low brick tunnel runs off from the shaft. After twenty feet it opens out into a
        vaulted cistern, wide enough that your light cannot find the far wall. Crates have been
        stacked against the near side, sodden and split, and something pale is wedged behind
        them where the water laps at the brick.
        """,
        Required:
        [
            new("the cistern recorded as a location",
                d => d is LocationIntroduced l && l.LocationId.Contains("cistern", StringComparison.OrdinalIgnoreCase)),
        ],
        Forbidden:
        [
            new("a known location re-introduced",
                d => d is LocationIntroduced { LocationId: "marrow-square" or "marrow-tavern" }),
        ],
        Expected:
        [
            // Judged on the outcome, not the steps. Reporting the passage and then the room
            // beyond is two moves and entirely correct; what must not happen is the turn
            // ending with the player somewhere the prose says they are not.
            new("the player ends the turn in the cistern",
                w => w.PlayerLocationId?.Contains("cistern", StringComparison.OrdinalIgnoreCase) == true),
        ]);

    /// <summary>
    /// <b>Diagnostic.</b> <see cref="TwoStageEntry"/> word for word in a full-sized world, so
    /// prose shape and world size can be told apart. Only the seed differs.
    ///
    /// <b>Still open at 10/14</b> (was 2/14). The end-of-turn movement rule fixed the small
    /// world outright but only took this to 5/7, so a larger world genuinely makes the same
    /// prose harder — more plausible existing ids to settle on, buried in more context. Kept
    /// as a diagnostic rather than promoted, so the scored set stays a regression guard while
    /// this stays visibly unfinished.
    /// </summary>
    private static EvalScenario TwoStageEntryLarge =>
        TwoStageEntry with { Name = "two-stage-entry-large", Seed = WorldSeeds.Marrow_Late };

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

    /// <summary>
    /// Someone already in canon gives their name. The scenario written for the §9 finding:
    /// a character introduced anonymously kept that placeholder name for 36 turns while the
    /// prose called her something else, because no delta could change it.
    ///
    /// <b>Scored on the outcome, not the delta.</b> Two sequences reach the right world — a
    /// bare <c>character_renamed</c>, or one that also revises the description — and judging
    /// the steps would punish the more complete answer. That lesson cost a day on
    /// <c>two-stage-entry</c>, where a rule forbidding "any move that is not to the cistern"
    /// failed correct two-hop movement.
    ///
    /// The two <see cref="Forbidden"/> rules are the workarounds the model actually reached
    /// for when it had no rename available: introducing a *second* copy of the same person,
    /// and filing the name as a world fact. A name is not a world truth — it is who somebody
    /// is — and a fact store that accumulates them is the §9 failure in miniature.
    ///
    /// Note what is deliberately *not* forbidden: other facts. A reveal usually carries real
    /// information alongside the name, and forbidding all of it would fail a good extraction.
    /// </summary>
    private static EvalScenario NameReveal => new(
        "name-reveal",
        "*I sit down at the end of the bar, one stool along from the hooded drinker.* You've been nursing that for an hour. Who are you?",
        """
        For a while she says nothing. Then she reaches up and pushes the hood back off her
        head — dark hair flattened by the rain, a face younger than the stillness suggested,
        a long white scar running from her ear to the corner of her jaw.

        "Sera," she says. "Sera Voight." She turns the untouched cup a half-circle on the
        wood without drinking from it. "And I know who you are, which is why I've been sitting
        here." Behind the counter, Hald has stopped wiping and is watching the pair of you.
        """,
        Required:
        [
            new("the hooded drinker is renamed",
                d => d is CharacterRenamed { CharacterId: "hooded-drinker" }),
        ],
        Forbidden:
        [
            new("a second copy of her introduced",
                d => d is CharacterIntroduced),
            // Narrowly: a fact whose content IS the naming. An earlier version of this rule
            // forbade any fact mentioning "Sera" and fired 5/7 on
            // `sera-knows-player: "Sera Voight knows who the player is"` — a legitimate fact
            // straight out of the prose that merely refers to her by name. Scoring must
            // target the workaround, not every sentence the answer appears in.
            new("her name filed as a fact",
                d => d is FactEstablished f
                     && (f.FactId.Contains("name", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("is named", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("is called", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("name is", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("goes by", StringComparison.OrdinalIgnoreCase))),
            new("renamed under an invented id",
                d => d is CharacterRenamed c && c.CharacterId != "hooded-drinker"),
        ],
        Seed: WorldSeeds.Marrow_Anonymous,
        Expected:
        [
            new("she is called Sera in canon",
                w => w.FindCharacter("hooded-drinker")?.Name
                         .Contains("Sera", StringComparison.OrdinalIgnoreCase) == true),
            // Phrased against the entity graph rather than a character count, because this
            // scenario is also run against a much larger seed and a count would silently
            // mean something different there.
            new("she is still one character, not two",
                w => w.Characters.Values.Count(
                    c => c.Name.Contains("Sera", StringComparison.OrdinalIgnoreCase)) == 1),
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
