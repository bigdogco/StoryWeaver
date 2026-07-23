using System.Text.Json;
using StoryWeaver.Core;

namespace StoryWeaver.Cli;

/// <summary>
/// Offline checks on delta serialization. No API calls, so this is free to run and there is
/// no excuse for skipping it.
///
/// It exists because a bug here is invisible: a delta that fails to deserialize is reported
/// as an extraction failure, which looks like a model problem, and a live session was spent
/// before the raw response was read. The specific failure — a provider emitting properties
/// alphabetically, putting <c>kind</c> last, which the built-in polymorphic reader cannot
/// handle — is the first case below.
/// </summary>
internal static class JsonSelfTest
{
    public static int Run()
    {
        int failures = 0;

        failures += Check(
            "kind first",
            """{"kind":"mood_changed","characterId":"innkeeper-hald","mood":"wary","evidence":"q"}""",
            typeof(MoodChanged));

        // The case that broke a live session. DigitalOcean served this ordering; another
        // provider on the same model id served kind-first and worked.
        failures += Check(
            "kind last (alphabetical)",
            """{"characterId":"innkeeper-hald","evidence":"q","mood":"wary","kind":"mood_changed"}""",
            typeof(MoodChanged));

        failures += Check(
            "kind in the middle",
            """{"characterId":"h","kind":"status_changed","status":"wounded","evidence":"q"}""",
            typeof(StatusChanged));

        failures += Check(
            "unusual casing",
            """{"KIND":"fact_learned","characterId":"h","factId":"f","evidence":"q"}""",
            typeof(FactLearned));

        failures += Check(
            "nullable field present as null",
            """{"kind":"character_introduced","characterId":"a","name":"A","description":"d","locationId":null,"evidence":"q"}""",
            typeof(CharacterIntroduced));

        failures += Check(
            "rename with a null description",
            """{"kind":"character_renamed","characterId":"figure-in-cistern","name":"Nessa","description":null,"evidence":"q"}""",
            typeof(CharacterRenamed));

        failures += CheckRoundTrip();
        failures += CheckCrossNamespaceIds();
        failures += CheckRejects("unknown kind", """{"kind":"teleported","characterId":"h"}""");
        failures += CheckRejects("missing kind", """{"characterId":"h","mood":"wary"}""");

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "All delta serialization checks passed."
            : $"{failures} check(s) FAILED.");

        return failures == 0 ? 0 : 1;
    }

    private static int Check(string name, string json, Type expected)
    {
        try
        {
            StateDelta? delta = JsonSerializer.Deserialize<StateDelta>(json, StoryJson.Options);

            if (delta is null)
            {
                Console.WriteLine($"  FAIL  {name}: deserialized to null.");
                return 1;
            }

            if (delta.GetType() != expected)
            {
                Console.WriteLine($"  FAIL  {name}: got {delta.GetType().Name}, expected {expected.Name}.");
                return 1;
            }

            Console.WriteLine($"  ok    {name} -> {delta.GetType().Name}");
            return 0;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"  FAIL  {name}: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Write then read. Saves in section 6 depend on this holding.</summary>
    private static int CheckRoundTrip()
    {
        StateDelta original = new RelationshipChanged("innkeeper-hald", -40, "openly hostile")
        {
            Evidence = "He turns his back.",
        };

        string json = JsonSerializer.Serialize(original, StoryJson.Options);
        StateDelta? back = JsonSerializer.Deserialize<StateDelta>(json, StoryJson.Options);

        if (back != original)
        {
            Console.WriteLine($"  FAIL  round trip: {json}");
            return 1;
        }

        Console.WriteLine("  ok    round trip preserves value equality");
        return 0;
    }

    /// <summary>
    /// A character's id must not be reusable as a location or fact id.
    ///
    /// Observed live: extraction emitted location_introduced with the id "innkeeper-hald",
    /// which existed as a character but not as a location. The per-type check passed and the
    /// bogus location was applied to canon. Nothing downstream would ever have flagged it.
    /// </summary>
    private static int CheckCrossNamespaceIds()
    {
        WorldState world = new();
        world.Characters["innkeeper-hald"] = new Character { Id = "innkeeper-hald", Name = "Hald" };

        ValidationOutcome outcome = DeltaValidator.Validate(world, [
            new LocationIntroduced("innkeeper-hald", "The Drowned Crow", "A taproom."),
        ]);

        if (outcome.Accepted.Count != 0 || outcome.Rejected.Count != 1)
        {
            Console.WriteLine(
                $"  FAIL  cross-namespace id: accepted {outcome.Accepted.Count}, " +
                $"rejected {outcome.Rejected.Count}; expected 0 and 1.");
            return 1;
        }

        Console.WriteLine("  ok    character id cannot be reused as a location");
        return 0;
    }

    /// <summary>
    /// These must throw rather than return null. A null would be indistinguishable from the
    /// model reporting no change — the failure mode this whole file exists to prevent.
    /// </summary>
    private static int CheckRejects(string name, string json)
    {
        try
        {
            JsonSerializer.Deserialize<StateDelta>(json, StoryJson.Options);
            Console.WriteLine($"  FAIL  {name}: accepted, should have thrown.");
            return 1;
        }
        catch (JsonException)
        {
            Console.WriteLine($"  ok    {name} rejected");
            return 0;
        }
    }
}
