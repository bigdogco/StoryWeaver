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
        - Do not restate what is already true. If the state says a mood is "wary", do not
          emit mood_changed to "wary" again. Report changes, not the current situation.
        - Emit mood_changed whenever the prose shows a shift in how a character feels, even
          a brief one. These are easy to miss and matter.
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
            envelope?.Deltas ?? [],
            result.Content,
            result.Usage is null
                ? null
                : new ExtractionUsage(
                    result.Usage.PromptTokens,
                    result.Usage.CompletionTokens,
                    result.Usage.ReasoningTokens),
            result.Provider);
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
