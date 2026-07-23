namespace StoryWeaver.Core;

/// <summary>
/// A named topic with a body of prose — an order, a religion, a war, a bloodline. The
/// reference material a DnD lorebook holds.
///
/// <b>Not a <see cref="Fact"/>.</b> A fact is one nameless proposition that is true or not,
/// and it is deliberately nameless so the extraction model is never invited to invent titles
/// for statements. Lore is a *named topic with a body*, and the nameless argument does not
/// apply because <b>lore is authored and never extracted</b> — a human writes the title.
/// Shredding "the King's Investigators" into six atomic facts would be tedious and lossy.
///
/// <b>Not part of canon.</b> Entries are pack content: authored, static, edited between
/// sessions, shipped and version-controlled independently of anyone's playthrough. The only
/// trace lore leaves in a save is which characters know it, which lives on
/// <see cref="Character.Knows"/> like any other knowledge. That asymmetry is the reason this
/// type is not in <see cref="WorldState"/>.
///
/// <b>Only for things with no entity representation.</b> Once something is a real
/// <see cref="Character"/> or <see cref="Location"/>, that entity is authoritative. A lore
/// entry about Hald and the <see cref="Character"/> Hald would drift apart, which is the
/// exact incoherence canon exists to prevent.
/// </summary>
public sealed class LoreEntry
{
    /// <summary>Slug, taken from the filename. Globally unique across characters, locations,
    /// facts and lore — the same namespace every other id lives in.</summary>
    public required string Id { get; init; }

    /// <summary>The topic, as a human wrote it: "The King's Investigators".</summary>
    public required string Title { get; init; }

    /// <summary>The reference text. Sent to the narrator; never to the extractor, which has
    /// no use for prose and every tendency to invent from it.</summary>
    public required string Body { get; init; }

    /// <summary>
    /// Words that should pull this entry into context when they appear in play.
    ///
    /// Unused by the first implementation, which injects everything — keyed retrieval waits
    /// until a world is large enough to prove it is needed. Authored now because the format
    /// is what gets baked into save-adjacent files, and adding a field later means touching
    /// every pack that exists.
    /// </summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>Inject regardless of keys — the world premise, the tone, the paragraph that
    /// must never fall out. A lorebook "constant" entry.</summary>
    public bool Always { get; init; }

    /// <summary>
    /// Everybody has heard of this. The kingdom they live in, its king, the war that ended
    /// last spring — things it would be strange for anyone not to know.
    ///
    /// <b>A different axis from <see cref="Always"/>, which is why it is a second flag.</b>
    /// <see cref="Always"/> answers "is this in context at all" and is about retrieval;
    /// this answers "who may refer to it" and is about knowledge. A kingdom is both. A secret
    /// cult may well be <see cref="Always"/> — the narrator needs it for tone — and must not
    /// be common, because who knows about it is the plot. One field could not say that.
    ///
    /// <b>Never written into <see cref="Character.Knows"/>.</b> It is resolved when the
    /// lorebook is read, so canon keeps meaning "what this character learned" and the pack
    /// stays the authority on what everyone knows. Materialising it on load would copy pack
    /// content into save state, and a later edit setting this false would leave every save
    /// holding entries indistinguishable from genuinely learned ones.
    /// </summary>
    public bool Common { get; init; }

    /// <summary>What survives when the budget is tight. Higher wins. Unused until budgeting
    /// exists, and authored for the same reason as <see cref="Keys"/>.</summary>
    public int Priority { get; init; }
}
