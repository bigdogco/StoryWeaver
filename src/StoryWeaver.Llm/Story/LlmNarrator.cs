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

        Rules:
        - Write in second person, present tense, addressing the player as "you".
        - Two to four paragraphs. Prose only: no headings, no lists, no meta commentary.
        - Never decide what the player says, thinks, or does beyond what they stated.
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
        string playerInput,
        CancellationToken cancellationToken = default)
    {
        LlmResult result = await _client.CompleteAsync(
            new LlmCall
            {
                Role = LlmRole.Narration,
                Messages =
                [
                    LlmMessage.System(SystemPrompt),
                    LlmMessage.User($"World state:\n\n{context}\n\nThe player: {playerInput}"),
                ],
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
}

/// <summary>Thrown when a step of the turn loop cannot produce anything usable.</summary>
public sealed class StoryWeaverException : Exception
{
    public StoryWeaverException(string message) : base(message)
    {
    }
}
