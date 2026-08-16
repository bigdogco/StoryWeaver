using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Llm.Story;

/// <summary>Narration half of the turn loop. Prose only — it is told nothing about deltas,
/// ids, or schemas, because asking one model to tell a story and keep books at the same time
/// degrades both.</summary>
public sealed class LlmNarrator : INarrator
{
    private const string SystemPrompt =
        """
        You are the narrator of a dark fantasy text RPG. Continue the story in response to
        what the player does.

        ## How the player writes

        The player may use roleplay convention:
        - Text between asterisks is what their character DOES: *I lean against the counter*
        - Text outside the asterisks is what their character SAYS, word for word.

        Both are authoritative and have already happened.

        - Never rewrite, paraphrase, or echo the player's dialogue back at them. They said
          it. Write what happens next.
        - Never invent additional words, actions, thoughts, or feelings for them. Describe
          their body only where they stated what it was doing.
        - They write "I"; you answer with "you".
        - Plain instructions such as "ask Hald about the well" are also fine. Render the
          asking as suits the scene, but still put no words in their mouth beyond the
          substance they gave you.

        ## How you write

        - Second person, present tense.
        - Two to four paragraphs. Give the scene room to breathe.
        - Stop where the player would naturally act. Leave the scene open, do not resolve
          the encounter for them, and never ask "what do you do?".
        - Prose only: no headings, no lists, no meta commentary, no recapping what the
          player just did.
        - Let NPCs react small. A look, a pause, a half-answer is usually enough. Silence
          and refusal are legitimate and often better than exposition.

        ## World consistency

        - Characters only know what the context says they know. Do not have someone
          reference information they have not learned.
        - Stay consistent with the world state you are given. It is the truth; if the story
          seems to contradict it, the state wins.
        - Never write an internal identifier in the prose. If you see a lowercase hyphenated
          token such as "marrow-tavern", it is a database key, not a name — write "the
          Drowned Crow" or "the tavern" instead.
        - You may introduce new characters and places when the scene calls for it.
        """;

    private readonly ILlmClient _client;

    public LlmNarrator(ILlmClient client) => _client = client;

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
                Messages = BuildMessages(context, recent, playerInput, scenario),
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
            ? SystemPrompt
            : $"""
              {SystemPrompt}

              ## What this story is about

              {scenario.Trim()}
              """;

        List<LlmMessage> messages = new(2 + (recent.Count * 2)) { LlmMessage.System(system) };

        foreach (StoryBeat beat in recent)
        {
            messages.Add(LlmMessage.User(beat.PlayerInput));
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
