namespace StoryWeaver.Core;

/// <summary>
/// The lore entries a world pack ships, in memory.
///
/// Separate from <see cref="WorldState"/> on purpose, and the split is the whole design:
/// <b>a pack is content and a save is state</b>. Entries are authored, static and shared;
/// canon is generated, per-playthrough and private. Conflating them is what stops the
/// character-card ecosystem from updating a world without breaking saves, or sharing a world
/// without shipping somebody's playthrough inside it.
///
/// A consequence to hold on to: a save may reference an entry a later version of the pack no
/// longer defines. That is <b>not corruption</b> — it is a dangling reference into content
/// that moved, and it drops with a warning. The opposite rule from facts, and correct,
/// because content and state have different lifetimes.
/// </summary>
public sealed class LoreBook
{
    public static readonly LoreBook Empty = new([]);

    private readonly Dictionary<string, LoreEntry> _entries;

    public LoreBook(IEnumerable<LoreEntry> entries)
    {
        _entries = entries.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
    }

    public int Count => _entries.Count;

    public IReadOnlyCollection<string> Ids => _entries.Keys;

    public IEnumerable<LoreEntry> All => _entries.Values.OrderBy(e => e.Id, StringComparer.Ordinal);

    public LoreEntry? Find(string id) =>
        _entries.TryGetValue(id, out LoreEntry? entry) ? entry : null;

    public bool Contains(string id) => _entries.ContainsKey(id);

    /// <summary>
    /// Entries to put in front of the narrator this turn.
    ///
    /// Everything, for now, ordered by priority. Keyed retrieval is deliberately not built
    /// yet: it introduces a way to *silently* omit an entry, which is the single biggest
    /// source of "why did the AI forget the Duke existed" in existing tools, and the budget
    /// it needs should be set against a measurement rather than a guess about world size.
    /// The seam exists here so adding it later does not move any caller.
    /// </summary>
    public IEnumerable<LoreEntry> Selected() =>
        _entries.Values
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.Id, StringComparer.Ordinal);

    /// <summary>
    /// The entries a character has heard of: everything marked <see cref="LoreEntry.Common"/>,
    /// plus whatever their <see cref="Character.Knows"/> names.
    ///
    /// <b>Derived, never stored.</b> Common knowledge is answered from the pack every time it
    /// is asked rather than copied into a save, so canon keeps meaning "what this character
    /// learned" and an author flipping the flag off does not leave saves holding entries that
    /// look learned and are not.
    ///
    /// One namespace with facts, which is what lets learning lore in play reuse
    /// <see cref="FactLearned"/> instead of needing a delta kind of its own. Unresolvable ids
    /// are skipped in silence — they are either facts (handled by
    /// <see cref="WorldState.KnownFacts"/>) or references into content that moved.
    /// </summary>
    public IEnumerable<LoreEntry> KnownBy(Character character) =>
        _entries.Values
            .Where(e => e.Common || character.Knows.Contains(e.Id))
            .OrderBy(e => e.Id, StringComparer.Ordinal);
}
