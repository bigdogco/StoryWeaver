using System.Text.Json;
using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Llm.Story;

/// <summary>
/// Extraction half of the turn loop: prose in, proposed deltas out.
///
/// The system prompt leans hard on the failures the schema probe exposed. Every "do not"
/// here corresponds to something the model actually did on its first exposure to this
/// schema, not to a hypothetical. It is cheaper to prevent those in the prompt than to
/// reject them in the validator, since a rejected delta is a change that silently did not
/// happen.
/// </summary>
public sealed class LlmStateExtractor : IStateExtractor
{
    private const string SystemPrompt =
        """
        You read narration from a text RPG and report what changed in the world, as
        structured deltas.

        You are a bookkeeper, not a storyteller. Report only what actually happened. Do not
        infer, embellish, or continue the scene.

        You are given both what the player did and the narration that followed. The player's
        input is authoritative: text between asterisks is an action they took, text outside
        is speech. If the player did something the narration does not restate — handing over
        an object, moving somewhere, revealing information — it still happened and must be
        reported.

        ## What counts as a fact

        A fact is a durable truth about the world. The test: **would it still be true if
        nobody had ever mentioned it?**

        "The well was sealed after a body was found in it" is a fact. "The player asked
        about the well" is not — it is a conversation, and conversations are not world
        truths.

        NEVER establish a fact for:
        - a question being asked, or a request being made
        - someone refusing, deflecting, or declining to answer
        - a greeting, a purchase, a gesture, or anyone's mood
        - the fact that a conversation happened at all

        Only establish a fact when genuinely new information about the world is revealed. If
        a character deflects a question, no information was revealed — emit nothing.

        Most turns should establish no facts. That is normal and correct.

        Critical rules:
        - Use the exact ids from "Known ids" for anything that already exists. Only invent a
          new slug id for something genuinely new.
        - Never introduce a character or location that is already in the known ids. If the
          prose merely mentions a known place, that is not an introduction.
        - A move must name the place the prose actually describes. When that place is new,
          introduce it and move to the id you just gave it — never redirect the move to a
          different place that happens to be already known.
        - Movement records where someone ENDS the turn. If the prose carries them through
          more than one space — down a shaft, along a passage, into the chamber beyond —
          report where they finish. Reporting only the first step leaves them standing in a
          place the story has already left.
        - But a journey has to actually happen. Proposing somewhere, naming it, standing up,
          turning toward the door, or setting off is NOT movement — those people are still in
          the room the turn ends in. Ask where the prose leaves them standing, not where they
          are headed. Recording an arrival early is worse than recording it late: the next
          turn, when they really do arrive, has nothing left to report and the journey
          vanishes. Introducing the place they named is fine; moving anyone into it is not.
        - When the story reveals the name of someone already in the known ids — an
          anonymous figure who gives their name, a stranger someone greets — emit
          character_renamed with their EXISTING id. Do not introduce them again as a new
          character, and do not record the name as a fact. A name is not a world truth, it
          is who somebody is: "the shivering figure is called Nessa" belongs in her name
          field, not in the fact store. Their id stays exactly as it was, however wrong it
          now looks.
        - Objects are items, and most objects in a scene are not. A room is full of furniture,
          fittings and background things — barrels, mugs, a rack of tools, the rushes on the
          floor — and none of those belong in canon. An object becomes an item only when it is
          HANDLED: taken out, handed over, picked up, put down, used, broken. If nobody
          touches it, it is scenery and you record nothing.
        - Every item is either in a location or held by a character, never both and never
          neither. An item that is nowhere has silently stopped existing. The one exception is
          a thing genuinely destroyed or beyond recovery — burned to nothing, swallowed by deep
          water, thrown into a fissure: report that as item_moved with no destination at all,
          neither location nor holder. Only for what is truly gone; something dropped or left
          behind is still in the world and goes to the room it is in.
        - Keep separate objects separate. Two things described differently are two items, even
          when they are similar and in the same scene, and an action on one says nothing about
          the other. Recording that the wrong thing was ground, burned or given away is a
          mistake nothing downstream can detect.
        - Identical is not the same. When the prose picks up something that matches an item
          already in the known ids — a twin, a matching pair, another of the same make — emit
          item_introduced with a NEW id naming where this one came from: shrine-medallion,
          not weeping-woman-medallion. Two coins from the same mint are two coins.
        - An item's status is its condition — intact, broken, burned, wet, ground to powder.
          Its description is what it IS. A carving found under the rust, a maker's mark, an
          inscription: those were always there and are what the thing is, so revise the
          DESCRIPTION with item_renamed, keeping the same name. Never write a discovered
          property into status. A ring whose status reads "carved with a weeping woman" is
          recorded as having been damaged into that shape, and its real description still says
          nothing about the carving.
        - A status is a condition, never a whereabouts. Broken, lit, soaked, ground to powder
          are statuses. On the floor, down the shaft, tied to the gate, back in its case are
          placements, and a placement is item_moved. If an object ends the turn resting on,
          inside, or fastened to something in a room, it is in that room: move it there. An
          item whose status says it is on the ground while canon still has it in somebody's
          hand is canon contradicting the story, and nothing downstream can notice.
        - Looking closely at something and finding out what it is IS a change worth recording.
          Do not stay silent because the object did not move and nothing happened to it — what
          the world knows about it changed.
        - A description field describes what something IS, permanently. Never put an event
          in a description.
        - Establishing a fact and someone knowing it are separate. When new information is
          revealed, emit fact_established, then fact_learned for everyone who now knows it —
          INCLUDING THE SPEAKER, unless the known ids already record them as knowing it.
          Canon only contains what you write down: a character who states a secret but gets
          no fact_learned is recorded as not knowing their own secret, and will contradict
          themselves later. This applies to EVERY fact you establish, not only the first —
          if one speech reveals three things, emit three fact_established and give each of
          them its own fact_learned for the speaker and for everyone else who now knows it.
        - fact_learned is only for real information. A character who was merely asked a
          question has learned nothing.
        - When a character asserts something, set sourceId to that character and write the
          claim plainly — "the stone went to the quarry", not "Hald claims the stone went to
          the quarry". The source field is what records who said it, and putting it in the
          text as well says it twice. Leave sourceId null only when the narration states
          something as plain truth rather than somebody saying it.
        - Two characters may contradict each other. Record BOTH claims with their own sources.
          Do not choose between them, do not merge them, and do not drop the second one — who
          disagrees with whom is exactly what the story is made of, and canon that asserts
          both as unattributed truth is simply wrong.
        - The world lore list holds authored topics. When someone is told about a topic that
          is already listed there — the order, the cult, the war — emit fact_learned for the
          listener against THAT topic's id. Do not establish new facts restating what the
          topic already covers, and never establish the topic itself. Only add a fact for
          something specific the lore does not already say.
        - A scene is usually about a topic without ever naming it. People speak of the thing
          itself — its god, its sign, its practices, what it is owed — and almost never say
          its title out loud, because everyone present already knows what they are discussing.
          Read the words listed after each topic and ask what the speech is ABOUT. If someone
          is being told the substance of a listed topic, they have learned that topic, whether
          or not its name was spoken.
        - Do not restate what is already true. If the state says a mood is "wary", do not
          emit mood_changed to "wary" again. Report changes, not the current situation.
        - Emit mood_changed whenever the prose shows a shift in how a character feels, even
          a brief one. These are easy to miss and matter.
        - Status is the body, mood is the feeling, and they are different deltas. Wounded,
          bleeding, unconscious, bound, poisoned, drunk, dead — all status_changed. "Injured"
          is not a mood. When someone is physically harmed, restrained, or incapacitated you
          must emit status_changed; add mood_changed as well only if how they FEEL also
          changed. A character beaten senseless whose status still reads "normal" is recorded
          as unhurt, and everything downstream will treat them as unhurt.
        - Places have a status too, and it is the commonest thing to get wrong. When a place
          starts doing something — water rising, a fire taking, a noise starting or stopping,
          a structure straining — that is location_status_changed, NOT a fact. A fact is
          something that stays true and that a character could be told later. "The sound from
          the shaft became a churning" is the well's condition this turn and will be wrong the
          next; it is not knowledge anyone can carry. Write the place's condition into its
          status and establish no fact for it.
        - An object that proves to be alive is PROMOTED, not re-introduced. A covered shape,
          a bundle, a heap of rags you recorded as an item, which then breathes or moves or
          speaks, is item_revealed_as_character on the id you already have. Do not introduce a
          new character and leave the item lying there: that puts a person and a thing in the
          same room, both real, and nothing downstream can tell they were ever one. Because the
          id survives, a fact in this same batch may name it as sourceId.
        - If nothing changed, return an empty deltas list. That is a valid answer.
        """;

