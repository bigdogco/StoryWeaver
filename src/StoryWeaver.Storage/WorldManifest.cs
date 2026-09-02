namespace StoryWeaver.Storage;

/// <summary>
/// A pack's identity: what it is called, who wrote it, and which version this is.
///
/// <b>Optional, and carries nothing the engine needs to run.</b> A pack without one is named
/// after its folder and plays identically — which is how every pack worked until now, and must
/// keep working.
///
/// The display fields exist for a world the player picks from a list rather than names on a
/// command line, which is a UI that does not exist yet. <see cref="Version"/> is the field that
/// does present work: a save records what it was started against, so when an author edits a
/// pack under a live save the warning can say *what* changed instead of only that something
/// did.
/// </summary>
public sealed record WorldManifest
{
    /// <summary>
    /// The pack id. Must equal the folder name, which is the id by decision — the manifest
    /// only restates it, and a disagreement is a copied pack whose directory was renamed and
    /// whose file was not.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>What to call this world in front of a person. Falls back to the folder id.</summary>
    public string Name { get; init; } = string.Empty;

    public string Author { get; init; } = string.Empty;

    /// <summary>
    /// Free-form on purpose. Nothing compares versions for ordering — a save records the string
    /// it was started against and a later session reports when it differs, which needs equality
    /// and nothing more. Imposing semantic versioning would be a rule with no reader.
    /// </summary>
    public string Version { get; init; } = string.Empty;
}
