using StoryWeaver.Storage;

namespace StoryWeaver.Desktop.Services;

/// <summary>
/// Reads the user-selected workspace for the Library. This is discovery only: opening a save
/// remains App's responsibility through SessionOpener.
/// </summary>
public static class WorkspaceLibrary
{
    public const string WorldsDirectoryName = "worlds";
    public const string SavesDirectoryName = "saves";

    public static WorkspaceContents Read(string workspacePath)
    {
        string root = Path.GetFullPath(workspacePath);
        string worlds = Path.Combine(root, WorldsDirectoryName);
        string saveRoot = Path.Combine(root, SavesDirectoryName);

        if (!Directory.Exists(worlds))
        {
            throw new DirectoryNotFoundException(
                $"'{root}' is not a StoryWeaver workspace because it has no {WorldsDirectoryName} folder.");
        }

        List<WorkspacePack> packs = [];
        foreach (string directory in Directory.EnumerateDirectories(worlds).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            string id = Path.GetFileName(directory);
            try
            {
                WorldPack pack = WorldPack.Load(worlds, id);
                packs.Add(WorkspacePack.Loaded(pack));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException)
            {
                packs.Add(WorkspacePack.Unavailable(id, error.Message));
            }
        }

        List<WorkspaceSave> saves = [];
        if (Directory.Exists(saveRoot))
        {
            foreach (string directory in Directory.EnumerateDirectories(saveRoot).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                string canonPath = Path.Combine(directory, "canon.json");
                if (!File.Exists(canonPath)) continue;
                SaveOrigin? origin = SaveOrigin.Read(directory);
                int? turns = null;
                DateTime? modified = null;
                try
                {
                    modified = File.GetLastWriteTimeUtc(canonPath);
                    using var canon = System.Text.Json.JsonDocument.Parse(File.ReadAllText(canonPath));
                    if (canon.RootElement.TryGetProperty("turnNumber", out var number) && number.TryGetInt32(out int value)) turns = value;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException) { }
                saves.Add(new WorkspaceSave(Path.GetFileName(directory), origin?.PackId, origin?.PackVersion, origin?.StartedUtc, turns, modified));
            }
        }

        return new WorkspaceContents(root, worlds, saves, packs);
    }

    public static string SaveRoot(string workspacePath) => Path.Combine(workspacePath, SavesDirectoryName);

    public static string PackRoot(string workspacePath) => Path.Combine(workspacePath, WorldsDirectoryName);
}

public sealed record WorkspaceContents(
    string RootPath,
    string PackRoot,
    IReadOnlyList<WorkspaceSave> Saves,
    IReadOnlyList<WorkspacePack> Packs);

public sealed record WorkspaceSave(string Id, string? PackId, string? PackVersion, DateTime? StartedUtc, int? Turns, DateTime? LastSavedUtc)
{
    public bool HasRecordedPack => !string.IsNullOrWhiteSpace(PackId);

    public string DisplayName => HasRecordedPack
        ? Id
        : $"{Id} (pack unknown)";

    public string Detail => WorldDetail + $"\nTurns: {Turns?.ToString() ?? "Unknown"} · Last saved: {LastSavedUtc?.ToLocalTime().ToString("g") ?? "Unknown"}"
        + (StartedUtc is { } started ? $"\nStarted: {started.ToLocalTime():g}" : string.Empty);

    private string WorldDetail => HasRecordedPack
        ? $"Started with {PackId}{(string.IsNullOrWhiteSpace(PackVersion) ? string.Empty : $" · {PackVersion}")}" 
        : "Created before pack provenance was recorded";
}

public sealed record WorkspacePack(
    string Id,
    string Name,
    string Author,
    string Version,
    string? Error)
{
    public bool CanOpen => Error is null;
    public string DisplayName => Name;
    public string Detail => Error ?? string.Join(" · ", [
        string.IsNullOrWhiteSpace(Author) ? "Unknown author" : Author,
        string.IsNullOrWhiteSpace(Version) ? "No version" : Version]);

    public static WorkspacePack Loaded(WorldPack pack) => new(
        pack.Id,
        string.IsNullOrWhiteSpace(pack.Manifest?.Name) ? pack.Id : pack.Manifest.Name,
        pack.Manifest?.Author ?? string.Empty,
        pack.Version,
        null);

    public static WorkspacePack Unavailable(string id, string error) => new(id, id, string.Empty, string.Empty, error);
}
