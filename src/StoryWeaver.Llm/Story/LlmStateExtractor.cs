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

        You are a bookkeeper, not a storyteller. Report only what the prose actually
        supports. Do not infer, embellish, or continue the scene.

        Critical rules:
        - Use the exact ids from "Known ids" for anything that already exists. Only invent a
          new slug id for something genuinely new.
        - Never introduce a character or location that is already in the known ids. If the
          prose merely mentions a known place, that is not an introduction.
        - A description field describes what something IS, permanently. Never put an event
          in a description.
        - Establishing a fact and someone knowing it are separate. If a character reveals
          new information, emit fact_established, then fact_learned for everyone who now
          knows it — INCLUDING THE SPEAKER, unless the known ids already record them as
          knowing it. Canon only contains what you write down: a character who states a
          secret but gets no fact_learned is recorded as not knowing their own secret, and
          will contradict themselves later.
        - Do not restate what is already true. If the state says a mood is "wary", do not
          emit mood_changed to "wary" again. Report changes, not the current situation.
        - Emit mood_changed whenever the prose shows a shift in how a character feels, even
          a brief one. These are easy to miss and matter.
        - If nothing changed, return an empty deltas list. That is a valid answer.
        """;

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILlmClient _client;

    public LlmStateExtractor(ILlmClient client) => _client = client;

    public async Task<ExtractionResult> ExtractAsync(
        string context,
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
                    LlmMessage.User($"World state:\n\n{context}\n\nNarration:\n\n{narration}"),
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
            envelope = JsonSerializer.Deserialize<DeltaEnvelope>(result.Content, ParseOptions);
        }
        catch (JsonException ex)
        {
            // Schema-conformant JSON that will not map onto the Core types means the schema
            // and the types have drifted apart. Worth naming precisely, because it looks
            // identical to a model failure from the outside and is not one.
            throw new StoryWeaverException(
                $"Extraction returned JSON that does not match StateDelta: {ex.Message}");
        }

        return new ExtractionResult(envelope?.Deltas ?? [], result.Content);
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
