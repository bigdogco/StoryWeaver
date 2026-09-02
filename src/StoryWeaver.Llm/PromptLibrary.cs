using System.Security.Cryptography;
using System.Text;

namespace StoryWeaver.Llm;

/// <summary>
/// The engine's prompts, read from <c>prompts/*.md</c> at the repository root.
///
/// <b>No prompt string lives in code.</b> Every one of these was a <c>const string</c> until
/// 2026-08-16, which meant tuning the narrator's voice required a rebuild and shipping a new
/// binary — and meant a world author could not touch it at all. They are content, they are
/// edited far more often than the code around them, and they belong in files.
///
/// Found by walking up from the executable, the same way <c>SettingsLoader</c> finds settings,
/// so the real files sit at the repo root where they are easy to edit and an edit needs no
/// rebuild.
///
/// <b>A missing file is a loud startup failure</b>, not a silent default. A narrator with no
/// prompt is not a degraded narrator; it is an unpredictable one, and this project's standing
/// preference is to fail where the cause is obvious rather than where the symptom appears.
///
/// <b>What stays in code:</b> <see cref="Story.DeltaSchema"/>. Its descriptions are prompt
/// engineering too, but they are welded to the schema's structure, which has to move in lockstep
/// with <c>DeltaApplier</c> and the C# delta types. On disk, a branch could be edited without the
/// applier knowing and a delta kind would silently stop working — a desync that cannot happen
/// while the two are compiled together.
/// </summary>
public sealed class PromptLibrary
{
    public const string DirectoryName = "prompts";

    private PromptLibrary(string directory, string narration, string extraction, string repair)
    {
        Directory = directory;
        Narration = narration;
        Extraction = extraction;
        Repair = repair;
    }

    /// <summary>Where the prompts were found, for the banner and for error messages.</summary>
    public string Directory { get; }

    /// <summary>
    /// The narrator's rules and default voice. A pack may add to this — never replace it — so
    /// the player-agency and canon rules cannot be dropped by an author writing a voice.
    /// </summary>
    public string Narration { get; }

    /// <summary>
    /// The extraction rules. <b>A pack may not override this at all</b>: narration is taste,
    /// extraction is correctness measured at 100% across the scored set, and a pack quietly
    /// replacing it would invalidate every measurement while looking like a content change.
    /// </summary>
    public string Extraction { get; }

    /// <summary>Corrective instructions for the repair round-trip. Two sections, by heading.</summary>
    public string Repair { get; }

    /// <summary>
    /// A short hash of every prompt in use.
    ///
    /// <b>This exists because the files are editable.</b> The project's hardest-won measurement
    /// rule is that a score without a provider name attached is not a measurement; once prompts
    /// live on disk, a score without knowing *which prompt* is equally meaningless — and unlike
    /// a <c>const</c> in a commit, a file can be edited between two runs leaving no trace in the
    /// result. The eval prints this beside the provider so a moved number is explainable.
    /// </summary>
    public string Fingerprint => ShortHash(Narration + Extraction + Repair);

    public static PromptLibrary Load(string? directory = null)
    {
        string resolved = directory ?? FindPromptDirectory()
            ?? throw new InvalidOperationException(
                $"No '{DirectoryName}' directory found from {AppContext.BaseDirectory} upward. " +
                "The engine's prompts live there; see docs/todo/TODO_PROMPTS_AS_FILES.md.");

        return new PromptLibrary(
            resolved,
            Read(resolved, "narration.md"),
            Read(resolved, "extraction.md"),
            Read(resolved, "repair.md"));
    }

    /// <summary>
    /// The body of a <c>## heading</c> section, trimmed. Used by the repair prompt, whose file
    /// holds two variants plus the prose explaining why each line is there — that reasoning is
    /// for a human reading the file and must not be sent to a model.
    /// </summary>
    public static string Section(string markdown, string heading)
    {
        string[] lines = markdown.Split('\n');
        StringBuilder body = new();
        bool inside = false;

        foreach (string line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inside)
                {
                    break;
                }

                inside = string.Equals(
                    line[3..].Trim(), heading, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inside)
            {
                body.AppendLine(line);
            }
        }

        return body.ToString().Trim();
    }

    private static string Read(string directory, string file)
    {
        string path = Path.Combine(directory, file);

        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Missing prompt file '{path}'. The engine will not run without it — a narrator " +
                "with no prompt is unpredictable rather than merely plain.");
        }

        string text = File.ReadAllText(path).Trim();

        if (text.Length == 0)
        {
            throw new InvalidOperationException($"Prompt file '{path}' is empty.");
        }

        return text;
    }

    private static string? FindPromptDirectory()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, DirectoryName);

            if (System.IO.Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static string ShortHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8].ToLowerInvariant();
}
