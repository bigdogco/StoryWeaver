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
    private bool _isBusy;
    private bool _needsReopen;
    private string _busyStatus = "Writing narration and updating the world…";
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (Set(ref _isBusy, value)) { Raise(nameof(CanSend)); Raise(nameof(CanInspectCanon)); Raise(nameof(CanReviseLastTurn)); Raise(nameof(ComposerStatus)); RefreshEditAvailability(); } }
    }
    public bool CanSend => HasLiveSession && !IsBusy && !_needsReopen && !string.IsNullOrWhiteSpace(Draft);
    public bool CanInspectCanon => HasLiveSession && !IsBusy;
    public bool CanEditCanon => CanInspectCanon && !_needsReopen;
    private void RefreshEditAvailability()
    {
        foreach (var tab in Tabs) tab.CanEdit = CanEditCanon;
        Raise(nameof(CanEditCanon));
    }
    public bool CanReviseLastTurn => HasLiveSession && !IsBusy && !_needsReopen
        && Narration.Any(paragraph => paragraph.Label.StartsWith("NARRATION · TURN ", StringComparison.Ordinal));
    public string ComposerStatus => IsBusy ? _busyStatus
        : _needsReopen ? "Reopen this playthrough before sending another turn."
        : HasLiveSession ? "Send takes a turn and saves the result." : "Open a playthrough to send an action.";
    private string _sceneTitle = "StoryWeaver";
    private string _sessionStatus = "No active session · choose Library to begin";
    private int _selectedTab;
    private bool _showWorldPanel;
    private double _narrationSize;
    private string _theme;
    private readonly string? _initialWorkspacePath;
    private readonly IReadOnlyList<ViewRecentPlaythrough> _initialRecentPlaythroughs;

    public PlayShellViewModel(ViewPreferences preferences)
    {
        preferences = preferences.Normalized();
        _showWorldPanel = preferences.ShowWorldPanel;
        _narrationSize = preferences.NarrationSize;
        _theme = preferences.Theme;
        _initialWorkspacePath = preferences.WorkspacePath;
        _initialRecentPlaythroughs = preferences.RecentPlaythroughs;
        StoryFraction = preferences.StoryFraction;
        ToggleWorldCommand = new(() => ShowWorldPanel = !ShowWorldPanel);
        LargerTextCommand = new(() => NarrationSize += 2, () => NarrationSize < 28);
        SmallerTextCommand = new(() => NarrationSize -= 2, () => NarrationSize > 14);
        ResetTextCommand = new(() => NarrationSize = 18);
        DismissNoticeCommand = new(() => Notice = string.Empty);
        Narration.CollectionChanged += (_, _) => Raise(nameof(CanReviseLastTurn));
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
    public string Draft { get => _draft; set { if (Set(ref _draft, value)) Raise(nameof(CanSend)); } }
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
    public IReadOnlyList<ViewRecentPlaythrough> InitialRecentPlaythroughs => _initialRecentPlaythroughs;
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
        RefreshEditAvailability();
        _sceneTitle = "Marrow · The Drowned Crow";
        _sessionStatus = "Preview scene · illustrative data · no active session";
        Raise(nameof(IsPreview));
        Raise(nameof(IsEmpty));
        Raise(nameof(HasLiveSession));
        Raise(nameof(CanInspectCanon));
        Raise(nameof(SceneTitle));
        Raise(nameof(SessionStatus));
    }

    public void AttachSession(StorySession session, SessionContext context)
    {
        _needsReopen = false;
        Characters.Replace(WorldPresentation.Characters(session.World));
        Locations.Replace(WorldPresentation.Locations(session.World));
        Canon.Replace(WorldPresentation.Facts(session.World));
        Items.Replace(WorldPresentation.Items(session.World));
        Narration.Clear();
        _preview = false;
        _hasLiveSession = true;
        RefreshEditAvailability();
        _sceneTitle = context.Pack.Manifest?.Name is { Length: > 0 } name ? name : context.Pack.Id;
        _sessionStatus = context.Resumed
            ? $"Live save · {session.SaveId} · resumed at turn {context.TurnNumber}"
            : $"Live save · {session.SaveId} · ready to begin";
        Raise(nameof(IsPreview));
        Raise(nameof(IsEmpty));
        Raise(nameof(HasLiveSession));
        Raise(nameof(CanInspectCanon));
        Raise(nameof(SceneTitle));
        Raise(nameof(SessionStatus));
        Raise(nameof(CanSend));
        Raise(nameof(ComposerStatus));
    }

    public void DetachSession()
    {
        if (!_hasLiveSession) return;
        foreach (EntityTabViewModel tab in Tabs) tab.Replace([]);
        Narration.Clear();
        _hasLiveSession = false;
        _needsReopen = false;
        RefreshEditAvailability();
        _sceneTitle = "StoryWeaver";
        _sessionStatus = "No active session · choose Library to begin";
        Raise(nameof(IsEmpty));
        Raise(nameof(HasLiveSession));
        Raise(nameof(CanInspectCanon));
        Raise(nameof(SceneTitle));
        Raise(nameof(SessionStatus));
        Raise(nameof(CanSend));
        Raise(nameof(ComposerStatus));
    }

    public async Task SendAsync(StorySession session)
    {
        if (!CanSend) return;
        string input = Draft.Trim();
        string originalDraft = Draft;
        int before = session.World.TurnNumber;
        _busyStatus = "Writing narration and updating the world…";
        IsBusy = true;
        Notice = string.Empty;
        var pending = new NarrativeParagraph("YOU · SENDING", [new(input)]);
        Narration.Add(pending);
        Draft = string.Empty;
        bool completed = false;
        try
        {
            var result = await session.TakeTurnAsync(input);
            if (result.WasRefused) { Notice = result.RefusedBecause!; return; }
            var outcome = result.Value!;
            Narration[Narration.IndexOf(pending)] = new($"YOU · TURN {outcome.Turn.TurnNumber}", [new(outcome.Turn.PlayerInput)]);
            completed = true;
            Narration.Add(new($"NARRATION · TURN {outcome.Turn.TurnNumber}", [new(outcome.Turn.Narration)]));
            RefreshWorld(session);
            LinkNarrationNames();
            if (Draft == originalDraft) Draft = string.Empty;
            Notice = outcome.ExtractionFailed
                ? "Narration was saved, but the world update failed: " + outcome.ExtractionError
                : $"Turn {outcome.Turn.TurnNumber} saved · {outcome.Turn.Applied.Count} world changes"
                    + (outcome.Turn.Rejected.Count > 0 ? $" · {outcome.Turn.Rejected.Count} rejected changes: "
                        + string.Join("; ", outcome.Turn.Rejected.Select(rejected => rejected.Reason)) : string.Empty);
        }
        catch (Exception error)
        {
            // A persistence failure can happen after canon changes. Do not silently retry
            // the action against that partially committed in-memory state.
            _needsReopen = session.World.TurnNumber != before;
            if (_needsReopen) RefreshWorld(session);
            Notice = _needsReopen
                ? "The turn changed the world but did not finish saving. Your draft is retained. Reopen the playthrough and inspect its state before continuing. " + error.Message
                : "The turn could not complete. Your draft is retained. " + error.Message;
        }
        finally
        {
            if (!completed)
            {
                Narration.Remove(pending);
                Draft = originalDraft;
            }
            IsBusy = false;
        }
    }

    public Task<SessionResult<EditReport>> SaveCanonEditAsync(StorySession session, CanonEditSnapshot baseline, CanonFields fields) =>
        SaveCanonActionAsync(session, () => session.EditAsync(baseline, fields), report => report, "Changes saved");

    public Task<SessionResult<EditReport>> CreateCanonAsync(StorySession session, CanonCreationSnapshot baseline, string id, CanonFields fields) =>
        SaveCanonActionAsync(session, () => session.CreateCanonAsync(baseline, id, fields), report => report, "Added");

    public Task<SessionResult<CanonRemovalOutcome>> RemoveCanonAsync(StorySession session, CanonRemovalPlan plan) =>
        SaveCanonActionAsync(session, () => session.RemoveCanonAsync(plan), outcome => outcome.Report, "Removed");

    private async Task<SessionResult<T>> SaveCanonActionAsync<T>(StorySession session, Func<Task<SessionResult<T>>> action,
        Func<T, EditReport?> reportOf, string success) where T : class
    {
        if (!CanEditCanon) return SessionResult<T>.Refused("Editing is unavailable. Wait for the current operation or reopen the playthrough.");
        _busyStatus = "Saving canon corrections…";
        IsBusy = true;
        try
        {
            var result = await action();
            if (result.WasRefused) return result;
            var report = reportOf(result.Value!);
            if (report is null) return result;
            RefreshWorld(session);
            LinkNarrationNames();
            Notice = report.IsClean ? success + "." : success + " with integrity warnings.";
            return result;
        }
        catch
        {
            _needsReopen = true;
            Notice = "The correction did not complete. Reopen this playthrough before making further changes.";
            throw;
        }
        finally { IsBusy = false; }
    }

    public async Task<string?> ReviseLastTurnAsync(StorySession session, bool reroll)
    {
        if (!CanReviseLastTurn) return null;
        string action = reroll ? "Reroll" : "Retry extraction";
        _busyStatus = reroll ? "Rewriting the last narration and updating the world…"
            : "Retrying extraction on the last narration…";
        IsBusy = true;
        Notice = string.Empty;
        try
        {
            var result = reroll ? await session.RerollLastAsync() : await session.ReExtractLastAsync();
            if (result.WasRefused)
            {
                Notice = $"{action} could not run: {result.RefusedBecause}";
                return Notice;
            }
            var outcome = result.Value!;
            var turn = outcome.Turn;
            ReplaceTurnParagraph($"YOU · TURN {turn.TurnNumber}", turn.PlayerInput);
            ReplaceTurnParagraph($"NARRATION · TURN {turn.TurnNumber}", turn.Narration);
            RefreshWorld(session);
            LinkNarrationNames();
            Notice = outcome.ExtractionFailed
                ? $"{action}: narration saved, but extraction failed."
                : $"{action} saved for turn {turn.TurnNumber} · {turn.Rejected.Count} rejected changes.";
            return $"Playthrough: {session.SaveId}\nTurn: {turn.TurnNumber}\n\n"
                + (reroll ? "The last narration was replaced. No new turn was added."
                    : "Extraction was retried. Narration and the turn number are unchanged.")
                + $"\n\nRecorded applied changes for this turn (including earlier extraction attempts): {turn.Applied.Count}"
                + $"\nLatest extraction: {turn.NoOps.Count} already-true changes, {turn.Rejected.Count} rejected changes."
                + (outcome.ExtractionFailed ? "\n\nExtraction failed\n" + outcome.ExtractionError : string.Empty)
                + (turn.Rejected.Count > 0 ? "\n\nRejected changes\n"
                    + string.Join("\n", turn.Rejected.Select(rejected => "• " + rejected.Reason)) : string.Empty);
        }
        catch (Exception error)
        {
            // These operations can save canon and then fail writing history without
            // advancing the turn counter. An exception supplies no completion outcome.
            _needsReopen = true;
            Notice = $"{action} did not complete. Reopen the playthrough and inspect its state before continuing.";
            return Notice + "\n\nThe previous narration and your draft are retained on screen."
                + " Saving may have been incomplete.\n\n" + error.Message;
        }
        finally { IsBusy = false; }
    }

    private void ReplaceTurnParagraph(string label, string text)
    {
        for (int index = 0; index < Narration.Count; index++)
        {
            if (Narration[index].Label != label) continue;
            Narration[index] = new(label, [new(text)]);
            return;
        }
        Narration.Add(new(label, [new(text)]));
    }

    public async Task<string?> InspectCanonAsync(StorySession session, bool reload)
    {
        if (!CanInspectCanon) return null;
        _busyStatus = reload ? "Reloading canon from disk…" : "Checking current canon…";
        IsBusy = true;
        string title = reload ? "Update State" : "Check Canon";
        bool adopted = false;
        try
        {
            IReadOnlyList<string> warnings;
            string summary;
            string changes = string.Empty;
            if (reload)
            {
                var result = await session.UpdateStateAsync();
                if (result.WasRefused)
                {
                    Notice = $"{title} could not run: {result.RefusedBecause}";
                    return Notice;
                }
                var report = result.Value!;
                if (report.NothingOnDisk)
                {
                    Notice = "No canon save was found on disk. The current world was kept.";
                    return Notice;
                }
                adopted = true;
                RefreshWorld(session);
                LinkNarrationNames();
                warnings = report.Warnings;
                summary = report.Unchanged ? "Canon reloaded. No changes found." : $"Canon reloaded · {report.Changes.Count} changes.";
                if (report.Changes.Count > 0)
                    changes = "\n\nChanges\n" + string.Join("\n", report.Changes.Select(change => "• " + change));
            }
            else
            {
                var result = await session.CheckCanonAsync();
                if (result.WasRefused)
                {
                    Notice = $"{title} could not run: {result.RefusedBecause}";
                    return Notice;
                }
                warnings = result.Value!.Warnings;
                summary = "Checked the current in-memory canon. Nothing was reloaded or written.";
            }
            string findings = warnings.Count == 0 ? "No integrity warnings found."
                : $"{warnings.Count} integrity warnings\n" + string.Join("\n", warnings.Select(warning => "• " + warning));
            Notice = $"{title} complete · {warnings.Count} integrity warnings.";
            return $"Playthrough: {session.SaveId}\n\n{summary}{changes}\n\n{findings}"
                + (_needsReopen ? "\n\nThe earlier save failure still requires reopening this playthrough before sending a turn." : string.Empty);
        }
        catch (Exception error)
        {
            Notice = adopted
                ? "Canon was reloaded, but the desktop could not refresh. Reopen the playthrough."
                : $"{title} failed. The current world was kept.";
            if (adopted) _needsReopen = true;
            return Notice + "\n\n" + error.Message;
        }
        finally { IsBusy = false; }
    }

    private void RefreshWorld(StorySession session)
    {
        Characters.Replace(WorldPresentation.Characters(session.World));
        Locations.Replace(WorldPresentation.Locations(session.World));
        Canon.Replace(WorldPresentation.Facts(session.World));
        Items.Replace(WorldPresentation.Items(session.World));
        _sessionStatus = $"Live save · {session.SaveId} · turn {session.World.TurnNumber}";
        Raise(nameof(SessionStatus));
    }

    public async Task LoadTranscriptAsync(StorySession session, SessionContext context)
    {
        Narration.Clear();
        AddOpening(session, context);

        if (context.Resumed)
        {
            var turns = await session.RecentTurnsAsync(context.HistoryTurns);
            foreach (var turn in turns)
            {
                Narration.Add(new($"YOU · TURN {turn.TurnNumber}", [new(turn.PlayerInput)]));
                Narration.Add(new($"NARRATION · TURN {turn.TurnNumber}", [new(turn.Narration)]));
            }
            if (turns.Count > 0) { LinkNarrationNames(); return; }
            if (session.World.TurnNumber > 0)
            {
                Narration.Add(new("HISTORY", [new("No recent narration is available for this playthrough.")]));
                LinkNarrationNames();
                return;
            }
        }

        LinkNarrationNames();
    }

    private void AddOpening(StorySession session, SessionContext context)
    {
        string? opening = context.Pack.HasOpening
            ? EntityReferences.Resolve(context.Pack.Opening, session.World)
            : session.World.PlayerLocationId is { } id ? session.World.FindLocation(id)?.Description : null;
        Narration.Add(new("OPENING", [new(string.IsNullOrWhiteSpace(opening)
            ? "This world has no opening scene. Your playthrough is ready."
            : System.Text.RegularExpressions.Regex.Replace(opening.Replace("\r\n", "\n"), @"(?<!\n)\n(?!\n)", " "))]));
    }

    private void LinkNarrationNames()
    {
        var entities = Tabs.SelectMany(tab => tab.Entities).ToList();
        for (int index = 0; index < Narration.Count; index++)
        {
            var paragraph = Narration[index];
            if (paragraph.Label != "OPENING" && !paragraph.Label.StartsWith("NARRATION ·", StringComparison.Ordinal)) continue;
            string text = string.Concat(paragraph.Spans.Select(span => span.Text));
            Narration[index] = paragraph with { Spans = NarrationNameLinks.Create(text, entities) };
        }
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
