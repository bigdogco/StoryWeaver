using System.Collections.ObjectModel;
using StoryWeaver.App;
using StoryWeaver.Core;
using StoryWeaver.Desktop.Presentation;
using StoryWeaver.Desktop.Preview;
using StoryWeaver.Desktop.Services;

namespace StoryWeaver.Desktop.ViewModels;

public sealed class PlayShellViewModel : ObservableObject
{
    private string _draft = string.Empty;
    private string _notice = string.Empty;
    private bool _preview;
    private bool _hasLiveSession;
    private string _sceneTitle = "StoryWeaver";
    private string _sessionStatus = "No active session · choose Library to begin";
    private int _selectedTab;
    private bool _showWorldPanel;
    private double _narrationSize;
    private string _theme;
    private readonly string? _initialWorkspacePath;

    public PlayShellViewModel(ViewPreferences preferences)
    {
        preferences = preferences.Normalized();
        _showWorldPanel = preferences.ShowWorldPanel;
        _narrationSize = preferences.NarrationSize;
        _theme = preferences.Theme;
        _initialWorkspacePath = preferences.WorkspacePath;
        StoryFraction = preferences.StoryFraction;
        ToggleWorldCommand = new(() => ShowWorldPanel = !ShowWorldPanel);
        LargerTextCommand = new(() => NarrationSize += 2, () => NarrationSize < 28);
        SmallerTextCommand = new(() => NarrationSize -= 2, () => NarrationSize > 14);
        ResetTextCommand = new(() => NarrationSize = 18);
        DismissNoticeCommand = new(() => Notice = string.Empty);
    }

    public EntityTabViewModel Characters { get; } = new("Characters", EntityKind.Character);
    public EntityTabViewModel Locations { get; } = new("Locations", EntityKind.Location);
    public EntityTabViewModel Canon { get; } = new("Canon", EntityKind.Fact);
    public EntityTabViewModel Items { get; } = new("Items", EntityKind.Item);
    public IReadOnlyList<EntityTabViewModel> Tabs => [Characters, Locations, Canon, Items];
    public ObservableCollection<NarrativeParagraph> Narration { get; } = [];
    public UiCommand ToggleWorldCommand { get; }
    public UiCommand LargerTextCommand { get; }
    public UiCommand SmallerTextCommand { get; }
    public UiCommand ResetTextCommand { get; }
    public UiCommand DismissNoticeCommand { get; }
    public double StoryFraction { get; set; }
    public string Draft { get => _draft; set => Set(ref _draft, value); }
    public string Notice
    {
        get => _notice;
        set { if (Set(ref _notice, value)) Raise(nameof(HasNotice)); }
    }
    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);
    public bool IsPreview => _preview;
    public bool IsEmpty => !_preview && !_hasLiveSession;
    public bool HasLiveSession => _hasLiveSession;
    public string? InitialWorkspacePath => _initialWorkspacePath;
    public string SceneTitle => _sceneTitle;
    public string SessionStatus => _sessionStatus;
    public int SelectedTab { get => _selectedTab; set => Set(ref _selectedTab, value); }
    public bool ShowWorldPanel { get => _showWorldPanel; set => Set(ref _showWorldPanel, value); }
    public string Theme { get => _theme; set => Set(ref _theme, value); }
    public double NarrationSize
    {
        get => _narrationSize;
        set
        {
            if (!Set(ref _narrationSize, Math.Clamp(value, 14, 28))) return;
            LargerTextCommand.Refresh();
            SmallerTextCommand.Refresh();
        }
    }

    public void LoadPreview()
    {
        foreach (var tab in Tabs) tab.Replace(PreviewScene.Entities.Where(e => e.Reference.Kind == tab.Kind));
        Narration.Clear();
        foreach (var paragraph in PreviewScene.Paragraphs) Narration.Add(paragraph);
        _preview = true;
        _hasLiveSession = false;
        _sceneTitle = "Marrow · The Drowned Crow";
        _sessionStatus = "Preview scene · illustrative data · no active session";
        Raise(nameof(IsPreview));
        Raise(nameof(IsEmpty));
        Raise(nameof(HasLiveSession));
        Raise(nameof(SceneTitle));
        Raise(nameof(SessionStatus));
    }

    public void AttachSession(StorySession session, SessionContext context)
    {
        Characters.Replace(WorldPresentation.Characters(session.World));
        Locations.Replace(WorldPresentation.Locations(session.World));
        Canon.Replace(WorldPresentation.Facts(session.World));
        Items.Replace(WorldPresentation.Items(session.World));
        Narration.Clear();
        _preview = false;
        _hasLiveSession = true;
        _sceneTitle = context.Pack.Manifest?.Name is { Length: > 0 } name ? name : context.Pack.Id;
        _sessionStatus = context.Resumed
            ? $"Live save · {session.SaveId} · resumed at turn {context.TurnNumber}"
            : $"Live save · {session.SaveId} · ready to begin";
        Raise(nameof(IsPreview));
        Raise(nameof(IsEmpty));
        Raise(nameof(HasLiveSession));
        Raise(nameof(SceneTitle));
        Raise(nameof(SessionStatus));
    }

    public void DetachSession()
    {
        if (!_hasLiveSession) return;
        foreach (EntityTabViewModel tab in Tabs) tab.Replace([]);
        Narration.Clear();
        _hasLiveSession = false;
        _sceneTitle = "StoryWeaver";
        _sessionStatus = "No active session · choose Library to begin";
        Raise(nameof(IsEmpty));
        Raise(nameof(HasLiveSession));
        Raise(nameof(SceneTitle));
        Raise(nameof(SessionStatus));
    }

    public bool CanNavigate(EntityReference reference) => Tabs.Any(tab =>
        tab.Kind == reference.Kind && tab.Entities.Any(e =>
            string.Equals(e.Reference.Id, reference.Id, StringComparison.OrdinalIgnoreCase)));

    public void Navigate(EntityReference reference)
    {
        var tab = Tabs.SingleOrDefault(t => t.Kind == reference.Kind);
        var entity = tab?.Entities.FirstOrDefault(e =>
            string.Equals(e.Reference.Id, reference.Id, StringComparison.OrdinalIgnoreCase));
        if (entity is null)
        {
            Notice = "This reference is no longer available in the world information.";
            return;
        }
        ShowWorldPanel = true;
        SelectedTab = Tabs.ToList().IndexOf(tab!);
        tab!.Selected = entity;
    }

    public ViewPreferences CapturePreferences() => new()
    {
        StoryFraction = StoryFraction, NarrationSize = NarrationSize,
        ShowWorldPanel = ShowWorldPanel, Theme = Theme, WorkspacePath = _initialWorkspacePath
    };

    public ViewPreferences CapturePreferences(string? workspacePath) => CapturePreferences() with
    {
        WorkspacePath = workspacePath
    };
}
