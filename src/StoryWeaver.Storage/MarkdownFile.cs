namespace StoryWeaver.Storage;

/// <summary>
/// The shape shared by every authored markdown file in a pack: optional frontmatter, a single
/// <c>#</c> heading that names the thing, and a prose body.
///
/// Lore entries and character sheets differ only in what their frontmatter means, so the file
/// grammar lives here and each reader supplies its own vocabulary and its own error wording.
/// </summary>
internal static class MarkdownFile
{
    public static string[] SplitLines(string text) =>
        text.Replace("\r\n", "\n").Split('\n');

    /// <summary>
    /// Reads the heading and everything under it, starting after the frontmatter.
    ///
    /// Both failures are hard rather than defaulted. A file with no heading has no name, and
    /// a name has no sensible default; a file with no body tells the narrator nothing it did
    /// not already know, and would sit in a pack looking like content.
    /// </summary>
    public static (string Heading, string Body) HeadingAndBody(
        string[] lines,
        int start,
        string source,
        string missingHeading,
        string missingBody)
    {
        int cursor = start;

        while (cursor < lines.Length && lines[cursor].Trim().Length == 0)
        {
            cursor++;
        }

        if (cursor >= lines.Length
            || !lines[cursor].TrimStart().StartsWith("# ", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{source}: {missingHeading}");
        }

        string heading = lines[cursor].TrimStart()[2..].Trim();
        cursor++;

        if (heading.Length == 0)
        {
            throw new InvalidDataException($"{source}: the heading is empty.");
        }

        string body = string.Join('\n', lines[cursor..]).Trim();

        if (body.Length == 0)
        {
            throw new InvalidDataException($"{source}: {missingBody}");
        }

        return (heading, body);
    }
}
