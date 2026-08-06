using System.Text.RegularExpressions;

namespace StoryWeaver.Core;

/// <summary>
/// The <c>{{ }}</c> forms authored content may use to name entities it does not own.
///
/// <code>
/// Hald is wary of {{player}} and drinks with {{hedge-witch-morwenna}}.
/// </code>
///
/// **Why they exist.** A pack author cannot know the player's name — that is the whole of the
/// case, and it is the one SillyTavern has. `{{&lt;id&gt;}}` covers a narrower second case: a pack
/// shipping a deliberately anonymous character the story later reveals, where sheets referring
/// to them should follow.
///
/// **Resolved per turn, not at load.** Resolving once when a file is read would freeze the name
/// and lose the point.
///
/// **A closed set of two forms.** Anything else is a load error. SillyTavern's macros grew
/// conditionals, randomness and state lookups; adding a third form here should be a decision
/// rather than a discovery.
/// </summary>
public static partial class EntityReferences
{
    /// <summary>The literal the player's own character is referred to by, whatever they named
    /// themselves.</summary>
    public const string PlayerToken = "player";

    /// <summary>
    /// Replaces every reference with the entity's current name.
    ///
    /// An unresolvable reference is replaced with nothing rather than left in place. It should
    /// never get this far — <see cref="Unresolved"/> fails the pack load first — but if it
    /// does, an empty gap is a bad sentence while <c>{{innkeeper-hald}}</c> in the prose is the
    /// id leak that forced the ForNarration/ForExtraction split, and has been paid for once.
    /// </summary>
    public static string Resolve(string text, WorldState world)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("{{", StringComparison.Ordinal))
        {
            return text;
        }

        return Reference().Replace(text, match =>
        {
            string id = match.Groups[1].Value.Trim();

            if (string.Equals(id, PlayerToken, StringComparison.OrdinalIgnoreCase))
            {
                return world.Player?.Name ?? string.Empty;
            }

            return world.FindCharacter(id)?.Name
                   ?? world.FindLocation(id)?.Name
                   ?? world.FindItem(id)?.Name
                   ?? string.Empty;
        });
    }

    /// <summary>
    /// Every reference in the text that names nothing in the world — checked at pack load so a
    /// typo fails by name instead of reaching a prompt as a blank.
    /// </summary>
    public static IEnumerable<string> Unresolved(string text, WorldState world)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (Match match in Reference().Matches(text))
        {
            string id = match.Groups[1].Value.Trim();

            if (string.Equals(id, PlayerToken, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (world.FindCharacter(id) is null
                && world.FindLocation(id) is null
                && world.FindItem(id) is null)
            {
                yield return id;
            }
        }
    }

    [GeneratedRegex(@"\{\{([^{}]*)\}\}")]
    private static partial Regex Reference();
}
