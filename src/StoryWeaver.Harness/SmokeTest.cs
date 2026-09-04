using System.Text.Json;
using StoryWeaver.Llm;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;

namespace StoryWeaver.Harness;

/// <summary>
/// The live smoke test — two real API calls behind <c>--smoke</c>.
///
/// A diagnostic, not a client feature: it moved out of the CLI with the rest of the
/// instrumentation when the CLI became a thin console UI. Like the self-tests it prints its own
/// result rather than returning data, because nothing in a game UI renders a smoke test.
/// </summary>
public static class SmokeTest
{
    /// <summary>
    /// Two calls. Narration proves the basic path; extraction proves schema-constrained
    /// output actually works on the configured model, which is the open question the
    /// bootstrap phase exists to answer.
    /// </summary>
    public static async Task<int> RunAsync(StoryWeaverSettings settings)
    {
        FileLlmLog log = new(settings.Logging);
        Console.WriteLine($"Logging to {log.FilePath}");
        Console.WriteLine();

        using OpenRouterClient client = new(settings, log);

        Console.WriteLine("[1/2] Narration ...");
        LlmResult narration = await client.CompleteAsync(new LlmCall
        {
            Role = LlmRole.Narration,
            Messages =
            [
                LlmMessage.System("You are narrating a dark fantasy RPG. Two sentences, no preamble."),
                LlmMessage.User("The player pushes open the door of the tavern in Marrow."),
            ],
        }).ConfigureAwait(false);

        Report(narration);

        if (!narration.IsSuccess)
        {
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("[2/2] Extraction (schema-constrained) ...");

        const string schema = """
        {
          "type": "object",
          "properties": {
            "location":  { "type": "string", "description": "Where the scene takes place." },
            "characters": {
              "type": "array",
              "description": "Named characters present.",
              "items": { "type": "string" }
            },
            "mood": { "type": "string", "description": "One word for the scene's tone." }
          },
          "required": ["location", "characters", "mood"],
          "additionalProperties": false
        }
        """;

        LlmResult extraction = await client.CompleteAsync(new LlmCall
        {
            Role = LlmRole.Extraction,
            Schema = new JsonSchemaSpec("scene_state", schema),
            Validator = IsJsonObject,
            Messages =
            [
                LlmMessage.System("Extract structured state from the narration. Return JSON only."),
                LlmMessage.User(narration.Content),
            ],
        }).ConfigureAwait(false);

        Report(extraction);

        if (extraction.IsSuccess)
        {
            Console.WriteLine();
            Console.WriteLine("  Schema-constrained output works on this model.");
        }

        return extraction.IsSuccess ? 0 : 1;
    }

    private static void Report(LlmResult result)
    {
        if (!result.IsSuccess)
        {
            Console.WriteLine($"  FAILED after {result.Attempts} attempt(s): {result.Error}");
            return;
        }

        Console.WriteLine($"  {result.Content.Trim()}");
        Console.WriteLine();
        Console.WriteLine(
            $"  served by {result.Model ?? "(unreported)"}, " +
            $"{result.Usage?.TotalTokens ?? 0} tokens, {result.Attempts} attempt(s)");
    }

    /// <summary>Cheap structural check — is this parseable JSON with an object at the root?</summary>
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
}
