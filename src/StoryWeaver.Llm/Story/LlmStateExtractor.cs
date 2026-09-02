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

    private readonly ILlmClient _client;
    private readonly string _systemPrompt;

    public LlmStateExtractor(ILlmClient client, PromptLibrary prompts)
    {
        _client = client;
        _systemPrompt = prompts.Extraction;
    }

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
                    LlmMessage.System(_systemPrompt),
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