    private readonly ILlmClient _client;

    public LlmStateExtractor(ILlmClient client) => _client = client;

    public async Task<ExtractionResult> ExtractAsync(
        string context,
        string playerInput,
        string narration,
        CancellationToken cancellationToken = default)
    {
        LlmResult result = await _client.CompleteAsync(
            new LlmCall
            {
                Role = LlmRole.Extraction,
                Schema = new JsonSchemaSpec(DeltaSchema.Name, DeltaSchema.Json),
                Validator = IsJsonObject,
                Messages =
                [
                    LlmMessage.System(SystemPrompt),
                    LlmMessage.User(
                        $"World state:\n\n{context}\n\n" +
                        $"What the player did:\n\n{playerInput}\n\n" +
                        $"Narration:\n\n{narration}"),
                ],
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new StoryWeaverException($"Extraction failed: {result.Error}");
        }

        DeltaEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<DeltaEnvelope>(result.Content, StoryJson.Options);
        }
        catch (JsonException ex)
        {
            // Schema-conformant JSON that will not map onto the Core types means the schema
            // and the types have drifted apart. Worth naming precisely, because it looks
            // identical to a model failure from the outside and is not one.
            throw new StoryWeaverException(
                $"Extraction returned JSON that does not match StateDelta: {ex.Message}");
        }

        return new ExtractionResult(
            Normalise(envelope?.Deltas ?? []),
            result.Content,
            result.Usage is null
                ? null
                : new ExtractionUsage(
                    result.Usage.PromptTokens,
                    result.Usage.CompletionTokens,
                    result.Usage.ReasoningTokens),
            result.Provider);
    }

