using System.Text.Json;
using StoryWeaver.Core;

namespace StoryWeaver.Storage;

/// <summary>
/// Writes pack content. Currently just the seed.
///
/// Separate from <see cref="WorldPack"/>, which reads. A pack is authored by hand or by an
/// editor, so writing is the rarer path and does not belong on the type every session loads.
/// </summary>
public static class WorldPackWriter
{
    /// <summary>
    /// Writes a <see cref="WorldState"/> as a pack seed, using the same options that write
    /// canon so a seed and a save are the same format by construction.
    /// </summary>
    public static void WriteSeed(string path, WorldState world)
    {
        // Turn 0 regardless of what was handed in, so writing a mid-story save out as a seed
        // produces a starting world rather than one that opens at turn 51.
        int original = world.TurnNumber;
        world.TurnNumber = 0;

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(world, SaveJson.Canon));
        }
        finally
        {
            world.TurnNumber = original;
        }
    }
}
