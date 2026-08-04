using System.Text.Json;

namespace StoryWeaver.Llm.OpenRouter;

/// <summary>
/// Offline checks on how a provider response is read.
///
/// Lives in this project rather than beside the other self-tests because the wire types are
/// internal, and they should stay that way — the shape of an OpenRouter response is not
/// something the rest of the codebase should be able to depend on.
///
/// The case that matters is the reasoning fallback. Empty content plus a reasoning field has
/// two causes that look identical and want opposite handling, and getting it wrong printed
/// 4,682 characters of a model's thinking into a live story.
/// </summary>
public static class ResponseSelfTest
{
    public static int Run()
    {
        int failures = 0;

        failures += Check(
            "ordinary response reads content",
            """
            {"choices":[{"finish_reason":"stop","message":{"content":"The door opens."}}]}
            """,
            r => r.Content == "The door opens." && !r.ContentCameFromReasoning && !r.WasTruncated);

        // The reason the fallback exists: a provider that puts the answer in the wrong field.
        failures += Check(
            "misreported payload is recovered from reasoning",
            """
            {"choices":[{"finish_reason":"stop","message":{"content":null,"reasoning":"The door opens."}}]}
            """,
            r => r.Content == "The door opens." && r.ContentCameFromReasoning);

        // The live failure, reduced. Reasoning burned the whole budget and there is no answer;
        // recovering the fragment would put a train of thought on screen as prose.
        failures += Check(
            "truncated reasoning is NOT recovered",
            """
            {"choices":[{"finish_reason":"length","message":{"content":null,"reasoning":"Thinking Process:\n1. Analyze the player's input"}}],
             "usage":{"completion_tokens":1202,"completion_tokens_details":{"reasoning_tokens":1200}}}
            """,
            r => r.Content is null && !r.ContentCameFromReasoning && r.WasTruncated);

        // A provider filling both fields is unaffected either way, truncated or not.
        failures += Check(
            "content wins over reasoning even when truncated",
            """
            {"choices":[{"finish_reason":"length","message":{"content":"The door opens","reasoning":"thinking"}}]}
            """,
            r => r.Content == "The door opens" && !r.ContentCameFromReasoning);

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "All response-parsing checks passed."
            : $"{failures} response-parsing check(s) FAILED.");

        return failures == 0 ? 0 : 1;
    }

    private static int Check(string name, string json, Func<OpenRouterResponse, bool> holds)
    {
        try
        {
            OpenRouterResponse? parsed = JsonSerializer.Deserialize<OpenRouterResponse>(json);

            if (parsed is null)
            {
                Console.WriteLine($"  FAIL  {name}: deserialized to null.");
                return 1;
            }

            if (!holds(parsed))
            {
                Console.WriteLine($"  FAIL  {name}: parsed, but the result was not as expected.");
                return 1;
            }

            Console.WriteLine($"  ok    {name}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL  {name}: {ex.Message}");
            return 1;
        }
    }
}
