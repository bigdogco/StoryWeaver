namespace StoryWeaver.Uno;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using StoryWeaver.Core;
using StoryWeaver.Storage;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
        LoadPackPreview();
    }

    private void LoadPackPreview()
    {
        try
        {
            string root = FindRepositoryRoot();
            WorldPack pack = WorldPack.Load(Path.Combine(root, "worlds"), "marrow");
            WorldState world = pack.Seed
                ?? throw new InvalidDataException("Pack 'marrow' has no seed.json.");

            NarrationHeading.Text = $"{pack.Name} opening";
            OpeningText.Text = string.IsNullOrWhiteSpace(pack.Opening)
                ? world.FindLocation(world.PlayerLocationId ?? string.Empty)?.Description
                    ?? "No opening text or player location description is available."
                : pack.Opening;
            ScenarioText.Text = string.IsNullOrWhiteSpace(pack.Scenario)
                ? "No standing scenario is authored for this pack."
                : $"Scenario: {pack.Scenario}";

            ExplorerItems.Children.Clear();
            AddCharacterCard(world.Player, world);

            foreach (Character character in world.NpcsWithPlayer().OrderBy(c => c.Name, StringComparer.Ordinal))
            {
                AddCharacterCard(character, world);
            }

            if (world.PlayerLocationId is { } locationId && world.FindLocation(locationId) is { } location)
            {
                AddLocationCard(location);
            }

            SpikeStatusText.Text =
                $"Loaded real pack data from worlds/marrow. " +
                $"{world.Characters.Count} characters, {world.Locations.Count} locations, " +
                $"{world.Facts.Count} facts, {world.Items.Count} items. Read-only spike; no save opened.";
        }
        catch (Exception ex)
        {
            OpeningText.Text = "Pack preview could not be loaded.";
            ScenarioText.Text = ex.Message;
            SpikeStatusText.Text = "Read-only backend wiring failed; see the message above.";
        }
    }

    private void AddCharacterCard(Character? character, WorldState world)
    {
        if (character is null)
        {
            AddCard("Player", "No player character exists in this pack seed.");
            return;
        }

        string location = character.LocationId is { } id && world.FindLocation(id) is { } place
            ? place.Name
            : "offstage";

        string knows = character.Knows.Count == 0
            ? "Knows: no recorded facts"
            : $"Knows: {character.Knows.Count} recorded facts";

        AddCard(
            character.IsPlayer ? $"Player: {character.Name}" : character.Name,
            $"Location: {location}\nMood: {character.Mood}\nStatus: {character.Status}\n{knows}");
    }

    private void AddLocationCard(Location location)
    {
        string connections = location.Connections.Count == 0
            ? "Connections: none"
            : $"Connections: {string.Join(", ", location.Connections.Order(StringComparer.Ordinal))}";

        string status = string.IsNullOrWhiteSpace(location.Status)
            ? "Status: normal"
            : $"Status: {location.Status}";

        AddCard(location.Name, $"{status}\n{connections}");
    }

    private void AddCard(string title, string body)
    {
        StackPanel content = new() { Spacing = 3 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
        });

        ExplorerItems.Children.Add(new Border
        {
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = content,
        });
    }

    private static string FindRepositoryRoot()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory = new(start);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "StoryWeaver.sln"))
                    && Directory.Exists(Path.Combine(directory.FullName, "worlds")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate StoryWeaver.sln and worlds/.");
    }
}
