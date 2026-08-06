namespace StoryWeaver.Storage;

/// <summary>
/// The `---` block at the top of an authored markdown file, parsed strictly.
///
/// Deliberately not YAML. It handles scalars and <b>exactly one</b> level of nesting, refuses
/// anything it does not understand, and has no dependency:
///
/// <code>
/// ---
/// keys: investigator, king's men
/// priority: 10
/// attitudes:
///   kings-investigators: fears them, will not say the name aloud
///   player: dislikes him — he stole his sword, years ago now
/// ---
/// </code>
///
/// The nesting level was added for character sheets, whose attitudes need a phrase per id —
/// "dislikes" alone loses the sentence the narrator would actually have used. It is a
/// deliberate extension of a format kept small on purpose, and the next request for nesting
/// should be argued on its own merits rather than waved through as precedent.
///
/// Strictness is the point. An unknown key is an error, because a silently ignored typo means
/// an entry missing the field its author thought they wrote — the quiet-failure shape this
/// project keeps deciding not to have.
/// </summary>
internal sealed class Frontmatter
{
    private const string Fence = "---";

    private Frontmatter(
        Dictionary<string, string> scalars,
        Dictionary<string, Dictionary<string, string>> maps,
        int bodyStart)
    {
        Scalars = scalars;
        Maps = maps;
        BodyStart = bodyStart;
    }

    public Dictionary<string, string> Scalars { get; }

    public Dictionary<string, Dictionary<string, string>> Maps { get; }

    /// <summary>Index of the first line after the closing fence.</summary>
    public int BodyStart { get; }

    /// <summary>
    /// Reads the block if the file opens with one. A file with no frontmatter is legitimate —
    /// the minimum viable entry is a heading and a paragraph.
    /// </summary>
    public static Frontmatter Parse(string[] lines, string source)
    {
        Dictionary<string, string> scalars = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, Dictionary<string, string>> maps = new(StringComparer.OrdinalIgnoreCase);

        if (lines.Length == 0 || lines[0].Trim() != Fence)
        {
            return new Frontmatter(scalars, maps, 0);
        }

        int cursor = 1;
        string? openMap = null;

        while (cursor < lines.Length && lines[cursor].Trim() != Fence)
        {
            string raw = lines[cursor];
            string line = raw.Trim();
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

            string key = line[..colon].Trim();
            string value = line[(colon + 1)..].Trim();

            // Indentation is what makes a line part of the map above it. Only one level is
            // supported, so a nested line with no open map is a mistake worth naming rather
            // than guessing at.
            bool indented = raw.Length > 0 && char.IsWhiteSpace(raw[0]);

            if (indented)
            {
                if (openMap is null)
                {
                    throw new InvalidDataException(
                        $"{source} line {cursor}: '{key}' is indented but no key opened a block above it.");
                }

                if (value.Length == 0)
                {
                    throw new InvalidDataException(
                        $"{source} line {cursor}: '{key}' has no value. Nesting is one level deep only.");
                }

                maps[openMap][key] = value;
                continue;
            }

            if (value.Length == 0)
            {
                openMap = key;
                maps[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            openMap = null;
            scalars[key] = value;
        }

        if (cursor >= lines.Length)
        {
            throw new InvalidDataException(
                $"{source}: frontmatter was opened with '---' but never closed.");
        }

        return new Frontmatter(scalars, maps, cursor + 1);
    }

    /// <summary>
    /// Refuses any key the caller did not expect. Callers pass every name they handle, in both
    /// forms, so a typo fails the load instead of vanishing.
    /// </summary>
    public void RejectUnknownKeys(string source, params string[] known)
    {
        HashSet<string> allowed = new(known, StringComparer.OrdinalIgnoreCase);

        foreach (string key in Scalars.Keys.Concat(Maps.Keys))
        {
            if (!allowed.Contains(key))
            {
                throw new InvalidDataException(
                    $"{source}: unknown frontmatter key '{key}'. Known keys: {string.Join(", ", known)}.");
            }
        }
    }

    public bool Flag(string key, string source) =>
        !Scalars.TryGetValue(key, out string? value) ? false : ParseBool(value, key, source);

    public int Number(string key, string source)
    {
        if (!Scalars.TryGetValue(key, out string? value))
        {
            return 0;
        }

        return int.TryParse(value, out int parsed)
            ? parsed
            : throw new InvalidDataException(
                $"{source}: {key} must be a whole number, got '{value}'.");
    }

    public string[] List(string key) =>
        Scalars.TryGetValue(key, out string? value)
            ? [.. value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : [];

    public IReadOnlyDictionary<string, string> Map(string key) =>
        Maps.TryGetValue(key, out Dictionary<string, string>? map)
            ? map
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static bool ParseBool(string value, string key, string source) => value.ToLowerInvariant() switch
    {
        "true" or "yes" => true,
        "false" or "no" => false,
        _ => throw new InvalidDataException(
            $"{source}: {key} expected true or false, got '{value}'."),
    };
}
