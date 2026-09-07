using System.Text.Json;

namespace StoryWeaver.Desktop.Services;

public sealed record ViewPreferences
{
    public double StoryFraction { get; init; } = 0.52;
    public double NarrationSize { get; init; } = 18;
    public bool ShowWorldPanel { get; init; } = true;
    public string Theme { get; init; } = "System";

    public ViewPreferences Normalized() => this with
    {
        StoryFraction = double.IsFinite(StoryFraction) ? Math.Clamp(StoryFraction, 0.25, 0.75) : 0.52,
        NarrationSize = double.IsFinite(NarrationSize) ? Math.Clamp(NarrationSize, 14, 28) : 18,
        Theme = Theme is "Light" or "Dark" ? Theme : "System"
    };
}

/// <summary>Desktop presentation preferences only. Never reads settings.local.json or saves.</summary>
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
