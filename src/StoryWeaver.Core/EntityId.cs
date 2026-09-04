namespace StoryWeaver.Core;

/// <summary>
/// The shape an authored id must have: lowercase letters, digits, and single hyphens between
/// them — <c>innkeeper-hald</c>, <c>marrow-tavern</c>, <c>kings-investigators</c>.
///
/// <b>Why enforce a convention that was already being followed.</b> An id is matched by exact
/// string comparison in several places at once: a sheet's filename against a <c>seed.json</c>
/// key, a <c>{{ }}</c> reference against canon, an attitude target against the lorebook.
/// <c>warrior_mike</c> and <c>warrior-mike</c> are the same character to a reader and two
/// different strings to all of it, and the difference is one glyph in the middle of a word —
/// which is exactly the kind of mistake that survives being looked for.
///
/// The failure it prevents is silent by construction: a sheet under one spelling and a seed
/// entry under the other produce a character who is placed nowhere and an entry nothing owns.
/// Both halves load. Neither complains.
///
/// <b>Two callers, which is why this is in Core.</b> Pack loading <i>requires</i> the shape and
/// throws — a mistyped filename is a mistyped id, and it is refused before a session starts.
/// <see cref="CanonRefresh"/> merely <i>warns</i> about it, because canon belongs to the player
/// and a reload reports rather than refuses. It lived in Storage until 2026-09-04, when the
/// second caller appeared and <c>Core</c>, which references nothing, could not reach it.
///
/// <b>Extraction was never held to this shape, and turns out to satisfy it anyway.</b> The
/// question was left open here deliberately — holding a proposed id to a convention costs a
/// rejection cascade rather than a refused load. Measured across every save before the warning
/// above was written: <b>549 ids, zero malformed</b>. So the check is silent on real play, and
/// the cheap version of this question is answered without the validator changing at all.
/// </summary>
public static class EntityId
{
    /// <summary>
    /// True for a non-empty id of lowercase alphanumeric runs joined by single hyphens.
    ///
    /// Hand-written rather than a regex: the rule is three conditions and reads as its own
    /// specification, where the pattern would need a comment to say the same thing.
    /// </summary>
    public static bool IsWellFormed(string? id)
    {
        if (string.IsNullOrEmpty(id) || id[0] == '-' || id[^1] == '-')
        {
            return false;
        }

        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];

            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                continue;
            }

            // A doubled hyphen is almost always a slug built from a title with punctuation in
            // it, and it makes two ids that differ only by an invisible amount.
            if (c == '-' && id[i - 1] != '-')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Throws naming the file, the id, and what kind of thing it was meant to name.
    ///
    /// The message says what a correct id looks like rather than which rule was broken: an
    /// author who typed <c>Warrior_Mike</c> broke two at once, and a list of violations is a
    /// worse answer than an example.
    /// </summary>
    public static void Require(string? id, string what, string file)
    {
        if (IsWellFormed(id))
        {
            return;
        }

        throw new InvalidDataException(
            $"{file}: '{id}' is not a usable {what} id. Ids are lowercase words joined by " +
            "single hyphens — 'warrior-mike', not 'warrior_mike' or 'Warrior Mike'. They are " +
            "matched exactly, so a near miss reads as a different thing entirely.");
    }
}
