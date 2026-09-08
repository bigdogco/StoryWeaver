using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using StoryWeaver.Desktop.Services;

namespace StoryWeaver.Desktop.Views;

/// <summary>Separates authored worlds from saved playthroughs in the desktop Library.</summary>
public sealed class LibraryWindow : Window
{
    private static readonly Regex SaveIdPattern = new("^[A-Za-z0-9][A-Za-z0-9_-]*$", RegexOptions.Compiled);
    private readonly TextBlock _workspace = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _status = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly ListBox _worlds = new();
    private readonly ListBox _playthroughs = new();
    private readonly ComboBox _legacyWorld = new() { IsVisible = false };
    private readonly TextBlock _legacyWorldLabel = new() { Text = "Legacy save world", FontWeight = Avalonia.Media.FontWeight.SemiBold, IsVisible = false };
    private readonly TextBox _newSaveId = new() { PlaceholderText = "New save identifier" };
    private readonly Button _primary = new() { HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Grid _worldsPanel = new() { RowDefinitions = new("Auto,Auto,Auto,*,Auto,Auto") };
    private readonly Grid _playthroughsPanel = new() { RowDefinitions = new("Auto,Auto,Auto,*,Auto,Auto") };
    private WorkspaceContents? _contents;
    private LibraryMode _mode;
    private readonly ComboBox _sort = new()
    {
        ItemsSource = new[] { "Last saved: newest first", "Last saved: oldest first", "Turns: most first", "Turns: fewest first", "Name: A–Z", "Name: Z–A" },
        SelectedIndex = 0,
        MinWidth = 230,
    };
    public event Action<string>? WorkspaceSelected;

    public LibraryWindow(string? workspacePath, LibraryMode initialMode)
    {
        Title = "StoryWeaver Library";
        Width = 680; Height = 720; MinWidth = 520; MinHeight = 650;
        CanResize = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _mode = initialMode;

        _worlds.ItemTemplate = CreateTemplate<WorkspacePack>(pack => Details(pack.DisplayName, pack.Detail));
        _playthroughs.ItemTemplate = CreateTemplate<WorkspaceSave>(save => Details(save.DisplayName, save.Detail));
        _legacyWorld.ItemTemplate = new FuncDataTemplate<WorkspacePack>((pack, _) => new TextBlock { Text = pack?.DisplayName ?? string.Empty });
        ScrollViewer.SetVerticalScrollBarVisibility(_worlds, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(_playthroughs, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        _worlds.DoubleTapped += (_, e) => { if (_worlds.SelectedItem is not null) StartNewPlaythrough(); e.Handled = true; };
        _playthroughs.DoubleTapped += (_, e) => { if (_playthroughs.SelectedItem is not null) OpenPlaythrough(); e.Handled = true; };
        _playthroughs.SelectionChanged += (_, _) => SelectPlaythrough();
        _primary.Click += (_, _) => SelectPrimaryAction();
        _sort.SelectionChanged += (_, _) => SortPlaythroughs();

        var choose = new Button { Content = "Choose workspace…", HorizontalAlignment = HorizontalAlignment.Left };
        choose.Click += async (_, _) => await ChooseWorkspaceAsync();
        var worlds = new Button { Content = "Worlds" };
        worlds.Click += (_, _) => SetMode(LibraryMode.Worlds);
        var playthroughs = new Button { Content = "Playthroughs" };
        playthroughs.Click += (_, _) => SetMode(LibraryMode.Playthroughs);
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => Close(null);

        _worldsPanel.Children.AddRange([
            Heading("Worlds"), Paragraph("Choose authored content, then create a completely new playthrough."),
            Label("Available worlds"), _worlds, Label("New save identifier (optional — defaults to world and date/time)"), _newSaveId]);
        _playthroughsPanel.Children.AddRange([
            Heading("Playthroughs"), Paragraph("Choose an existing saved game to resume. Its world is recorded with the save."),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12,
                Children = { new TextBlock { Text = "Sort by", VerticalAlignment = VerticalAlignment.Center }, _sort } },
            _playthroughs, _legacyWorldLabel, _legacyWorld]);

        foreach (var panel in new[] { _worldsPanel, _playthroughsPanel })
        {
            for (int row = 0; row < panel.Children.Count; row++)
            {
                Grid.SetRow(panel.Children[row], row);
                panel.Children[row].Margin = new Thickness(0, 0, 0, 12);
            }
        }

        var header = new StackPanel
        {
            Spacing = 12, Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                Heading("Library", 26), Paragraph("Worlds are authored content. Playthroughs are your saved story state."),
                _workspace, choose,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { worlds, playthroughs } },
            },
        };
        var body = new Grid { Children = { _worldsPanel, _playthroughsPanel } };
        var footer = new StackPanel
        {
            Spacing = 12,
            Children = { _status,
                new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, _primary } } },
        };
        Grid.SetRow(body, 1);
        Grid.SetRow(footer, 2);
        Content = new Grid
        {
            Margin = new Thickness(24), RowDefinitions = new("Auto,*,Auto"),
            Children = { header, body, footer },
        };

        if (!string.IsNullOrWhiteSpace(workspacePath)) SetWorkspace(workspacePath);
        else SetStatus("Choose a workspace to see its worlds and playthroughs.");
        SetMode(initialMode);
    }

    private async Task ChooseWorkspaceAsync()
    {
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        { Title = "Choose StoryWeaver workspace", AllowMultiple = false });
        if (folders.Count == 0) return;
        string? path = folders[0].TryGetLocalPath();
        if (path is null) SetStatus("The selected folder is not available as a local path.");
        else
        {
            SetWorkspace(path);
            if (_contents is not null) WorkspaceSelected?.Invoke(_contents.RootPath);
        }
    }

    private void SetWorkspace(string path)
    {
        try
        {
            _contents = WorkspaceLibrary.Read(path);
            _workspace.Text = _contents.RootPath;
            _worlds.ItemsSource = _contents.Packs;
            SortPlaythroughs();
            _legacyWorld.ItemsSource = _contents.Packs.Where(pack => pack.CanOpen).ToList();
            _worlds.SelectedItem = _contents.Packs.FirstOrDefault(pack => pack.CanOpen);
            _playthroughs.SelectedItem = null;
            _newSaveId.Text = string.Empty;
            SetStatus(_contents.Packs.Count == 0 ? "No worlds were found in worlds." : string.Empty);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            _contents = null;
            _workspace.Text = path;
            _worlds.ItemsSource = Array.Empty<WorkspacePack>();
            _playthroughs.ItemsSource = Array.Empty<WorkspaceSave>();
            _legacyWorld.ItemsSource = Array.Empty<WorkspacePack>();
            SetStatus(error.Message);
        }
    }

    private void SortPlaythroughs()
    {
        if (_contents is null) return;
        var selected = _playthroughs.SelectedItem;
        var saves = _contents.Saves;
        var sorted = _sort.SelectedIndex switch
        {
            1 => saves.OrderBy(save => save.LastSavedUtc is null).ThenBy(save => save.LastSavedUtc).ThenBy(save => save.Id, StringComparer.OrdinalIgnoreCase),
            2 => saves.OrderBy(save => save.Turns is null).ThenByDescending(save => save.Turns).ThenBy(save => save.Id, StringComparer.OrdinalIgnoreCase),
            3 => saves.OrderBy(save => save.Turns is null).ThenBy(save => save.Turns).ThenBy(save => save.Id, StringComparer.OrdinalIgnoreCase),
            4 => saves.OrderBy(save => save.Id, StringComparer.OrdinalIgnoreCase),
            5 => saves.OrderByDescending(save => save.Id, StringComparer.OrdinalIgnoreCase),
            _ => saves.OrderBy(save => save.LastSavedUtc is null).ThenByDescending(save => save.LastSavedUtc).ThenBy(save => save.Id, StringComparer.OrdinalIgnoreCase),
        };
        _playthroughs.ItemsSource = sorted.ToList();
        _playthroughs.SelectedItem = selected;
    }

    private void SetMode(LibraryMode mode)
    {
        _mode = mode is LibraryMode.Library ? LibraryMode.Worlds : mode;
        _worldsPanel.IsVisible = _mode == LibraryMode.Worlds;
        _playthroughsPanel.IsVisible = _mode == LibraryMode.Playthroughs;
        _primary.Content = _mode == LibraryMode.Worlds ? "Start new playthrough" : "Open playthrough";
        SelectPlaythrough();
    }

    private void SelectPlaythrough()
    {
        bool legacy = _mode == LibraryMode.Playthroughs && _playthroughs.SelectedItem is WorkspaceSave { HasRecordedPack: false };
        _legacyWorld.IsVisible = legacy;
        _legacyWorldLabel.IsVisible = legacy;
        if (legacy) SetStatus("This save predates provenance. Choose the world it was created from before opening it.");
        else if (_contents is not null) SetStatus(string.Empty);
    }

    private void SelectPrimaryAction()
    {
        if (_contents is null) { SetStatus("Choose a workspace first."); return; }
        if (_mode == LibraryMode.Worlds) StartNewPlaythrough();
        else OpenPlaythrough();
    }

    private void StartNewPlaythrough()
    {
        if (_worlds.SelectedItem is not WorkspacePack { CanOpen: true } world)
        { SetStatus("Choose a readable world first."); return; }
        string saveId = _newSaveId.Text?.Trim() ?? string.Empty;
        if (saveId.Length == 0)
        {
            string prefix = $"{world.Id}-{DateTime.Now:yyyyMMdd-HHmmss}";
            saveId = prefix;
            for (int suffix = 2; Directory.Exists(Path.Combine(WorkspaceLibrary.SaveRoot(_contents!.RootPath), saveId)); suffix++)
                saveId = $"{prefix}-{suffix}";
        }
        if (!SaveIdPattern.IsMatch(saveId))
        { SetStatus("A new save identifier must start with a letter or number and use only letters, numbers, hyphens or underscores."); return; }
        if (Directory.Exists(Path.Combine(WorkspaceLibrary.SaveRoot(_contents!.RootPath), saveId)))
        { SetStatus("That save already exists. Open it from Playthroughs or choose a new identifier."); return; }
        Close(new LibrarySelection(_contents.RootPath, world.Id, saveId));
    }

    private void OpenPlaythrough()
    {
        if (_playthroughs.SelectedItem is not WorkspaceSave save)
        { SetStatus("Choose a saved playthrough first."); return; }
        WorkspacePack? world = save.HasRecordedPack
            ? _contents!.Packs.FirstOrDefault(pack => pack.CanOpen && string.Equals(pack.Id, save.PackId, StringComparison.OrdinalIgnoreCase))
            : _legacyWorld.SelectedItem as WorkspacePack;
        if (world is null)
        { SetStatus(save.HasRecordedPack ? "The world recorded by this save is not available in this workspace." : "Choose the original world for this legacy save."); return; }
        Close(new LibrarySelection(_contents!.RootPath, world.Id, save.Id));
    }

    private void SetStatus(string value) => _status.Text = value;
    private static StackPanel Details(string name, string detail) => new()
    {
        Margin = new Thickness(4, 2), Spacing = 1,
        Children = { new TextBlock { Text = name, FontSize = 14, FontWeight = Avalonia.Media.FontWeight.SemiBold },
            new TextBlock { Text = detail, FontSize = 12, Opacity = 0.8, TextWrapping = Avalonia.Media.TextWrapping.Wrap } },
    };
    private static TextBlock Heading(string text, double size = 22) => new() { Text = text, FontSize = size, FontWeight = Avalonia.Media.FontWeight.SemiBold };
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = Avalonia.Media.FontWeight.SemiBold };
    private static TextBlock Paragraph(string text, double opacity = 1) => new() { Text = text, Opacity = opacity, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private static FuncDataTemplate<T> CreateTemplate<T>(Func<T, Control> build) => new((value, _) => build((T)value!), true);
}

public enum LibraryMode { Library, Worlds, Playthroughs }
public sealed record LibrarySelection(string WorkspacePath, string PackId, string SaveId);
