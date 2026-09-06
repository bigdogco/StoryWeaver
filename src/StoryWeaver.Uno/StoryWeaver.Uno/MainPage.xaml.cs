namespace StoryWeaver.Uno;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using StoryWeaver.App;
using StoryWeaver.Core;
using StoryWeaver.Llm.Configuration;

public sealed partial class MainPage : Page
{
    private const string PackId = "marrow";
    private const string SaveId = "uno-spike";

    private StorySession? _session;
    private SessionContext? _context;
    private bool _turnInProgress;

    public MainPage()
    {
        this.InitializeComponent();
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        await OpenSessionAsync();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _session?.Dispose();
        _session = null;
    }

    private async Task OpenSessionAsync()
    {
        try
        {
            string root = FindRepositoryRoot();
            Directory.SetCurrentDirectory(root);

            StoryWeaverSettings settings = SettingsLoader.Load(Path.Combine(root, SettingsLoader.DefaultFileName));
            SessionOpening opening = await SessionOpener.OpenAsync(
                settings,
                PackId,
                SaveId,
                force: false,
                saveRoot: Path.Combine(root, SessionOpener.DefaultSaveRoot),
                packRoot: Path.Combine(root, SessionOpener.DefaultPackRoot));

            _context = opening.Context;

            if (opening.WasRefused)
            {
                RenderOpenRefusal(opening);
                return;
            }

            if (opening.IsWaitingForPlayer)
            {
                RenderNeedsPlayer(opening);
                return;
            }

            _session = opening.Session
                ?? throw new InvalidOperationException("Session opener returned no session.");

            await RenderOpenedSessionAsync();
        }
        catch (Exception ex)
        {
            RenderFailure("Session could not be opened.", ex.Message);
        }
    }

    private async Task RenderOpenedSessionAsync()
    {
        if (_session is null || _context is null)
        {
            return;
        }

        WorldState world = _session.World;
        string root = FindRepositoryRoot();
        string saveDirectory = Path.Combine(root, SessionOpener.DefaultSaveRoot, SaveId);
        string version = string.IsNullOrWhiteSpace(_context.Pack.Version)
            ? string.Empty
            : $" v{_context.Pack.Version}";

        HeaderDetailText.Text = _context.Resumed
            ? $"Resumed {SaveId} at turn {_context.TurnNumber}."
            : $"Started {SaveId} from {_context.Pack.Name}{version}.";

        NarrationHeading.Text = _context.Resumed
            ? $"{_context.Pack.Name} resumed"
            : $"{_context.Pack.Name} opening";

        IReadOnlyList<TurnRecord> recentTurns = await _session.RecentTurnsAsync(_context.HistoryTurns);

        if (_context.Resumed && recentTurns.Count > 0)
        {
            OpeningText.Text = $"The story so far, last {recentTurns.Count} turn(s):";
            ScenarioText.Text = string.IsNullOrWhiteSpace(_context.Pack.Scenario)
                ? "No standing scenario is authored for this pack."
                : $"Scenario: {EntityReferences.Resolve(_context.Pack.Scenario, world)}";
            ResetStoryItems();

            foreach (TurnRecord turn in recentTurns)
            {
                AddStoryTurn(turn);
            }
        }
        else
        {
            OpeningText.Text = OpeningScene(world);
            ScenarioText.Text = string.IsNullOrWhiteSpace(_context.Pack.Scenario)
                ? "No standing scenario is authored for this pack."
                : $"Scenario: {EntityReferences.Resolve(_context.Pack.Scenario, world)}";
            ResetStoryItems();
        }

        ExplorerHeading.Text = $"Session: {SaveId}";
        RenderExplorer(world);

        SpikeStatusText.Text =
            $"Opened through StoryWeaver.App. Saving to {saveDirectory}. " +
            $"{world.Characters.Count} characters, {world.Locations.Count} locations, " +
            $"{world.Facts.Count} facts, {world.Items.Count} items.";

        SetInteractive(true);
    }

    private void ResetStoryItems()
    {
        StoryItems.Children.Clear();
        StoryItems.Children.Add(OpeningText);
        StoryItems.Children.Add(ScenarioText);
    }

    private void RenderExplorer(WorldState world)
    {
        ExplorerItems.Children.Clear();
        AddCharacterCard(world.Player, world);

        foreach (Character character in world.NpcsWithPlayer().OrderBy(c => c.Name, StringComparer.Ordinal))
        {
            AddCharacterCard(character, world);
        }

        if (world.PlayerLocationId is { } locationId && world.FindLocation(locationId) is { } location)
        {
            AddLocationCard(location);
            AddSuggestedActions(location);
        }
    }

    private void AddStoryTurn(TurnRecord turn)
    {
        StackPanel content = new() { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = $"> {turn.PlayerInput}",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        content.Children.Add(new TextBlock
        {
            Text = turn.Narration,
            TextWrapping = TextWrapping.Wrap,
        });

        AddStoryBlock(content);
    }

    private void AddStoryBlock(UIElement content)
    {
        StoryItems.Children.Add(new Border
        {
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10),
            Child = content,
        });
    }

    private string OpeningScene(WorldState world)
    {
        string text = _context?.Pack.HasOpening == true
            ? EntityReferences.Resolve(_context.Pack.Opening, world)
            : world.PlayerLocationId is { } id && world.FindLocation(id) is { } here
                ? here.Description
                : string.Empty;

        return string.IsNullOrWhiteSpace(text)
            ? "No opening text or player location description is available."
            : text;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SubmitTurnAsync();
    }

