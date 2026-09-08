using System.Text.Json;

namespace StoryWeaver.Desktop.Services;

public sealed record ViewPreferences
{
    public double StoryFraction { get; init; } = 0.52;
    public double NarrationSize { get; init; } = 18;
    public bool ShowWorldPanel { get; init; } = true;
    public string Theme { get; init; } = "System";
    public string? WorkspacePath { get; init; }
    public IReadOnlyList<ViewRecentPlaythrough> RecentPlaythroughs { get; init; } = [];

    public ViewPreferences Normalized() => this with
    {
        StoryFraction = double.IsFinite(StoryFraction) ? Math.Clamp(StoryFraction, 0.25, 0.75) : 0.52,
        NarrationSize = double.IsFinite(NarrationSize) ? Math.Clamp(NarrationSize, 14, 28) : 18,
        Theme = Theme is "Light" or "Dark" ? Theme : "System",
        WorkspacePath = string.IsNullOrWhiteSpace(WorkspacePath) ? null : WorkspacePath.Trim(),
        RecentPlaythroughs = RecentPlaythroughs
            .Where(recent => !string.IsNullOrWhiteSpace(recent.WorkspacePath)
                && !string.IsNullOrWhiteSpace(recent.PackId)
                && !string.IsNullOrWhiteSpace(recent.SaveId))
            .GroupBy(recent => new { Workspace = recent.WorkspacePath.Trim(), Pack = recent.PackId.Trim(), Save = recent.SaveId.Trim() })
            .Select(group => group.OrderByDescending(recent => recent.OpenedUtc).First().Normalized())
            .OrderByDescending(recent => recent.OpenedUtc)
            .Take(12)
            .ToList()
    };
}

public sealed record ViewRecentPlaythrough(string WorkspacePath, string PackId, string SaveId, DateTime OpenedUtc)
{
    public ViewRecentPlaythrough Normalized() => this with
    {
        WorkspacePath = WorkspacePath.Trim(),
        PackId = PackId.Trim(),
        SaveId = SaveId.Trim(),
        OpenedUtc = OpenedUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(OpenedUtc, DateTimeKind.Utc)
            : OpenedUtc.ToUniversalTime()
    };
}

/// <summary>Desktop view preferences and the selected workspace path. Never reads settings.local.json or saves.</summary>
public sealed class PreferencesStore
{
    public string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StoryWeaver", "desktop-view.json");

    public (ViewPreferences Preferences, string? Warning) Load()
    {
        try
        {
            if (!File.Exists(Path)) return (new(), null);
            var preferences = JsonSerializer.Deserialize<ViewPreferences>(File.ReadAllText(Path));
            return preferences is null
                ? (new(), "View preferences were empty; using defaults.")
                : (preferences.Normalized(), null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new(), "Could not read view preferences; using defaults. " + error.Message);
        }
    }

    public string? Save(ViewPreferences preferences)
    {
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences.Normalized(),
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, Path, overwrite: true);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return "Could not save view preferences. " + error.Message;
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