    /// <summary>
    /// Rewrites what the model reliably says into what the domain means.
    ///
    /// <b>One rule: an <c>item_moved</c> with no destination at all is a loss.</b> The model
    /// emits it unprompted whenever an object goes somewhere it cannot come back from — a rock
    /// into the dark, a key into a lava fissure — and the validator refuses it, correctly,
    /// because an item that is merely nowhere has silently stopped existing. The refusal then
    /// leaves canon asserting the old placement: a key recorded lying in a cellar for twenty
    /// turns after it went into the lava.
    ///
    /// <b>Why this is a rewrite and not a new delta kind.</b> An <c>item_lost</c> branch was
    /// built, measured, and removed. It worked — <c>object-lost-for-good</c> went 0/6 to 10/10
    /// — but adding the branch dropped an unrelated scenario, <c>object-leaves-the-hand</c>,
    /// from 16/20 to between 0/20 and 10/20 depending on where its prompt rule sat. **A
    /// schema branch is not free: the anyOf competes for the model's attention, and a rule
    /// added to explain it competes with the rules already there.** Rewriting an output the
    /// model already produces costs nothing, because nothing about the request changes.
    ///
    /// The evidence text becomes the reason, which is the only place the *how* survives.
    /// </summary>
    private static IReadOnlyList<StateDelta> Normalise(IReadOnlyList<StateDelta> deltas)
    {
        if (!deltas.Any(d => d is ItemMoved { ToLocationId: null, ToHolderId: null }))
        {
            return deltas;
        }

        return
        [
            .. deltas.Select(d => d is ItemMoved { ToLocationId: null, ToHolderId: null } gone
                ? new ItemLost(
                    gone.ItemId,
                    string.IsNullOrWhiteSpace(gone.Evidence) ? "gone from the world" : gone.Evidence)
                    { Evidence = gone.Evidence }
                : d),
        ];
    }

    private static bool IsJsonObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(content);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class DeltaEnvelope
    {
        public List<StateDelta>? Deltas { get; init; }
    }
}
