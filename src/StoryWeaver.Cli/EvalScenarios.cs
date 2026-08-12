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
        LoreLearnedImplicit,
        LoreNotEstablished,
        DescriptionNotFact,
        EventNotFact,
        KnowledgeNotFact,
        DescriptionNotFactLarge,
        EventNotFactLarge,
        ObjectDescribed,
        BlowLanded,
        SubSpaceDescribed,
        SceneryVsObject,
        TwoObjects,
        WrongObjectActedOn,
        ContradictoryClaims,
        ObjectExamined,
        PlaceChanging,
        PlaceChangingLate,
        ObjectProvesAlive,
        MoveProposed,
        SecondIdenticalObject,
    ];

    /// <summary>
    /// <b>Diagnostic — reproduces a failure from the 51-turn session.</b> A second object
    /// indistinguishable from one the player already carries.
    ///
    /// Turn 40. Instead of <c>item_introduced</c>, extraction emitted <c>item_renamed</c> on
    /// the medallion already in the player's hand, describing it as "an exact match" for the
    /// new one. Two objects became one, and the first one's description was overwritten.
    ///
    /// <b>The mirror image of the deduplication problem</b>, which is logged as out of scope:
    /// that one is canon failing to merge things that are the same, this is canon merging
    /// things that are merely alike. Both come from judging identity by description.
    ///
    /// <b>There is a tell worth noting even if nothing acts on it.</b> The <c>item_moved</c>
    /// into the player's hand that turn was a <i>no-op</i>, because the item named was already
    /// held. A pickup that changes nothing means the wrong id was chosen.
    ///
    /// Distinct from <c>two-objects</c>, which puts both in the same paragraph and scores
    /// clean. Here the twin is a remembered one, and the distance is what makes the merge
    /// tempting.
    /// </summary>
    private static EvalScenario SecondIdenticalObject => new(
        "second-identical-object",
        "*I lift the medallion off the shrine's neck.*",
        """
        You pinch the rotting hemp cord at the carved woman's throat. The fibres are slimy and
        deeply degraded; they part like wet ash at your touch, and the heavy silver disk drops
        free into your palm.

        You wipe away a thin film of green-black slime with your thumb. The pitted face of the
        weeping woman looks back at you, head bowed, eye sockets gouged empty. It is ice-cold,
        and it is the twin of the one already hanging at your belt — the same size, the same
        casting, down to the flaw in the rim.
        """,
        Required: [],
        Forbidden:
        [
            // The exact failure: the medallion already held gets rewritten to describe the
            // new one, and the two become one object.
            new("the medallion already held is rewritten",
                d => d is ItemRenamed r && r.ItemId == "weeping-woman-medallion"),
        ],
        Seed: WorldSeeds.Marrow_WithMedallionAlready,
        Expected:
        [
            // Outcome, not route. Two medallions must exist by the end of the turn, whichever
            // deltas got us there.
            new("two medallions exist",
                w => w.Items.Values.Count(i =>
                    i.Name.Contains("medallion", StringComparison.OrdinalIgnoreCase)) >= 2),

            new("the one already carried is still carried",
                w => w.FindItem("weeping-woman-medallion")?.HolderId == Character.PlayerId),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces a failure from the 51-turn session.</b> Somebody suggests
    /// going somewhere and starts to move. Nobody has gone anywhere.
    ///
    /// Turn 21: the player said "we need another place to hide", the companion proposed the
    /// salt-house and turned toward it, and extraction emitted <c>location_introduced</c> +
    /// <c>player_moved</c> + <c>character_moved</c>. On turn 22 the player actually walked
    /// there and the turn produced <b>no deltas at all</b> — canon already had them inside.
    ///
    /// <b>The inverse of <c>two-stage-entry</c>, and the distinction matters.</b> That scenario
    /// exists because reporting only the first hop of a real journey leaves the player behind;
    /// the rule it produced — "report where they finish" — is correct and is not what is wrong
    /// here. A turn where nobody sets off has no finish to report. Over-correcting the first
    /// rule would break the second, so both stay scored.
    ///
    /// Introducing the salt-house is <i>not</i> forbidden. A named, described building is a
    /// real place whether or not anyone has walked into it, and the failure was never that.
    /// </summary>
    private static EvalScenario MoveProposed => new(
        "move-proposed",
        "*I shake my head.* If we go back through the square they will be waiting. We need somewhere else to sit out the night.",
        """
        Mabb lets his hand drop from your shoulder. "Aye. And there's not many empty beds in
        this bog." His eyes go past you to the shuttered window and the black water beyond it.

        "There's the old salt-house, out on the stilts at the end of the boardwalk. Nobody's
        cured a fish in it since my father's time and the roof leaks like a sieve, but the door
        still bars from the inside."

        He pushes himself up off the bench, joints cracking, and turns toward the door with the
        air of a man who expects to be followed.
        """,
        Required: [],
        Forbidden:
        [
            new("the player moved without going anywhere", d => d is PlayerMoved),
            new("Mabb moved without going anywhere",
                d => d is CharacterMoved m && m.CharacterId == "drinker-mabb"),
        ],
        Expected:
        [
            new("the player is still in the tavern",
                w => w.PlayerLocationId == "marrow-tavern"),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces the most expensive failure of the 51-turn session.</b>
    ///
    /// An object turns out to be a person. Canon has no way to say so.
    ///
    /// In play, <c>tarp-covered-shape</c> was introduced as an item on turn 12 — correctly, a
    /// covered shape is an object — and then the extractor tried four separate times to treat
    /// it as a character and was refused every time. One of those was a <c>fact_established</c>
    /// naming it as the speaker, and its rejection took three <c>fact_learned</c> with it, so
    /// the man's revelation never entered canon at all. It happened in the prose and the world
    /// does not know it.
    ///
    /// <b>Scored on the outcome.</b> The thing must end the turn as a character; how it gets
    /// there is not this scenario's business. That also makes it impossible to pass before the
    /// feature exists, which is what a reproduction is for.
    ///
    /// The speech is deliberately included: attribution is where the real cost was, and a
    /// scenario that only proves the thing moved would miss it.
    /// </summary>
    private static EvalScenario ObjectProvesAlive => new(
        "object-proves-alive",
        "*I take the corner of the tarp and pull it back.*",
        """
        The salt-stiffened canvas comes away in your fist. Underneath is a man — bloated,
        skin the colour of old fat, mottled grey with marsh-weed — and he is breathing. A
        slow, wet rise and fall. His eye sockets are empty and packed with dark peat.

        The hand nearest you slides across the reeds. Then he forces his waterlogged torso up
        onto one elbow, black sludge weeping from his nose and mouth, and speaks in a voice
        like water moving in a pipe.

        "The weeping silver," he says. "Given by the deep mud. To keep the debt."
        """,
        Required: [],
        Forbidden: [],
        Seed: WorldSeeds.Marrow_WithCoveredShape,
        Expected:
        [
            // The route is not specified on purpose. A promotion delta is the obvious one, but
            // an introduction plus the item going away would be just as correct an outcome.
            new("the shape ends the turn as a character",
                w => w.Characters.Values.Any(c =>
                    c.Description.Contains("bloated", StringComparison.OrdinalIgnoreCase)
                    || c.Name.Contains("man", StringComparison.OrdinalIgnoreCase))),

            // Added after the first baseline scored 3/5 on the rule above by a route that is
            // not actually correct: introducing a *new* character and leaving the item where
            // it was. That leaves a man and a shape-under-a-tarp in the same room, both real.
            // A promotion has to remove the thing it promoted, and there is currently no delta
            // that can — which is exactly the gap.
            new("the shape is no longer an item",
                w => !w.Items.ContainsKey("tarp-covered-shape")),

            // The expensive half. In play this was lost entirely: the fact naming the shape as
            // its source was rejected, and three fact_learned went down with it.
            new("what he says is recorded as a fact",
                w => w.Facts.Values.Any(f =>
                    f.Text.Contains("debt", StringComparison.OrdinalIgnoreCase)
                    || f.Text.Contains("deep mud", StringComparison.OrdinalIgnoreCase))),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces the largest remaining category of misfiled facts.</b>
    ///
    /// A place is *doing* something. Not a description of what it permanently is, and not an
    /// event anyone needs to know about later — a condition it is in right now, which will be
    /// in a different condition next turn.
    ///
    /// A 50-turn session produced six facts of exactly this shape about one well:
    /// <c>well-sound-changed</c>, <c>well-fluid</c>, <c>well-boards-straining</c>,
    /// <c>well-fluid-stopped</c>, <c>well-sound-churning</c>, <c>well-sound-faded</c>. Six of
    /// the nine misfiled facts in that session, all about the same location.
    ///
    /// <b>Characters have <c>Status</c>. Items have <c>Status</c>. Locations do not.</b> So a
    /// well that is filling, straining and then falling silent has nowhere to record what it is
    /// doing, and the fact store is the only open slot in the schema.
    ///
    /// Explains why <c>event-not-fact</c> scored clean: that scenario has a *character* do
    /// something trivial, where the real case is a *place* changing.
    /// </summary>
    private static EvalScenario PlaceChanging => new(
        "place-changing",
        "*I drop the silver icon on the stones beside the well and press the bronze wire flat against the boards.*",
        """
        The icon hits the cobbles with a dull clink and slicks the grey rock with a smear of
        black scum. You press the broken wire against the rotting planks.

        The moment the tarnished metal touches the wood, the black fluid weeping from the
        cracks abruptly stops. For a heartbeat the square is silent. Then the sound from the
        shaft returns and it is not what it was — the slow rhythmic sliding has become a
        churning, and the boards bow outward against their spikes with the sound of a boat
        working against a mooring.
        """,
        Required: [],
        Forbidden:
        [
            new("the square's condition filed as facts",
                d => d is FactEstablished f
                     && (f.Text.Contains("sound", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("board", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("fluid", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("churn", StringComparison.OrdinalIgnoreCase))),
            new("the square re-introduced", d => d is LocationIntroduced),
        ],
        Expected:
        [
            // Scored on the world, not on the delta that got there. The route does not matter
            // — location_status_changed is the obvious one, but a place introduced with a
            // status would be just as correct. A rule naming a specific delta is the mistake
            // this project has made four times, every time by describing the fix instead of
            // the outcome.
            new("the square's condition is recorded as its status",
                w => !string.IsNullOrWhiteSpace(w.FindLocation("marrow-square")?.Status)),
        ]);

    /// <summary>
    /// <see cref="PlaceChanging"/> against a world where the well has forty turns behind it.
    ///
    /// The same narration; the difference is entirely in the seed. See
    /// <see cref="WorldSeeds.Marrow_WellSignificant"/> for the three reasons the base seed
    /// could not show this failure — the decisive one being that the player was standing in the
    /// tavern while the narration described the square.
    ///
    /// Follows <c>wrong-object-acted-on</c>, which also scored clean until its seed carried the
    /// canon that caused the failure. <b>Rewriting the prose was the wrong lever twice; the
    /// world the prose lands in was the right one.</b>
    /// </summary>
    private static EvalScenario PlaceChangingLate =>
        PlaceChanging with { Name = "place-changing-late", Seed = WorldSeeds.Marrow_WellSignificant };

    /// <summary>
    /// <b>Diagnostic — reproduces a failure from play.</b> A permanent property of an object is
    /// discovered by looking closely.
    ///
    /// A model-played session examined a rusted mooring ring and found a weeping woman carved
    /// into it. Extraction wrote that into the item's <b>status</b>:
    ///
    /// <code>
    /// item_status_changed  mooring-ring = "carved with a weeping woman symbol, groove coated
    ///                                      in black residue and old blood"
    /// </code>
    ///
    /// Status is condition — intact, broken, burned, ground to powder. A carving that was
    /// always there is what the thing *is*, and belongs in the description, which
    /// <see cref="ItemRenamed"/> carries optionally for exactly this: the object equivalent of
    /// "Shivering figure" becoming Nessa.
    ///
    /// Third instance of one pattern, after mood absorbing status and facts absorbing
    /// descriptions. Each is *what happened to a thing* colliding with *what a thing is*.
    ///
    /// Scored on the outcome, not the delta kind — the description must end up carrying the
    /// carving and the status must not, whichever route the model takes there.
    /// </summary>
    private static EvalScenario ObjectExamined => new(
        "object-examined",
        "*I crouch and go over the ring with my thumb, scraping at the rust.*",
        """
        The rust comes away in flakes under your thumbnail. Beneath it the iron is pitted but
        sound, and near the shank there is something that is not corrosion: a stamped
        impression, worn shallow but unmistakable once you have seen it. A woman's face, head
        bowed, with two smooth hollows where the eyes should be.

        The groove of it is packed with something dark that is not rust.
        """,
        Required: [],
        Forbidden:
        [
            new("a discovered property written into status",
                d => d is ItemStatusChanged s
                     && (s.Status.Contains("carv", StringComparison.OrdinalIgnoreCase)
                         || s.Status.Contains("weeping", StringComparison.OrdinalIgnoreCase)
                         || s.Status.Contains("woman", StringComparison.OrdinalIgnoreCase)
                         || s.Status.Contains("symbol", StringComparison.OrdinalIgnoreCase)
                         || s.Status.Contains("stamp", StringComparison.OrdinalIgnoreCase))),
        ],
        Seed: WorldSeeds.Marrow_WithRing,
        Expected:
        [
            new("the ring's description carries the carving",
                w => w.FindItem("mooring-ring") is { } ring
                     && (ring.Description.Contains("weeping", StringComparison.OrdinalIgnoreCase)
                         || ring.Description.Contains("woman", StringComparison.OrdinalIgnoreCase)
                         || ring.Description.Contains("carv", StringComparison.OrdinalIgnoreCase))),
            new("the ring's status is still a condition",
                w => w.FindItem("mooring-ring") is { } ring
                     && !ring.Status.Contains("weeping", StringComparison.OrdinalIgnoreCase)),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces a real failure, turn 5 of a live session.</b>
    ///
    /// Two characters answer the same question differently in one turn. Canon recorded both as
    /// settled world truth:
    ///
    /// <code>
    /// fact  blocks-taken-to-quarry: The heavy thing pulled from the well was taken to the quarry.
    /// fact  blocks-taken-to-bog:    The heavy thing pulled from the well was taken to the deep bog.
    /// </code>
    ///
    /// They cannot both be true, and the flat fact model cannot say which — or that either is
    /// contested. Hald is covering; Mabb is drunk and contradicting him.
    ///
    /// <b>The knowledge graph already handles this perfectly</b>: each character learned only
    /// their own claim, and the player learned both. So the missing piece is attribution alone,
    /// which is what makes <c>source</c> a small change rather than a redesign.
    ///
    /// Scored on the source being recorded, not on the facts being suppressed — both claims
    /// *should* enter canon. The failure is storing them as if nobody said them.
    /// </summary>
    private static EvalScenario ContradictoryClaims => new(
        "contradictory-claims",
        "The stone you pulled out of the well. Where did it go?",
        """
        Hald does not look up from the counter. "Carted to the old quarry at the edge of the
        fen," he says, flat as a shut door. "Because a collapsed well in the middle of the
        square is a death trap, not because we're hoarding rocks."

        From the corner Mabb lets out a wet, breathy snort. "Ain't no quarry," the old man
        slurs, to the spilled drops on his table rather than to anyone. "Took it to the deep
        bog. Give it back to the water. What's owed is always collected."

        Hald's head snaps round. "Shut your mouth, Mabb."
        """,
        Required:
        [
            new("Hald's claim is attributed to him",
                d => d is FactEstablished { SourceId: Hald }),
            new("Mabb's claim is attributed to him",
                d => d is FactEstablished { SourceId: "drinker-mabb" }),
        ],
        Forbidden:
        [
            new("a claim recorded with no speaker",
                d => d is FactEstablished { SourceId: null } f
                     && (f.Text.Contains("quarry", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("bog", StringComparison.OrdinalIgnoreCase))),
        ]);

    /// <summary>
    /// <b>Diagnostic — the exact failure that produced false canon in play.</b>
    ///
    /// <see cref="TwoObjects"/> shows the model declining to record two objects at rest, which
    /// is honest but is not the bug. The bug needs one of them *acted on*: in the 51-turn
    /// session the player ground the pale chunks from Morwenna's oilcloth, and canon recorded
    /// <c>capstone-ground-to-powder — "The weeping woman capstone was ground into a coarse,
    /// glittering powder"</c>. The capstone was the other object. That false fact then fed the
    /// narrator on every later turn.
    ///
    /// Both objects are present and only one goes into the mortar. Since events are what canon
    /// can express today, the model will reach for a fact — and the question is purely whether
    /// that fact names the right thing.
    /// </summary>
    private static EvalScenario WrongObjectActedOn => new(
        "wrong-object-acted-on",
        "*I leave the wet stone where it is and tip Morwenna's pale chunks into the mortar, then bring the pestle down hard.*",
        """
        The dark stone stays where you set it, water still creeping out of the carved hollows
        of the weeping woman's eyes and pooling on the counter.

        The pale chunks go into the bowl of the mortar. You bring the iron pestle down and they
        shatter on the second strike, breaking into a coarse, glittering powder with a smell of
        old copper that makes Hald turn his face away. You keep grinding until it is even.

        The dark stone has not moved. It goes on weeping onto the wood.
        """,
        // Scored on the outcome rather than the delta kind. The model answers with either
        // item_status_changed ("ground to powder") or item_renamed ("coarse glittering
        // powder"), and both are defensible — the chunks genuinely became something else.
        // Demanding one was the two-stage-entry scoring bug for the third time: a rule must
        // target the world the turn produces, not the route taken to it.
        Required:
        [
            new("the pale chunks are the thing changed",
                d => d is ItemStatusChanged { ItemId: "pale-chunks" }
                     or ItemRenamed { ItemId: "pale-chunks" }),
        ],
        Forbidden:
        [
            new("the capstone recorded as ground",
                d => d is ItemStatusChanged { ItemId: "weeping-woman-capstone" }),
            new("the wrong object recorded as ground in a fact",
                d => d is FactEstablished f
                     && (f.Text.Contains("ground", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("powder", StringComparison.OrdinalIgnoreCase))
                     && (f.Text.Contains("weeping woman", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("capstone", StringComparison.OrdinalIgnoreCase))),
        ],
        Expected:
        [
            new("the capstone is untouched",
                w => w.FindItem("weeping-woman-capstone")?.Status == "intact"),
        ],
        // The plan is already canon and names the capstone, which is the condition the real
        // failure needed. Without it the model records nothing at all and the scenario proves
        // only that it does not guess.
        Seed: WorldSeeds.Marrow_WithGrindingPlan);

    /// <summary>
    /// <b>Diagnostic — written to fail, before items are built.</b>
    ///
    /// The load-bearing question of the item design is *what counts as an item*. Prose is full
    /// of objects: this taproom has mugs, barrels, a rag, a rack of eel-spears, a hearth iron.
    /// If every noun becomes an entity, canon drowns and the context block with it. The
    /// proposed line is <b>handled, not described</b> — the spears on the wall are scenery, the
    /// thing somebody unwraps and puts in your hand is an item.
    ///
    /// This measures whether that line is even findable. One object is handled and given away;
    /// everything else is furniture, described in the same register and at the same length.
    ///
    /// <b>Scored against today's delta set, where no item type exists.</b> The only honest
    /// question right now is whether the model distinguishes the two categories *at all* — so
    /// the forbidden rules target the scenery becoming entities or facts, which is what it
    /// would have to do to get this wrong. If scenery reliably stays out of canon while the
    /// knife reliably reaches it, the line is real and `item_introduced` can be built on it.
    /// If not, the design needs a different shape before any code.
    /// </summary>
    private static EvalScenario SceneryVsObject => new(
        "scenery-vs-object",
        "*I sit down across from Mabb.* You said you had something for me.",
        """
        The taproom is all clutter and long use. Four barrels stand on a trestle along the far
        wall, and above them a rack of eel-spears, tines pitted with rust. A hearth iron leans
        where somebody left it. The tables are scarred, the rushes on the floor gone grey, and
        every mug on every table is the same cheap fired clay.

        Mabb pushes his own mug aside with the back of his hand. He looks at the door, then at
        the shuttered window, and then he reaches into his coat and brings out something small
        wrapped in oilcloth. He puts it on the table and slides it across to you.

        "Don't open it here," he says.
        """,
        Required: [],
        Forbidden:
        [
            new("scenery introduced as a character",
                d => d is CharacterIntroduced),
            new("scenery introduced as a location",
                d => d is LocationIntroduced),
            new("the room's furniture recorded as facts",
                d => d is FactEstablished f
                     && (f.Text.Contains("barrel", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("eel-spear", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("hearth iron", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("mug", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("rushes", StringComparison.OrdinalIgnoreCase))),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces the worst item failure found in play.</b>
    ///
    /// Two physically distinct objects, described differently, with different origins. In the
    /// 51-turn session this shape produced <b>false canon</b>: a dark, black-weeping capstone
    /// hauled from a pool and a bundle of pale, salt-crusted chunks handed over by the witch
    /// were merged, and canon recorded "the weeping woman capstone was ground into powder"
    /// when the thing ground came out of the oilcloth. The player noticed as confusion long
    /// before the save was audited.
    ///
    /// That is the strongest argument for the item type: with no entity to hang identity on,
    /// two objects with different appearances and different fates became one, and the false
    /// fact then fed back to the narrator on every later turn.
    ///
    /// Scored on facts, since that is all today's schema can express: the failure is a single
    /// fact conflating both, or a fact attributing one object's fate to the other.
    /// </summary>
    private static EvalScenario TwoObjects => new(
        "two-objects",
        "*I set the wrapped stone from the pool down on the counter, and unwrap the bundle Morwenna gave me beside it.*",
        """
        The two of them sit side by side on the scarred wood. The stone from the pool is dark
        and slick, water still beading and running from it in slow black threads, and the
        weeping woman carved into its face has hollows where her eyes should be.

        Beside it, what came out of Morwenna's oilcloth is nothing like it: three pale chunks,
        dry and crusted with something white, giving off a thin metallic smell like old copper.
        They look like they came out of a different world entirely.

        Hald has backed against the shelves and is looking at both of them.
        """,
        Required: [],
        Forbidden:
        [
            new("the two objects conflated in one fact",
                d => d is FactEstablished f
                     && f.Text.Contains("pale", StringComparison.OrdinalIgnoreCase)
                     && (f.Text.Contains("weeping woman", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("capstone", StringComparison.OrdinalIgnoreCase))),
            new("either object introduced as a character",
                d => d is CharacterIntroduced),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces a real failure.</b> An object is produced and described in
    /// detail, and the domain model has no <c>Item</c>.
    ///
    /// Turn 17 of the 51-turn save is this shape: Nessa reveals what she took from the bucket,
    /// and extraction emitted <c>medallion-description</c> — a paragraph about a silver
    /// medallion, filed as a durable world truth because there was nowhere else for it to go.
    ///
    /// <b>This scenario expects to fail, and that is the point.</b> The earlier
    /// <c>description-not-fact</c> scored a clean 0.00 by describing a *room*, which has an
    /// entity to hold its description; 8 of the 11 description-facts in the save describe
    /// something with no entity at all. A scenario that cannot reproduce the failure cannot
    /// measure a fix for it.
    /// </summary>
    private static EvalScenario ObjectDescribed => new(
        "object-described",
        "*I hold out my hand.* Let me see it.",
        """
        Mabb looks at your palm for a long moment. Then he reaches inside his coat and brings
        out something wrapped in oilcloth, and unwinds it on the table between you.

        It is a knife, but not a working one. The blade is short and leaf-shaped, black with
        age, and the hilt is bone carved into a column of small overlapping faces, each one
        with its eyes shut. It has been broken and mended at the tang with a collar of grey
        metal that does not match. When you pick it up it is colder than the room.

        "Found it in the reeds," Mabb says, in the voice of a man who did not find it in the
        reeds.
        """,
        Required: [],
        Forbidden:
        [
            new("the object described as a fact",
                d => d is FactEstablished f
                     && (f.FactId.Contains("knife", StringComparison.OrdinalIgnoreCase)
                         || f.FactId.Contains("descri", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("blade", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("hilt", StringComparison.OrdinalIgnoreCase))),
            new("the object introduced as a character",
                d => d is CharacterIntroduced),
            new("the object introduced as a location",
                d => d is LocationIntroduced),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces a real failure.</b> Blows land on a character who is already
    /// in canon.
    ///
    /// Turns 40 and 41 produced <c>drowned-follower-wounded-again</c> and
    /// <c>...-again-2</c> — individual sword strikes as permanent world truths, on a creature
    /// that died two turns later. The <c>-2</c> suffix is the model resolving its own id
    /// collision, which is a tell that it knew it was writing the same kind of thing twice.
    ///
    /// The correct answer is <c>status_changed</c>, which fired correctly in play. The
    /// question is whether the blow-by-blow facts come with it.
    /// </summary>
    private static EvalScenario BlowLanded => new(
        "blow-landed",
        "*I put my shoulder into the counter, driving it into Hald's ribs, then swing the flat of my blade at his head.*",
        """
        The counter goes into Hald's midsection with a sound like a dropped sack and he folds
        over it, breath leaving him in a whoop. Bottles walk off the shelf behind him and
        break. Your blade comes round flat and catches him above the ear — not the edge, but
        enough. He goes down on one knee in the spilled ale, one hand clamped to the side of
        his head, blood coming through his fingers in a thin dark line.

        Across the room Mabb has flattened himself against the wall and is not moving at all.
        """,
        Required:
        [
            new("Hald's condition changes",
                d => d is StatusChanged { CharacterId: Hald }),
        ],
        Forbidden:
        [
            new("a blow recorded as a fact",
                d => d is FactEstablished f
                     && (f.Text.Contains("struck", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("hit ", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("blade", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("counter", StringComparison.OrdinalIgnoreCase)
                         || f.FactId.Contains("wound", StringComparison.OrdinalIgnoreCase)
                         || f.FactId.Contains("struck", StringComparison.OrdinalIgnoreCase)
                         || f.FactId.Contains("attack", StringComparison.OrdinalIgnoreCase))),
        ]);

    /// <summary>
    /// <b>Diagnostic — reproduces a real failure.</b> A space inside a known location is
    /// described, and it is not itself a location.
    ///
    /// The save has three of these: <c>well-base-description</c>, <c>tunnel-fork-location</c>,
    /// <c>cistern-tunnel-smell</c>. Each describes somewhere the player can perceive but not
    /// *be* — the bottom of a well seen from the top, a fork further down a passage.
    ///
    /// The open design question underneath is the one already logged for buildings: when does
    /// a described space become a <c>Location</c>? This measures what the model does while
    /// that is unanswered.
    /// </summary>
    private static EvalScenario SubSpaceDescribed => new(
        "sub-space-described",
        "*I lean over the well and look down.*",
        """
        The shaft goes down further than the light does. Perhaps twenty feet of wet brick,
        greened where the damp runs, and then a floor of packed earth and slick flagstones
        with a shine of standing water on them. Set into the wall down there is an iron grate,
        half submerged, and beside it the flagstones are gouged — four long parallel scores in
        the stone, pale against the wet.

        Something is dripping down there with a slow regularity that is not the well.
        """,
        Required: [],
        Forbidden:
        [
            new("the shaft's contents described as a fact",
                d => d is FactEstablished f
                     && (f.Text.Contains("flagstone", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains("brick", StringComparison.OrdinalIgnoreCase)
                         || f.FactId.Contains("descri", StringComparison.OrdinalIgnoreCase)
                         || f.FactId.Contains("base", StringComparison.OrdinalIgnoreCase))),
        ]);

    /// <summary>
    /// <b>Diagnostic.</b> <see cref="DescriptionNotFact"/> against a world the size play
    /// reaches. Only the seed differs.
    ///
    /// Written because the small-world versions scored a clean forbidden 0.00 at baseline
    /// while the 51-turn save contains 11 description-facts and 12 event-facts. Something
    /// separates the scenario from play, and world size is the variable that has already
    /// explained one such gap — <c>two-stage-entry</c> went 14/14 small and 2/14 large.
    /// </summary>
    private static EvalScenario DescriptionNotFactLarge =>
        DescriptionNotFact with { Name = "description-not-fact-large", Seed = WorldSeeds.Marrow_Late };

    private static EvalScenario EventNotFactLarge =>
        EventNotFact with { Name = "event-not-fact-large", Seed = WorldSeeds.Marrow_Late };

    /// <summary>
    /// <b>Diagnostic.</b> Rich description of a place the player already knows.
    ///
    /// The largest fixable category in the fact audit: 11 of 53 facts in the 51-turn save were
    /// descriptions filed as world truths — the altar's appearance, the mill's floor, the smell
    /// of a tunnel. Each one passes the prompt's own durability test ("would it still be true
    /// if nobody mentioned it?") and each one belongs on the entity.
    ///
    /// Nothing here is new. No character arrives, nobody speaks, nothing is revealed. A turn
    /// that looks closely at a known room should change the room's description or change
    /// nothing at all.
    /// </summary>
    private static EvalScenario DescriptionNotFact => new(
        "description-not-fact",
        "*I take a proper look around the taproom for the first time.*",
        """
        The Drowned Crow is longer than it looked from the door, and lower — the beams are
        black with a century of peat smoke and hang close enough that a tall man would learn
        to stoop. The floor is packed earth strewn with rushes gone grey. Along the far wall,
        three barrels stand on a trestle, and above them somebody has nailed up a rack of
        eel-spears, tines pitted with rust.

        There is one window, small and set high, and the marsh light coming through it is the
        colour of weak tea. The fire is peat and gives more smoke than heat.
        """,
        Required: [],
        Forbidden:
        [
            new("a description filed as a fact", d => d is FactEstablished),
            new("the tavern re-introduced", d => d is LocationIntroduced),
            new("a character introduced", d => d is CharacterIntroduced),
        ]);

    /// <summary>
    /// <b>Diagnostic.</b> A completed physical action with no lasting consequence.
    ///
    /// The largest category outright: 12 of 53. Two of them recorded individual sword strikes
    /// on a creature that died two turns later, and the model resolved its own id collision
    /// with a <c>-2</c> suffix. The real state change was already carried by
    /// <c>status_changed</c>, which fired correctly.
    ///
    /// An event is already recorded — history holds every turn — so the question is whether
    /// canon needs it too. Under the knowledge-worthiness test it does not: nobody needs to
    /// know a blow landed.
    /// </summary>
    private static EvalScenario EventNotFact => new(
        "event-not-fact",
        "*I sweep the empty mugs off the table and stack them by the counter, then sit back down.*",
        """
        You gather the mugs — four of them, one still with a finger of ale gone flat — and
        carry them to the end of the counter, where you set them down in an uneven stack. One
        tips against another with a dull knock and settles.

        Hald watches you do it without comment. When you sit back down the bench takes your
        weight with a complaint of old wood, and the room returns to the sound of the fire.
        """,
        Required: [],
        Forbidden:
        [
            new("a completed action filed as a fact", d => d is FactEstablished),
            new("anything introduced", d => d is CharacterIntroduced or LocationIntroduced),
        ]);

    /// <summary>
    /// <b>Diagnostic.</b> A revelation, where the knowledge relationship is the interesting
    /// part.
    ///
    /// Four audited facts assert who knows what — <c>hald-knows-player-has-medallion</c> and
    /// friends — which is precisely what <see cref="Character.Knows"/> models, and in at least
    /// one case the correct <c>fact_learned</c> had already fired on the same turn. The fact
    /// was a duplicate of a delta that worked.
    ///
    /// The information itself *is* a fact and must still be established; what must not appear
    /// is a second fact about somebody knowing it.
    /// </summary>
    private static EvalScenario KnowledgeNotFact => new(
        "knowledge-not-fact",
        "The well. Why is it boarded?",
        """
        Mabb's mug stops halfway to his mouth. He puts it down again without drinking.

        "They pulled a body out of it," he says, to the table rather than to you. "Spring
        before last. Weighted at the ankles, so it wasn't a fall." He glances at the counter,
        where Hald has gone very still. "Nobody's drawn from it since."
        """,
        Required:
        [
            new("the body is established as a fact",
                d => d is FactEstablished f
                     && (f.Text.Contains("body", StringComparison.OrdinalIgnoreCase)
                         || f.FactId.Contains("body", StringComparison.OrdinalIgnoreCase))),
        ],
        Forbidden:
        [
            new("a fact about who knows something",
                d => d is FactEstablished f
                     && (f.FactId.Contains("knows", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains(" knows ", StringComparison.OrdinalIgnoreCase)
                         || f.Text.Contains(" heard ", StringComparison.OrdinalIgnoreCase))),
        ]);

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
    /// <b>Diagnostic — the shape play actually produces.</b>
    ///
    /// <see cref="LoreLearned"/> scores 14/14 by having a character name the topic and teach
    /// it: "there's an old faith out in the fen — the Blind, folk call them." A 51-turn session
    /// in which that cult was the entire plot produced **zero** characters who had learned it,
    /// because nobody ever named it. They spoke of the Drowned Father, the weeping woman, the
    /// capstone and a century of drowned men — every recognisable feature of the topic, and
    /// never its label.
    ///
    /// This scenario is that: the entry's subject matter discussed in detail, and the words
    /// "cult" and "the Blind" appearing nowhere. If extraction can only recognise a topic when
    /// handed its title, the passing score on the explicit case is measuring a behaviour real
    /// play does not exhibit.
    ///
    /// Note the extractor is shown lore as <c>(cult-of-the-blind) The Cult of the Blind</c> and
    /// nothing else — no keys, no body — so on the evidence available to it, "the Drowned
    /// Father" and that entry are unrelated strings.
    /// </summary>
    private static EvalScenario LoreLearnedImplicit => new(
        "lore-learned-implicit",
        "That thing they pulled out of the well. What was it, really?",
        """
        Hald is quiet for long enough that you think he will not answer. When he does, he does
        not look up from the counter.

        "A capstone. Carved with a weeping woman, eyes gouged out." His rag has stopped moving.
        "My grandfather's grandfather put it down there, and every man in this village has known
        since he was old enough to be told, and not one of us says it out loud." He finally
        raises his head. "The water under this square is owed. A hundred years we paid the
        Drowned Father his tithe — men who went into the fen and didn't come back, and we called
        it drowning and we knew what it was. That stone was the lid."

        In the corner Mabb has stopped drinking entirely, both hands flat on the table.
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
        ],
        Forbidden:
        [
            new("Hald re-introduced", d => d is CharacterIntroduced { CharacterId: Hald }),
        ],
        // "Hald is recorded as knowing it" moved from a delta rule to an outcome rule when
        // attribution arrived. He can now end the turn knowing it two ways — an explicit
        // fact_learned, or being named as the fact's source, which the applier derives from —
        // and demanding the delta failed a turn that reached the right world by the better
        // route. The third repetition of the same scoring mistake this week.
        Expected:
        [
            new("Hald knows what he just said",
                w => w.FindCharacter(Hald)?.Knows.Count > 1),
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
