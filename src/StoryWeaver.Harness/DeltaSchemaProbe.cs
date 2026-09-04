using System.Text.Json;
using StoryWeaver.Core;
using StoryWeaver.Llm;
using StoryWeaver.Llm.Configuration;
using StoryWeaver.Llm.Logging;
using StoryWeaver.Llm.OpenRouter;
using StoryWeaver.Llm.Story;

namespace StoryWeaver.Harness;

/// <summary>
/// Answers one question before §7 is built on top of it: can the extraction model emit a
/// nine-branch discriminated union under <c>strict: true</c>?
///
/// The smoke test is weaker evidence than it looks — it proved <c>json_schema</c> works for
/// a three-field flat object. <c>anyOf</c> is a materially harder ask and less universally
/// implemented. If this fails, the fallback is a flat object with a <c>kind</c> enum plus
/// nullable fields, validated in code after parsing.
///
/// Worth keeping after it passes: the routing hazard means this needs re-running whenever
/// the extraction model changes.
/// </summary>
public static class DeltaSchemaProbe
{
    /// <summary>
    /// Prose written to exercise several delta kinds at once — a move, a mood shift, a new
    /// character, a fact entering canon, and someone learning it. A single-delta scene
    /// would not distinguish "anyOf works" from "the model picked the first branch".
    /// </summary>
    private const string TestNarration = """
        Hald the innkeeper stops wiping the counter as you enter, his easy grin curdling
        into something guarded. He leans close and says, low enough that the hearth swallows
        it: the ale casks in the cellar were poisoned three nights ago, and the militia
        already know. Behind him the cellar door creaks open and a thin woman in a militia
        tabard steps out, dust on her shoulders. She looks at Hald, then at you, and says
        nothing. Hald walks out from behind the counter to the taproom hearth and stands
        with his back to it, arms folded.
        """;

    public static async Task<int> RunAsync(StoryWeaverSettings settings)
    {
        FileLlmLog log = new(settings.Logging);
        Console.WriteLine($"Logging to {log.FilePath}");
        Console.WriteLine();
        Console.WriteLine("Probing: nine-branch anyOf under strict json_schema ...");
        Console.WriteLine();

        using OpenRouterClient client = new(settings, log);

        LlmResult result = await client.CompleteAsync(new LlmCall
        {
            Role = LlmRole.Extraction,
            Schema = new JsonSchemaSpec(DeltaSchema.Name, DeltaSchema.Json),
            Messages =
            [
                LlmMessage.System(
                    "Extract state changes from the narration as deltas. Emit only changes the " +
                    "prose actually supports. The player is in location 'marrow-tavern'. Known " +
                    "characters: 'innkeeper-hald' (Hald). Known locations: 'marrow-tavern', " +
                    "'marrow-cellar'. Use those ids where they apply, and slug ids for anything " +
                    "new. Quote the prose in each evidence field."),
                LlmMessage.User(TestNarration),
            ],
        }).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            Console.WriteLine($"  FAILED after {result.Attempts} attempt(s): {result.Error}");
            Console.WriteLine();

            // Do not conclude "anyOf is unsupported" from any failure. The first run of this
            // probe failed on an exhausted token budget and said exactly that, which sent the
            // investigation in the wrong direction until the raw response was read.
            Console.WriteLine("  Read the error above before concluding anything about anyOf.");
            Console.WriteLine("  A budget or transport failure says nothing about schema support.");
            Console.WriteLine("  If it IS a schema rejection, the fallback is a flat object with a");
            Console.WriteLine("  'kind' enum plus nullable fields, validated in code after parsing.");
            return 1;
        }

        Console.WriteLine($"  served by {result.Model ?? "(unreported)"}, " +
                          $"{result.Usage?.TotalTokens ?? 0} tokens, {result.Attempts} attempt(s)");
        Console.WriteLine();
        Console.WriteLine(result.Content.Trim());
        Console.WriteLine();

        // The schema being honoured is necessary but not sufficient — it also has to round-trip
        // into the Core types, which is what §7 will actually do with it.
        return ReportRoundTrip(result.Content);
    }

    private static int ReportRoundTrip(string content)
    {
        DeltaEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<DeltaEnvelope>(content, StoryJson.Options);
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"  Schema honoured, but round-trip into StateDelta FAILED: {ex.Message}");
            return 1;
        }

        if (envelope?.Deltas is null || envelope.Deltas.Count == 0)
        {
            Console.WriteLine("  Parsed, but no deltas were returned.");
            return 1;
        }

        Console.WriteLine($"  Round-tripped into {envelope.Deltas.Count} StateDelta object(s):");

        foreach (StateDelta delta in envelope.Deltas)
        {
            Console.WriteLine($"    {delta.GetType().Name}");
        }

        Console.WriteLine();
        Console.WriteLine("  anyOf under strict mode works, and deserializes into Core types.");
        return 0;
    }

    private sealed class DeltaEnvelope
    {
        public List<StateDelta>? Deltas { get; init; }
    }

}
