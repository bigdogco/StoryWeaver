namespace StoryWeaver.Cli;

/// <summary>
/// The console's way of asking a question. Shared by the authoring commands and the edit
/// command, because a second copy of "blank means cancel" that drifted from the first would be
/// the sort of thing nobody notices until it eats an answer.
///
/// Every prompt treats **end of input as giving up**, not as a blank answer. That distinction
/// cost a real bug: character creation once looped forever on a closed stdin, spinning a prompt
/// while holding the save lock, because it could not tell "they pressed enter" from "there is
/// nobody there".
/// </summary>
internal static class ConsolePrompt
{
    /// <summary>Required text. Null means cancelled — blank, or end of input.</summary>
    public static string? Ask(string label)
    {
        Console.Write($"  {label} (blank to cancel): ");
        string? value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Optional text. Null means they left it blank, which is a legitimate answer.</summary>
    public static string? AskOptional(string label)
    {
        Console.Write($"  {label}: ");
        string? value = Console.ReadLine()?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool AskYesNo(string label, bool defaultYes)
    {
        Console.Write($"  {label} [{(defaultYes ? "Y/n" : "y/N")}]: ");
        string? value = Console.ReadLine()?.Trim();

        return string.IsNullOrWhiteSpace(value)
            ? defaultYes
            : value.StartsWith('y') || value.StartsWith('Y');
    }

    /// <summary>
    /// Explicit confirmation, defaulting to no and requiring the word rather than a letter.
    ///
    /// For the destructive case only. A <c>y</c> is muscle memory; typing <c>remove</c> is a
    /// decision, and this is the one place in the console where that difference is worth the
    /// extra keystrokes.
    /// </summary>
    public static bool Confirm(string word)
    {
        Console.Write($"  Type '{word}' to go ahead, anything else to cancel: ");
        string? value = Console.ReadLine()?.Trim();

        return string.Equals(value, word, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>One of a numbered list. Null means cancelled.</summary>
    public static int? AskChoice(string label, params string[] options)
    {
        for (int i = 0; i < options.Length; i++)
        {
            Console.WriteLine($"    {i + 1}  {options[i]}");
        }

        Console.Write($"  {label} (blank to cancel): ");
        string? value = Console.ReadLine()?.Trim();

        return int.TryParse(value, out int chosen) && chosen >= 1 && chosen <= options.Length
            ? chosen - 1
            : null;
    }
}
