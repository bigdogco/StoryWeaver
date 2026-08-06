using StoryWeaver.Core;

namespace StoryWeaver.Storage;

/// <summary>
/// Reads lore entries from a folder of markdown files — one file per entry.
///
/// <code>
/// ---
/// keys: investigator, king's men, the order
/// always: false
/// common: true
/// priority: 10
/// ---
///
/// # The King's Investigators
///
/// An order answering directly to the crown...
/// </code>
///
/// <b>The filename is the id.</b> <c>kings-investigators.md</c> becomes
/// <c>kings-investigators</c>, so the filesystem enforces uniqueness for free and there is no
/// <c>id:</c> field able to drift from the thing it names.
///
/// <b>Failures are loud.</b> A malformed entry names itself and stops the load. Silently
/// dropping one is the worst failure mode in this genre — the same shape as a silently
/// discarded delta or a budget-cut lorebook entry, and both are already written up as things
/// not to do.
///
/// Frontmatter is parsed by the shared <see cref="Frontmatter"/> reader, which is strict and
/// refuses unknown keys: a silently ignored typo means an entry missing the field its author
/// thought they wrote.
/// </summary>
public static class MarkdownLoreReader
{
    /// <summary>
    /// Loads every <c>*.md</c> in <paramref name="directory"/>. A missing directory is an
    /// empty lorebook, not an error — a pack need not ship lore.
    /// </summary>
    public static LoreBook Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return LoreBook.Empty;
        }

        List<LoreEntry> entries = [];

        foreach (string path in Directory.EnumerateFiles(directory, "*.md")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            entries.Add(Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path), path));
        }

        return new LoreBook(entries);
    }

    /// <summary>Exposed for the self-test, which parses strings rather than files.</summary>
    public static LoreEntry Parse(string text, string id, string source)
    {
        string[] lines = MarkdownFile.SplitLines(text);

        Frontmatter front = Frontmatter.Parse(lines, source);
        front.RejectUnknownKeys(source, "keys", "always", "common", "priority");

        (string title, string body) = MarkdownFile.HeadingAndBody(
            lines,
            front.BodyStart,
            source,
            missingHeading:
                "expected a '# Title' heading. The title is the entry's name and has no default.",
            missingBody:
                "the entry has no body. A title with nothing under it says nothing to the narrator.");

        return new LoreEntry
        {
            Id = id,
            Title = title,
            Body = body,
            Keys = front.List("keys"),
            Always = front.Flag("always", source),
            Common = front.Flag("common", source),
            Priority = front.Number("priority", source),
        };
    }
}
