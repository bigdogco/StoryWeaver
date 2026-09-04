using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Llm.Story;

/// <summary>Narration half of the turn loop. Prose only — it is told nothing about deltas,
/// ids, or schemas, because asking one model to tell a story and keep books at the same time
/// degrades both.</summary>
public sealed class LlmNarrator : INarrator
{
    private readonly ILlmClient _client;
    private readonly string _systemPrompt;

    /// <param name="voice">
    /// A pack's own narration prompt, appended to the engine's. <b>Appended, never
    /// substituted.</b> The engine's prompt mixes taste with correctness — genre and paragraph
    /// count beside "never speak for the player" and "never write an internal id" — and those
    /// rules exist because they broke first. An author writing a voice must not be able to drop
    /// one by omission, which is what replacement would allow while looking like content.
    /// </param>
    public LlmNarrator(ILlmClient client, PromptLibrary prompts, string voice = "")
    {
        _client = client;

        _systemPrompt = string.IsNullOrWhiteSpace(voice)
            ? prompts.Narration
            : $"""
              {prompts.Narration}

              ## This world's voice

              {voice.Trim()}
              """;
    }

    public async Task<string> NarrateAsync(
        string context,
        IReadOnlyList<StoryBeat> recent,
        string playerInput,
        string scenario = "",
        CancellationToken cancellationToken = default)
    {
        LlmResult result = await _client.CompleteAsync(
            new LlmCall
            {
                Role = LlmRole.Narration,
                Messages = BuildMessages(_systemPrompt, context, recent, playerInput, scenario),
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Narration failing is not recoverable into something sensible to show a player, so
        // unlike extraction this does throw. The turn loop deliberately does not catch it.
        if (!result.IsSuccess)
        {
            throw new StoryWeaverException($"Narration failed: {result.Error}");
        }

        return result.Content.Trim();
    }

    /// <summary>
    /// System prompt, then the recent story as real alternating user/assistant turns, then
    /// the current turn.
    ///
    /// Replaying history as actual messages rather than pasting a transcript into one blob
    /// buys two things. It is the shape a chat model was trained on for multi-turn dialogue;
    /// and it keeps the volatile part — world state, which changes every turn — in the
    /// <i>last</i> message, so the system prompt plus the whole history stays a stable,
    /// cacheable prefix. A transcript blob would invalidate that prefix on every single turn.
    ///
    /// The replayed user messages carry the player's raw input only, deliberately without the
    /// world-state block they originally shipped with: stale state in the history would sit
    /// there competing with the current state below it.
    /// </summary>
    private static IReadOnlyList<LlmMessage> BuildMessages(
        string systemPrompt,
        string context,
        IReadOnlyList<StoryBeat> recent,
        string playerInput,
        string scenario)
    {
        // The scenario joins the system prompt rather than the world-state block below.
        // Both are prompt text, but they differ in volatility: state changes every turn and
        // has to sit last so everything above it stays a stable cacheable prefix, while a
        // scenario is identical for the life of the save. Putting it below would invalidate
        // the prefix every turn to resend the same paragraph.
        string system = string.IsNullOrWhiteSpace(scenario)
            ? systemPrompt
            : $"""
              {systemPrompt}

              ## What this story is about

              {scenario.Trim()}
              """;

        List<LlmMessage> messages = new(2 + (recent.Count * 2)) { LlmMessage.System(system) };

        foreach (StoryBeat beat in recent)
        {
            // The opening message is prose nobody prompted — it arrives as a beat with no
            // player input, and inventing an empty user turn for it would put a blank message
            // in the conversation the model is reading.
            if (beat.PlayerInput.Length > 0)
            {
                messages.Add(LlmMessage.User(beat.PlayerInput));
            }

            messages.Add(LlmMessage.Assistant(beat.Narration));
        }

        messages.Add(LlmMessage.User($"World state:\n\n{context}\n\nThe player: {playerInput}"));
        return messages;
    }
}

/// <summary>Thrown when a step of the turn loop cannot produce anything usable.</summary>
public sealed class StoryWeaverException : Exception
{
    public StoryWeaverException(string message) : base(message)
    {
    }
}
