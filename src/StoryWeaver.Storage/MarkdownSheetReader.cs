using StoryWeaver.Core;

namespace StoryWeaver.Storage;

/// <summary>
/// Reads character sheets from a folder of markdown files — one file per character, the same
/// shape as lore entries.
///
/// <code>
/// ---
/// attitudes:
///   kings-investigators: fears them, will not say the name aloud
///   player: dislikes him — he stole his sword, years ago now
/// ---
///
/// # Hald
///
/// Heavyset and watchful, with a publican's memory for faces. He wipes the same patch of
/// counter when he is thinking, and does not notice he is doing it.
///
/// ## Manner
///
/// Speaks flatly and briefly...
/// </code>
///
/// Filename is the id, <c>#</c> heading is the name, body is what the narrator reads. Headings
/// inside the body are the author's own — nothing here requires or interprets them.
/// </summary>
public static class MarkdownSheetReader
{
    public static IReadOnlyList<CharacterSheet> Load(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        List<CharacterSheet> sheets = [];

        foreach (string path in Directory.EnumerateFiles(directory, "*.md")
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            sheets.Add(Parse(File.ReadAllText(path), Path.GetFileNameWithoutExtension(path), path));
        }

        return sheets;
    }

    /// <summary>Exposed for the self-test, which parses strings rather than files.</summary>
    public static CharacterSheet Parse(string text, string id, string source)
    {
        string[] lines = MarkdownFile.SplitLines(text);

        Frontmatter front = Frontmatter.Parse(lines, source);
        front.RejectUnknownKeys(source, "attitudes");

        (string name, string body) = MarkdownFile.HeadingAndBody(
            lines,
            front.BodyStart,
            source,
            missingHeading:
                "expected a '# Name' heading. A character's name has no sensible default.",
            missingBody:
                "the sheet has no body. A name with nothing under it tells the narrator nothing.");

        return new CharacterSheet
        {
            Id = id,
            Name = name,
            Body = body,
            Attitudes = front.Map("attitudes"),
        };
    }
}
