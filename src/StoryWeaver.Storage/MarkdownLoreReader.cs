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
/// <b>Failures are loud.</b> A malformed entry names itself and its line and stops the load.
/// Silently dropping one is the worst failure mode in this genre — it is the same shape as a
/// silently discarded delta or a budget-cut lorebook entry, and both are already written up
/// as things not to do.
///
/// No YAML dependency on purpose. Three scalar fields and one comma-separated list is a small
/// strict parser that refuses what it does not understand; a YAML library would buy nested
/// structures the design deliberately does not have, and with them a much larger surface for
/// a pack to be subtly wrong.
/// </summary>
public static class MarkdownLoreReader
{
    private const string Fence = "---";

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

        foreach (string path in Directory.EnumerateFiles(directory, "*.md").OrderBy(p => p, StringComparer.Ordinal))
        {
            entries.Add(Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path), path));
        }

        return new LoreBook(entries);
    }

    /// <summary>Exposed for the self-test, which parses strings rather than files.</summary>
    public static LoreEntry Parse(string text, string id, string source)
    {
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        int cursor = 0;

        string[] keys = [];
        bool always = false;
        bool common = false;
        int priority = 0;

        if (cursor < lines.Length && lines[cursor].Trim() == Fence)
        {
            cursor++;

            while (cursor < lines.Length && lines[cursor].Trim() != Fence)
            {
                string line = lines[cursor].Trim();
                cursor++;

                if (line.Length == 0)
                {
                    continue;
                }

                int colon = line.IndexOf(':');

                if (colon <= 0)
                {
                    throw new InvalidDataException(
                        $"{source} line {cursor}: expected 'key: value' in the frontmatter, got '{line}'.");
                }

                string key = line[..colon].Trim().ToLowerInvariant();
                string value = line[(colon + 1)..].Trim();

                switch (key)
                {
                    case "keys":
                        keys = [.. value
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
                        break;

                    case "always":
                        always = ParseBool(value, source, cursor);
                        break;

                    case "common":
                        common = ParseBool(value, source, cursor);
                        break;

                    case "priority":
                        priority = int.TryParse(value, out int parsed)
                            ? parsed
                            : throw new InvalidDataException(
                                $"{source} line {cursor}: priority must be a whole number, got '{value}'.");
                        break;

                    default:
                        // Refused rather than ignored. An unknown key is a typo or a field
                        // from a newer format, and both are better surfaced than silently
                        // dropped — this is the one place a pack can be wrong invisibly.
                        throw new InvalidDataException(
                            $"{source} line {cursor}: unknown frontmatter key '{key}'. " +
                            "Known keys: keys, always, common, priority.");
                }
            }

            if (cursor >= lines.Length)
            {
                throw new InvalidDataException($"{source}: frontmatter was opened with '---' but never closed.");
            }

            cursor++;
        }

        while (cursor < lines.Length && lines[cursor].Trim().Length == 0)
        {
            cursor++;
        }

        if (cursor >= lines.Length || !lines[cursor].TrimStart().StartsWith("# ", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"{source}: expected a '# Title' heading. The title is the entry's name and has no default.");
        }

        string title = lines[cursor].TrimStart()[2..].Trim();
        cursor++;

        if (title.Length == 0)
        {
            throw new InvalidDataException($"{source}: the title heading is empty.");
        }

        string body = string.Join('\n', lines[cursor..]).Trim();

        if (body.Length == 0)
        {
            throw new InvalidDataException(
                $"{source}: the entry has no body. A title with nothing under it says nothing to the narrator.");
        }

        return new LoreEntry
        {
            Id = id,
            Title = title,
            Body = body,
            Keys = keys,
            Always = always,
            Common = common,
            Priority = priority,
        };
    }

    private static bool ParseBool(string value, string source, int line) => value.ToLowerInvariant() switch
    {
        "true" or "yes" => true,
        "false" or "no" => false,
        _ => throw new InvalidDataException(
            $"{source} line {line}: expected true or false, got '{value}'."),
    };
}