    private async void PlayerInputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await SubmitTurnAsync();
        }
    }

    private async Task SubmitTurnAsync()
    {
        if (_session is null || _turnInProgress)
        {
            return;
        }

        string input = PlayerInputBox.Text.Trim();
        if (input.Length == 0)
        {
            return;
        }

        _turnInProgress = true;
        SetInteractive(false);
        PlayerInputBox.Text = string.Empty;
        SpikeStatusText.Text = "Narrating and extracting the turn...";

        try
        {
            SessionResult<TurnOutcome> result = await _session.TakeTurnAsync(input);

            if (result.WasRefused)
            {
                SpikeStatusText.Text = $"Not now: {result.RefusedBecause}";
                return;
            }

            TurnOutcome outcome = result.Value!;
            AddStoryTurn(outcome.Turn);

            if (outcome.ExtractionFailed)
            {
                SpikeStatusText.Text =
                    $"Turn {outcome.Turn.TurnNumber} narrated, but extraction failed: {outcome.ExtractionError}";
            }
            else
            {
                SpikeStatusText.Text =
                    $"Turn {outcome.Turn.TurnNumber}: {outcome.Turn.Applied.Count} applied, " +
                    $"{outcome.Turn.NoOps.Count} no-op, {outcome.Turn.Rejected.Count} rejected.";
            }

            RenderExplorer(_session.World);
        }
        catch (Exception ex)
        {
            SpikeStatusText.Text = $"Turn failed: {ex.Message}";
        }
        finally
        {
            _turnInProgress = false;
            SetInteractive(_session is not null);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        SetInteractive(false);
        try
        {
            SessionResult<RefreshReport> result = await _session.UpdateStateAsync();

            if (result.WasRefused)
            {
                SpikeStatusText.Text = $"Cannot refresh: {result.RefusedBecause}";
                return;
            }

            RefreshReport report = result.Value!;
            RenderExplorer(_session.World);

            if (report.NothingOnDisk)
            {
                SpikeStatusText.Text = "Nothing saved yet; canon is only in this session.";
            }
            else if (report.Unchanged)
            {
                SpikeStatusText.Text = "Canon on disk matches this session.";
            }
            else
            {
                SpikeStatusText.Text =
                    $"Re-read canon: {report.Changes.Count} change(s), {report.Warnings.Count} warning(s).";
            }
        }
        catch (Exception ex)
        {
            SpikeStatusText.Text = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            SetInteractive(_session is not null);
        }
    }

    private void StateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || _context is null)
        {
            return;
        }

        OpeningText.Text = ContextAssembler.ForExtraction(
            _session.World,
            _context.Pack.Lore,
            _context.Pack.Sheets);
        ScenarioText.Text = "Extractor state view.";
        ResetStoryItems();
    }

    private void ProseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || _context is null)
        {
            return;
        }

        OpeningText.Text = ContextAssembler.ForNarration(
            _session.World,
            _context.Pack.Lore,
            _context.Pack.Sheets);
        ScenarioText.Text = string.IsNullOrWhiteSpace(_context.Pack.Scenario)
            ? "Narrator prose view."
            : $"Scenario: {EntityReferences.Resolve(_context.Pack.Scenario, _session.World)}";
        ResetStoryItems();
    }

    private void RenderOpenRefusal(SessionOpening opening)
    {
        SetInteractive(false);
        NarrationHeading.Text = "Cannot open session";
        OpeningText.Text = opening.RefusedBecause ?? "The save could not be opened.";
        ScenarioText.Text = opening.HeldBy is null ? string.Empty : $"Held by: {opening.HeldBy}";
        SpikeStatusText.Text = "Close the other StoryWeaver session, or clear the stale lock intentionally.";
    }

    private void RenderNeedsPlayer(SessionOpening opening)
    {
        SetInteractive(false);
        NarrationHeading.Text = "Player setup required";
        OpeningText.Text =
            "This pack does not author the player yet. The Uno spike has not built the player setup dialog.";
        ScenarioText.Text = opening.Context?.Pack.Name ?? string.Empty;
        SpikeStatusText.Text = "Use the CLI for this pack until the player creation dialog is added.";
    }

    private void RenderFailure(string title, string detail)
    {
        SetInteractive(false);
        NarrationHeading.Text = title;
        OpeningText.Text = detail;
        ScenarioText.Text = string.Empty;
        SpikeStatusText.Text = "Fix the issue and restart the Uno shell.";
    }

    private void SetInteractive(bool enabled)
    {
        PlayerInputBox.IsEnabled = enabled;
        SendButton.IsEnabled = enabled;
        RefreshButton.IsEnabled = enabled;
        StateButton.IsEnabled = enabled;
        ProseButton.IsEnabled = enabled;
    }

    private void AddSuggestedActions(Location location)
    {
        if (location.Connections.Count == 0)
        {
            return;
        }

        StackPanel actions = new() { Spacing = 6 };
        actions.Children.Add(new TextBlock
        {
            Text = "Suggested moves",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        foreach (string id in location.Connections.Order(StringComparer.Ordinal))
        {
            string label = _session?.World.FindLocation(id)?.Name ?? id;
            Button button = new()
            {
                Content = $"Go to {label}",
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            button.Click += (_, _) =>
            {
                PlayerInputBox.Text = $"*I go to {label}.*";
                PlayerInputBox.Focus(FocusState.Programmatic);
            };
            actions.Children.Add(button);
        }

        AddCard("Actions", actions);
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

        string description = string.IsNullOrWhiteSpace(character.Description)
            ? string.Empty
            : $"\n{character.Description}";

        AddCard(
            character.IsPlayer ? $"Player: {character.Name}" : character.Name,
            $"Location: {location}\nMood: {character.Mood}\nStatus: {character.Status}\n{knows}{description}");
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
        AddCard(title, new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private void AddCard(string title, UIElement body)
    {
        StackPanel content = new() { Spacing = 3 };
        content.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(body);

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
