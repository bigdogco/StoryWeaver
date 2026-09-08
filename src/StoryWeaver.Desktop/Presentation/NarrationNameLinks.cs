using System.Text.RegularExpressions;

namespace StoryWeaver.Desktop.Presentation;

/// <summary>Optional display links from current, unique names; never changes saved prose.</summary>
internal static class NarrationNameLinks
{
    public static IReadOnlyList<NarrativeSpan> Create(string text, IEnumerable<EntityDetails> entities)
    {
        var candidates = entities.Where(entity => entity.Reference.Kind != EntityKind.Fact && !string.IsNullOrWhiteSpace(entity.Name)).ToList();
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "the", "and", "of", "Mr", "Mrs", "Ms", "Miss", "Dr", "Sir", "Lady", "Lord", "Detective", "Sergeant", "Inspector", "Captain", "Officer", "King", "Queen" };
        var fullNames = candidates.Select(entity => (Name: entity.Name, entity.Reference));
        var shortNames = candidates.Where(entity => entity.Reference.Kind == EntityKind.Character)
            .SelectMany(entity => Regex.Matches(entity.Name, @"[\p{L}\p{M}]+(?:['’-][\p{L}\p{M}]+)*")
                .Select(match => match.Value)
                .Where(name => name.Length >= 3 && !excluded.Contains(name))
                .Select(name => (Name: name, entity.Reference)));
        var names = fullNames.Concat(shortNames)
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Reference).Distinct().Count() == 1 ? group.First().Reference : null,
                StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0 || text.Length == 0) return [new(text)];

        // Match longer names first, including ambiguous ones, so a short unique name
        // cannot steal a substring from a longer ambiguous name.
        string alternatives = string.Join("|", names.Keys.OrderByDescending(name => name.Length).Select(Regex.Escape));
        var pattern = new Regex(@"(?<![\p{L}\p{M}\p{N}_])(?:" + alternatives + @")(?![\p{L}\p{M}\p{N}_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        List<NarrativeSpan> spans = [];
        int offset = 0;
        try
        {
            foreach (Match match in pattern.Matches(text))
            {
                if (match.Index > offset) spans.Add(new(text[offset..match.Index]));
                names.TryGetValue(match.Value, out var target);
                spans.Add(new(match.Value, target));
                offset = match.Index + match.Length;
            }
        }
        catch (RegexMatchTimeoutException) { return [new(text)]; }
        if (offset < text.Length) spans.Add(new(text[offset..]));
        return spans;
    }
}
