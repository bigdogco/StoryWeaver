using StoryWeaver.Core;
using StoryWeaver.Storage;

namespace StoryWeaver.Harness;

/// <summary>
/// Writes the built-in world out as a pack seed.
///
/// Exists so the JSON seed and the C# fixture start provably identical rather than
/// approximately so. Hand-transcribing 285 lines of seed into JSON is exactly the kind of
/// task that silently loses a mood or a relationship standing, and the resulting difference
/// would look like a behaviour change in whatever got measured next.
///
/// Kept afterwards as an authoring aid: it turns any world this codebase can build — including
/// a save — into a starting pack.
/// </summary>
public static class SeedWriter
{
    public static int Run(string path)
    {
        WorldState world = WorldSeeds.Marrow();

        string? directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WorldPackWriter.WriteSeed(path, world);

        Console.WriteLine($"Wrote seed to {path}");
        Console.WriteLine(
            $"  {world.Locations.Count} location(s), {world.Characters.Count} character(s), " +
            $"{world.Facts.Count} fact(s)");

        return 0;
    }
}
