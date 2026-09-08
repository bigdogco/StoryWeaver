using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StoryWeaver.App;
using StoryWeaver.Desktop.Services;
using StoryWeaver.Desktop.ViewModels;
using StoryWeaver.Llm.Configuration;

namespace StoryWeaver.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private readonly PreferencesStore _preferences;
    private TextBox? _editor;
    private bool _closing;
    private bool _opening;
    private string? _workspacePath;
    private IReadOnlyList<ViewRecentPlaythrough> _recentPlaythroughs;
    private StoryWeaver.Core.StorySession? _session;

    // Avalonia's runtime XAML loader and designers require a public default constructor.
    // Production startup injects the preferences loaded by DesktopApplication instead.
    public MainWindow() : this(new PlayShellViewModel(new ViewPreferences()), new PreferencesStore()) { }

    public MainWindow(PlayShellViewModel model, PreferencesStore preferences)
    {
        Model = model;
        _preferences = preferences;
        _workspacePath = model.InitialWorkspacePath;
        _recentPlaythroughs = model.InitialRecentPlaythroughs;
        ExitCommand = new(Close);
        ResetSplitCommand = new(() => { Model.StoryFraction = 0.52; ApplyPanelLayout(); SavePreferences(); });
        PreviewCommand = new(Model.LoadPreview);
        SettingsCommand = new(OpenSettings);
        AboutCommand = new(() => ShowInformation("About StoryWeaver",
            "StoryWeaver — an LLM-driven text RPG.\n\nChoose a workspace in Library to open a world and playthrough. Live turns and authoring are the next desktop work.\n\nThe preview scene is illustrative; no save is opened and no model calls are made."));
        ShortcutsCommand = new(() => ShowInformation("Keyboard shortcuts",
            "Alt+F / E / V / S / H — menus\nCtrl+Shift+W — show/hide world panel\nF1 — this guide\nTab / Shift+Tab — move focus\nLeft / Right on the divider — resize panes\n\nIn a text field:\nCtrl+Z / Ctrl+Y — Undo / Redo\nCtrl+X / Ctrl+C / Ctrl+V — Cut / Copy / Paste\nCtrl+A — Select All\nEnter — insert a new line\n\nUndo affects text only, never a story turn."));
        UndoCommand = new(() => Edit(t => t.Undo()), () => _editor?.CanUndo == true);
        RedoCommand = new(() => Edit(t => t.Redo()), () => _editor?.CanRedo == true);
        CutCommand = new(() => Edit(t => t.Cut()), () => _editor is { IsReadOnly: false } t && t.SelectionStart != t.SelectionEnd);
        CopyCommand = new(() => Edit(t => t.Copy()), () => _editor is { } t && t.SelectionStart != t.SelectionEnd);
        PasteCommand = new(() => Edit(t => t.Paste()), () => _editor is { IsReadOnly: false });
        SelectAllCommand = new(() => Edit(t => t.SelectAll()), () => _editor is not null);
        InitializeComponent();
        WindowPlacementStore.Attach(this, "main", message => Model.Notice = message);
        DataContext = model;
        DraftEditor.GotFocus += (_, _) => _editor = DraftEditor;
        KeyBindings.Add(new KeyBinding { Gesture = new(Key.W, KeyModifiers.Control | KeyModifiers.Shift), Command = Model.ToggleWorldCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new(Key.F1), Command = ShortcutsCommand });
        Model.PropertyChanged += ModelChanged;
        Closing += SaveBeforeClosing;
        Closed += (_, _) => { Model.PropertyChanged -= ModelChanged; ReleaseActiveSession(); };
        ApplyPanelLayout();
        ApplyTheme();
    }

    public PlayShellViewModel Model { get; }
    public UiCommand ExitCommand { get; }
    public UiCommand ResetSplitCommand { get; }
    public UiCommand PreviewCommand { get; }
    public UiCommand SettingsCommand { get; }
    public UiCommand AboutCommand { get; }
    public UiCommand ShortcutsCommand { get; }
    public UiCommand UndoCommand { get; }
    public UiCommand RedoCommand { get; }
    public UiCommand CutCommand { get; }
    public UiCommand CopyCommand { get; }
    public UiCommand PasteCommand { get; }
    public UiCommand SelectAllCommand { get; }

    private void Edit(Action<TextBox> action)
    {
        if (_editor is not { } editor) return;
        editor.Focus();
        action(editor);
    }

    private void EditMenuOpened(object? sender, RoutedEventArgs e)
    {
        foreach (var command in new[] { UndoCommand, RedoCommand, CutCommand, CopyCommand, PasteCommand, SelectAllCommand })
            command.Refresh();
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Model.ShowWorldPanel))
        {
            // Capture keyboard resizing too, before collapsing the information column.
            if (!Model.ShowWorldPanel) CaptureSplit();
            ApplyPanelLayout();
        }
        if (e.PropertyName == nameof(Model.Theme)) ApplyTheme();
        if (e.PropertyName is nameof(Model.ShowWorldPanel) or nameof(Model.Theme) or nameof(Model.NarrationSize))
            SavePreferences();
    }

    private void ApplyPanelLayout()
    {
        PlayLayout.ColumnDefinitions[0].Width = new GridLength(Model.StoryFraction, GridUnitType.Star);
        PlayLayout.ColumnDefinitions[1].Width = new GridLength(Model.ShowWorldPanel ? 7 : 0);
        PlayLayout.ColumnDefinitions[2].MinWidth = Model.ShowWorldPanel ? 310 : 0;
        PlayLayout.ColumnDefinitions[2].Width = Model.ShowWorldPanel
            ? new GridLength(1 - Model.StoryFraction, GridUnitType.Star) : new GridLength(0);
    }

    private void CaptureSplit()
    {
        var story = PlayLayout.ColumnDefinitions[0].ActualWidth;
        var world = PlayLayout.ColumnDefinitions[2].ActualWidth;
        if (story > 0 && world > 0) Model.StoryFraction = story / (story + world);
    }

    private void SplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        CaptureSplit();
        SavePreferences();
    }

    private void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant = Model.Theme switch
            {
                "Light" => ThemeVariant.Light, "Dark" => ThemeVariant.Dark, _ => ThemeVariant.Default
            };
    }

    private void SavePreferences()
    {
        if (_preferences.Save(Model.CapturePreferences(_workspacePath) with { RecentPlaythroughs = _recentPlaythroughs }) is { } warning)
            Model.Notice = warning;
    }

    private async void SaveBeforeClosing(object? sender, WindowClosingEventArgs e)
    {
        if (Model.IsBusy) { e.Cancel = true; Model.Notice = "Please wait for the current turn to finish before closing."; return; }
        if (_opening) { e.Cancel = true; Model.Notice = "Please wait for the playthrough to finish opening."; return; }
        if (_closing) return;
        CaptureSplit();
        if (_preferences.Save(Model.CapturePreferences(_workspacePath)) is not { } warning) return;
        e.Cancel = true;
        var dialog = Dialog("View preferences were not saved");
        var close = new Button { Content = "Close anyway", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 20,
            Children = { new TextBlock { Text = warning }, close }
        };
        if (await dialog.ShowDialog<bool>(this))
        {
            _closing = true;
            Close();
        }
    }

    private async void OpenSettings()
    {
        var theme = new ComboBox { ItemsSource = new[] { "System", "Light", "Dark" }, SelectedItem = Model.Theme };
        var size = new Slider { Minimum = 14, Maximum = 28, TickFrequency = 2, IsSnapToTickEnabled = true, Value = Model.NarrationSize };
        var sizeLabel = new TextBlock { Text = $"Narration size: {Model.NarrationSize:0}" };
        var dialog = Dialog("View settings");
        var close = new Button { Content = "Done", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        theme.SelectionChanged += (_, _) => { if (theme.SelectedItem is string value) Model.Theme = value; };
        size.PropertyChanged += (_, e) =>
        {
            if (e.Property == RangeBase.ValueProperty)
            {
                Model.NarrationSize = size.Value;
                sizeLabel.Text = $"Narration size: {Model.NarrationSize:0}";
            }
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 14,
            Children = { new TextBlock { Text = "Appearance" }, theme, sizeLabel, size,
                new TextBlock { Text = "These preferences apply to the desktop view. Provider and model settings will be connected with sessions." }, close }
        };
        await dialog.ShowDialog(this);
    }

    private async void ShowInformation(string title, string text)
    {
        var dialog = Dialog(title);
        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => dialog.Close();
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 20,
            Children = { new TextBlock { Text = text }, close }
        };
        await dialog.ShowDialog(this);
    }

    private static Window Dialog(string title) => new()
    {
        Title = title, Width = 470, SizeToContent = SizeToContent.Height, CanResize = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
    };

    private async void OpenLibrary(object? sender, RoutedEventArgs e) => await OpenLibraryAsync(LibraryMode.Library);
    private async void NewPlaythrough(object? sender, RoutedEventArgs e) => await OpenLibraryAsync(LibraryMode.Worlds);
    private async void OpenSave(object? sender, RoutedEventArgs e) => await OpenLibraryAsync(LibraryMode.Playthroughs);

    private async Task OpenLibraryAsync(LibraryMode mode)
    {
        if (Model.IsBusy) { Model.Notice = "Please wait for the current turn to finish before switching playthroughs."; return; }
        if (_opening) return;
        ReleaseActiveSession();
        var library = new LibraryWindow(_workspacePath, mode, _recentPlaythroughs);
        library.WorkspaceSelected += path => { _workspacePath = path; SavePreferences(); };
        LibrarySelection? selection = await library.ShowDialog<LibrarySelection?>(this);
        if (selection is null) return;

        _workspacePath = selection.WorkspacePath;
        SavePreferences();
        Model.Notice = "Opening playthrough…";
        _opening = true;

        try
        {
            StoryWeaverSettings settings = SettingsLoader.Load();
            SessionOpening opening = await SessionOpener.OpenAsync(
                settings, selection.PackId, selection.SaveId, force: false,
                WorkspaceLibrary.SaveRoot(selection.WorkspacePath), WorkspaceLibrary.PackRoot(selection.WorkspacePath));

            if (opening.WasRefused)
            {
                Model.Notice = opening.HeldBy is { Length: > 0 } heldBy
                    ? $"Could not open: {opening.RefusedBecause}. Held by {heldBy}."
                    : $"Could not open: {opening.RefusedBecause}.";
                return;
            }

            StoryWeaver.Core.StorySession? session = opening.Session;
            if (opening.IsWaitingForPlayer)
            {
                session = await CompletePlayerAsync(opening.NeedsPlayer!);
                if (session is null)
                {
                    Model.Notice = "Player creation was cancelled. No playthrough was opened.";
                    return;
                }
            }

            _session = session;
            Model.AttachSession(session!, opening.Context!);
            try
            {
                await Model.LoadTranscriptAsync(session!, opening.Context!);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => NarrationScroll.ScrollToEnd());
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                Model.Notice = "The playthrough is open, but its narration history could not be read: " + error.Message;
                return;
            }
            Model.Notice = opening.Context!.PackHasMoved
                ? $"Opened {session!.SaveId}. This save began with pack version {opening.Context.PackVersionAtStart}."
                : $"Opened {session!.SaveId}. Ready for your next action.";
            RememberRecent(selection);
        }
        catch (SettingsException error)
        {
            Model.Notice = "Could not open because settings need attention: " + error.Message;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Model.Notice = "Could not open playthrough: " + error.Message;
        }
        finally { _opening = false; }
    }

    private async Task<StoryWeaver.Core.StorySession?> CompletePlayerAsync(PendingPlayer pending)
    {
        PlayerAnswer? answer;
        try
        {
            answer = await AskForPlayerAsync(pending);
            if (answer is null) return null;
            return await pending.CompleteAsync(answer.Name, answer.Description);
        }
        catch (ArgumentException error)
        {
            Model.Notice = error.Message;
            return null;
        }
        finally
        {
            pending.Dispose();
        }
    }

    private async Task<PlayerAnswer?> AskForPlayerAsync(PendingPlayer pending)
    {
        var name = new TextBox { PlaceholderText = "Name", Text = pending.Player?.Name ?? string.Empty };
        var description = new TextBox
        {
            PlaceholderText = "Optional description",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 90,
        };
        var validation = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var dialog = Dialog("Who are you?");
        var cancel = new Button { Content = "Cancel" };
        var begin = new Button { Content = "Begin", HorizontalAlignment = HorizontalAlignment.Right };
        cancel.Click += (_, _) => dialog.Close(null);
        begin.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                validation.Text = "The player needs a name.";
                return;
            }
            dialog.Close(new PlayerAnswer(name.Text.Trim(), string.IsNullOrWhiteSpace(description.Text) ? null : description.Text.Trim()));
        };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 12,
            Children =
            {
                new TextBlock { Text = "This world does not author its protagonist. Choose who you are for this playthrough.", TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = "Name" }, name,
                new TextBlock { Text = "Description (optional — leave blank to keep the seeded description)" }, description,
                validation,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, begin } },
            },
        };
        return await dialog.ShowDialog<PlayerAnswer?>(this);
    }

    private void ClosePlaythrough(object? sender, RoutedEventArgs e)
    {
        if (Model.IsBusy) { Model.Notice = "Please wait for the current turn to finish before closing the playthrough."; return; }
        if (_opening) return;
        ReleaseActiveSession();
        Model.Notice = "Playthrough closed.";
    }

    private void ReleaseActiveSession()
    {
        _session?.Dispose();
        _session = null;
        Model.DetachSession();
    }

    private void RememberRecent(LibrarySelection selection)
    {
        var opened = new ViewRecentPlaythrough(selection.WorkspacePath, selection.PackId, selection.SaveId, DateTime.UtcNow);
        _recentPlaythroughs = new ViewPreferences { RecentPlaythroughs = [opened, .. _recentPlaythroughs] }
            .Normalized()
            .RecentPlaythroughs;
        SavePreferences();
    }

    private async void SendTurn(object? sender, RoutedEventArgs e)
    {
        if (_opening || _session is null) return;
        var sending = Model.SendAsync(_session);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => NarrationScroll.ScrollToEnd());
        await sending;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => NarrationScroll.ScrollToEnd());
    }

    private sealed record PlayerAnswer(string Name, string? Description);
}
