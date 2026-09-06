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

    private enum StoryViewMode
    {
        Transcript,
        State,
        Prose,
    }

    private StorySession? _session;
    private SessionContext? _context;
    private readonly List<TurnRecord> _transcriptTurns = [];
    private StoryViewMode _viewMode = StoryViewMode.Transcript;
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
        StatusPillText.Text = _context.Resumed ? "Resumed" : "Ready";
        TurnText.Text = $"Turn {world.TurnNumber}";

        NarrationHeading.Text = _context.Resumed
            ? $"{_context.Pack.Name} resumed"
            : $"{_context.Pack.Name} opening";

        _transcriptTurns.Clear();
        _transcriptTurns.AddRange(await _session.RecentTurnsAsync(_context.HistoryTurns));
        RenderTranscriptView();

        ExplorerHeading.Text = $"Session: {SaveId}";
        SaveText.Text = saveDirectory;
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

    private void RenderTranscriptView()
    {
        if (_session is null || _context is null)
        {
            return;
        }

        _viewMode = StoryViewMode.Transcript;
        WorldState world = _session.World;

        NarrationHeading.Text = _context.Resumed && _transcriptTurns.Count > 0
            ? $"{_context.Pack.Name} transcript"
            : $"{_context.Pack.Name} opening";
        TurnText.Text = $"Turn {world.TurnNumber}";

        OpeningText.Text = _context.Resumed && _transcriptTurns.Count > 0
            ? $"The story so far, last {_transcriptTurns.Count} turn(s):"
            : OpeningScene(world);

        ScenarioText.Text = string.IsNullOrWhiteSpace(_context.Pack.Scenario)
            ? "No standing scenario is authored for this pack."
            : $"Scenario: {EntityReferences.Resolve(_context.Pack.Scenario, world)}";

        ResetStoryItems();

        foreach (TurnRecord turn in _transcriptTurns)
        {
            AddStoryTurn(turn);
        }
    }

    private void RenderDebugView(StoryViewMode mode, string heading, string text, string note)
    {
        if (_session is null)
        {
            return;
        }

        _viewMode = mode;
        NarrationHeading.Text = heading;
        TurnText.Text = $"Turn {_session.World.TurnNumber}";
        OpeningText.Text = text;
        ScenarioText.Text = note;
        ResetStoryItems();
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
        StatusPillText.Text = "Thinking";

        try
        {
            SessionResult<TurnOutcome> result = await _session.TakeTurnAsync(input);

            if (result.WasRefused)
            {
                SpikeStatusText.Text = $"Not now: {result.RefusedBecause}";
                return;
            }

            TurnOutcome outcome = result.Value!;
            _transcriptTurns.Add(outcome.Turn);
            RenderTranscriptView();

            if (outcome.ExtractionFailed)
            {
                SpikeStatusText.Text =
                    $"Turn {outcome.Turn.TurnNumber} narrated, but extraction failed: {outcome.ExtractionError}";
                StatusPillText.Text = "Check extraction";
            }
            else
            {
                SpikeStatusText.Text =
                    $"Turn {outcome.Turn.TurnNumber}: {outcome.Turn.Applied.Count} applied, " +
                    $"{outcome.Turn.NoOps.Count} no-op, {outcome.Turn.Rejected.Count} rejected.";
                StatusPillText.Text = outcome.Turn.Rejected.Count == 0 ? "Ready" : "Check turn";
            }

            TurnText.Text = $"Turn {_session.World.TurnNumber}";
            RenderExplorer(_session.World);
        }
        catch (Exception ex)
        {
            SpikeStatusText.Text = $"Turn failed: {ex.Message}";
            StatusPillText.Text = "Turn failed";
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
            RenderCurrentStoryView();

            if (report.NothingOnDisk)
            {
                SpikeStatusText.Text = "Nothing saved yet; canon is only in this session.";
                StatusPillText.Text = "Ready";
            }
            else if (report.Unchanged)
            {
                SpikeStatusText.Text = "Canon on disk matches this session.";
                StatusPillText.Text = "Ready";
            }
            else
            {
                SpikeStatusText.Text =
                    $"Re-read canon: {report.Changes.Count} change(s), {report.Warnings.Count} warning(s).";
                StatusPillText.Text = report.Warnings.Count == 0 ? "Updated" : "Check canon";
            }

            TurnText.Text = $"Turn {_session.World.TurnNumber}";
        }
        catch (Exception ex)
        {
            SpikeStatusText.Text = $"Refresh failed: {ex.Message}";
            StatusPillText.Text = "Refresh failed";
        }
        finally
        {
            SetInteractive(_session is not null);
        }
    }

    private void StateButton_Click(object sender, RoutedEventArgs e)
    {
        RenderStateView();
    }

    private void ProseButton_Click(object sender, RoutedEventArgs e)
    {
        RenderProseView();
    }

    private void TranscriptButton_Click(object sender, RoutedEventArgs e)
    {
        RenderTranscriptView();
    }

    private void RenderCurrentStoryView()
    {
        switch (_viewMode)
        {
            case StoryViewMode.State:
                RenderStateView();
                break;
            case StoryViewMode.Prose:
                RenderProseView();
                break;
            default:
                RenderTranscriptView();
                break;
        }
    }

    private void RenderStateView()
    {
        if (_session is null || _context is null)
        {
            return;
        }

        RenderDebugView(
            StoryViewMode.State,
            "State",
            ContextAssembler.ForExtraction(_session.World, _context.Pack.Lore, _context.Pack.Sheets),
            "Extractor state view.");
    }

    private void RenderProseView()
    {
        if (_session is null || _context is null)
        {
            return;
        }

        string note = string.IsNullOrWhiteSpace(_context.Pack.Scenario)
            ? "Narrator prose view."
            : $"Scenario: {EntityReferences.Resolve(_context.Pack.Scenario, _session.World)}";

        RenderDebugView(
            StoryViewMode.Prose,
            "Prose",
            ContextAssembler.ForNarration(_session.World, _context.Pack.Lore, _context.Pack.Sheets),
            note);
    }

    private void RenderOpenRefusal(SessionOpening opening)
    {
        SetInteractive(false);
        StatusPillText.Text = "Refused";
        NarrationHeading.Text = "Cannot open session";
        TurnText.Text = "Turn --";
        OpeningText.Text = opening.RefusedBecause ?? "The save could not be opened.";
        ScenarioText.Text = opening.HeldBy is null ? string.Empty : $"Held by: {opening.HeldBy}";
        SaveText.Text = $"Save: {SaveId}";
        SpikeStatusText.Text = "Close the other StoryWeaver session, or clear the stale lock intentionally.";
    }

    private void RenderNeedsPlayer(SessionOpening opening)
    {
        SetInteractive(false);
        StatusPillText.Text = "Needs player";
        NarrationHeading.Text = "Player setup required";
        TurnText.Text = "Turn 0";
        OpeningText.Text =
            "This pack does not author the player yet. The Uno spike has not built the player setup dialog.";
        ScenarioText.Text = opening.Context?.Pack.Name ?? string.Empty;
        SaveText.Text = $"Save: {SaveId}";
        SpikeStatusText.Text = "Use the CLI for this pack until the player creation dialog is added.";
    }

    private void RenderFailure(string title, string detail)
    {
        SetInteractive(false);
        StatusPillText.Text = "Failed";
        NarrationHeading.Text = title;
        TurnText.Text = "Turn --";
        OpeningText.Text = detail;
        ScenarioText.Text = string.Empty;
        SaveText.Text = "Save --";
        SpikeStatusText.Text = "Fix the issue and restart the Uno shell.";
    }

    private void SetInteractive(bool enabled)
    {
        PlayerInputBox.IsEnabled = enabled;
        SendButton.IsEnabled = enabled;
        RefreshButton.IsEnabled = enabled;
        TranscriptButton.IsEnabled = enabled;
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
