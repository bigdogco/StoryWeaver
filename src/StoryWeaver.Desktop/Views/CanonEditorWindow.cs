using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using StoryWeaver.App;
using StoryWeaver.Core;

namespace StoryWeaver.Desktop.Views;

/// <summary>A detached form. World and pack are read once to build choices; saving delegates to the session.</summary>
public sealed class CanonEditorWindow : Window
{
    private readonly CanonEditSnapshot _baseline;
    private readonly Func<CanonFields, Task<SessionResult<EditReport>>> _save;
    private readonly StackPanel _fields = new() { Spacing = 12 };
    private readonly SelectableTextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _saveButton = new() { Content = "Save changes" };
    private readonly Button _cancel = new() { Content = "Cancel" };
    private Func<CanonFields> _read;
    private Func<bool> _valid = () => true;
    private bool _saving, _failed, _allowClose, _confirming;
    private readonly CanonCreationSnapshot? _creation;
    private TextBox? _id, _name;
    private readonly TextBlock _idError = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _requiredError = new() { TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _sheetContext = new() { Spacing = 5 };
    private bool _ready, _manualId, _suggesting;
    private bool _placementChanged;
    private string? _shownSheetId;
    private readonly SessionContext _context;
    public string CreatedId => _id?.Text ?? "";
    private Func<InitialDiscovery?> _readDiscovery = () => null;

    public CanonEditorWindow(CanonCreationSnapshot creation, SessionContext context, string saveId,
        Func<string, CanonFields, InitialDiscovery?, Task<SessionResult<EditReport>>> save)
        : this(creation.Form, creation.CopyCatalog(), context, saveId, null!, creation, save) { }

    public CanonEditorWindow(CanonEditSnapshot baseline, WorldState world, SessionContext context,
        string saveId, Func<CanonFields, Task<SessionResult<EditReport>>> save)
        : this(baseline, world, context, saveId, save, null, null) { }

    private CanonEditorWindow(CanonEditSnapshot baseline, WorldState world, SessionContext context,
        string saveId, Func<CanonFields, Task<SessionResult<EditReport>>> save,
        CanonCreationSnapshot? creation, Func<string, CanonFields, InitialDiscovery?, Task<SessionResult<EditReport>>>? createSave)
    {
        _baseline = baseline; _creation = creation; _context = context;
        _save = creation is null ? save : fields => createSave!(CreatedId, fields, _readDiscovery());
        _read = () => baseline.Fields;
        string label = baseline.Fields switch
        {
            CharacterFields c => c.Name, LocationFields l => l.Name,
            ItemFields i => i.Name, FactFields f => f.Text.Length > 80 ? f.Text[..80] + "…" : f.Text,
            _ => baseline.Target.Id
        };
        Title = creation is null ? $"Edit {baseline.Target.Kind.ToString().ToLowerInvariant()} · {label}"
            : $"Add {baseline.Target.Kind.ToString().ToLowerInvariant()}";
        if (creation is not null) _saveButton.Content = Title;
        Width = 720; Height = 680; MinWidth = 400; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var header = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(new TextBlock { Text = $"{context.Pack.Manifest?.Name ?? context.Pack.Id} · {saveId}", FontWeight = FontWeight.SemiBold });
        header.Children.Add(new TextBlock { Text = "Changes apply to this playthrough", TextWrapping = TextWrapping.Wrap });
        if (creation is null) header.Children.Add(new SelectableTextBlock { Text = $"ID: {baseline.Target.Id}", TextWrapping = TextWrapping.Wrap });
        if (creation is null && baseline.Target.Key != baseline.Target.Id)
            header.Children.Add(new SelectableTextBlock { Text = $"Stored under key: {baseline.Target.Key}", TextWrapping = TextWrapping.Wrap });
        layout.Children.Add(header);
        var scroll = new ScrollViewer { Content = _fields, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0, 14, 0, 0) };
        footer.Children.Add(new ScrollViewer { Content = _error, MaxHeight = 120,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        footer.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10, Children = { _cancel, _saveButton } });
        Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;

        var locations = world.Locations.Select(p => new CanonChoice(p.Key, p.Value.Name)).ToArray();
        var people = world.Characters.Select(p => new CanonChoice(p.Key, p.Value.Name)).ToArray();
        if (creation is not null)
        {
            _id = TextField("ID · permanent after adding", "");
            _id.PropertyChanged += (_, e) =>
            {
                if (e.Property != TextBox.TextProperty) return;
                if (_ready && !_suggesting) _manualId = true;
                Update();
            };
            _fields.Children.Add(_idError);
            var suggest = new Button { Content = "Use suggested ID" };
            suggest.Click += (_, _) => { _manualId = false; SuggestId(); Update(); };
            _fields.Children.Add(suggest);
            _fields.Children.Add(new TextBlock { Text = "Reusing an old ID may make past references resolve to this new entry. History is not rewritten.", TextWrapping = TextWrapping.Wrap });
        }
        switch (baseline.Fields)
        {
            case CharacterFields c:
                CharacterForm(c, world, context, locations); break;
            case LocationFields l:
                var ln = NameField("Name", l.Name); var ld = TextField("Description", l.Description, true);
                var ls = TextField("Status", l.Status);
                var links = Multiple("Reachable from here · outgoing connections only", locations, l.Connections);
                _read = () => new LocationFields(ln.Text ?? "", ld.Text ?? "", ls.Text ?? "", links.SelectedIds);
                break;
            case FactFields f:
                var ft = NameField("Fact text", f.Text, true);
                var knowers = Multiple("Known by", people, f.KnownBy);
                if (creation is not null) _fields.Children.Add(new TextBlock
                {
                    Text = "Only the selected characters will know this fact. Any old dangling knowledge of this ID on unselected characters is removed.",
                    TextWrapping = TextWrapping.Wrap
                });
                _read = () => new FactFields(ft.Text ?? "", knowers.SelectedIds);
                break;
            case ItemFields i:
                ItemForm(i, people, locations); break;
        }
        string metadata = baseline.Target.Kind switch
        {
            CanonKind.Character => $"Last seen: {(baseline.Metadata.LastSeenTurn is { } turn ? $"turn {turn}" : "never")}",
            CanonKind.Fact => $"Established: turn {baseline.Metadata.EstablishedTurn}\nSource: "
                + (baseline.Metadata.SourceId is { } source ? $"{world.FindCharacter(source)?.Name ?? "Missing reference"} ({source})" : "Narration"),
            CanonKind.Location => "Present: " + string.Join(", ", world.CharactersIn(baseline.Target.Id).Select(c => $"{c.Name} ({c.Id})")),
            _ => string.Empty
        };
        if (creation is not null && baseline.Target.Kind != CanonKind.Fact)
        {
            _fields.Children.Add(new TextBlock { Text = "Player knowledge · creation does not imply an encounter. Reused IDs retain their remembered information unless explicitly corrected.", TextWrapping = TextWrapping.Wrap });
            var known = new CheckBox { Content = "The protagonist knows this identity", IsChecked = false }; _fields.Children.Add(known);
            var safeName = TextField("Disclosed name / alias (required when known)", "");
            var safeDescription = TextField("Disclosed description (optional)", "", true);
            var safeCondition = TextField("Observed condition (optional)", "");
            ComboBox? whereaboutsMode = null;
            CanonReferencePicker? knownLocation = null, knownHolder = null;
            TextBox? whereaboutsLabel = null;
            if (baseline.Target.Kind != CanonKind.Location)
            {
                whereaboutsMode = new ComboBox { ItemsSource = baseline.Target.Kind == CanonKind.Item
                    ? new[] { "No whereabouts recorded", "Unknown whereabouts", "At location", "Held by character" }
                    : new[] { "No whereabouts recorded", "Unknown whereabouts", "At location" }, SelectedIndex = 0 };
                Add("Player's knowledge of whereabouts", whereaboutsMode);
                knownLocation = Single("Disclosed location", locations, null, "Choose a location");
                if (baseline.Target.Kind == CanonKind.Item) knownHolder = Single("Disclosed holder", people, null, "Choose a character");
                whereaboutsLabel = TextField("Disclosed location / holder label (optional)", "");
                void ShowKnowledgePlacement()
                {
                    ((Control)knownLocation.Parent!).IsVisible = whereaboutsMode.SelectedIndex == 2;
                    if (knownHolder is not null) ((Control)knownHolder.Parent!).IsVisible = whereaboutsMode.SelectedIndex == 3;
                    ((Control)whereaboutsLabel.Parent!).IsVisible = whereaboutsMode.SelectedIndex >= 2;
                }
                whereaboutsMode.SelectionChanged += (_, _) => ShowKnowledgePlacement();
                ShowKnowledgePlacement();
            }
            var requires = new CheckBox { Content = "Requires discovery", IsChecked = false }; _fields.Children.Add(requires);
            var instruction = TextField("Private discovery instruction", "", true);
            _readDiscovery = () =>
            {
                if (known.IsChecked == true && string.IsNullOrWhiteSpace(safeName.Text)) throw new InvalidOperationException("Enter a disclosed name or alias for player knowledge.");
                Whereabouts? whereabouts = null;
                if (known.IsChecked == true && whereaboutsMode is { SelectedIndex: > 0 })
                {
                    int mode = whereaboutsMode.SelectedIndex;
                    string? target = mode == 2 ? knownLocation?.SelectedId : mode == 3 ? knownHolder?.SelectedId : null;
                    if (mode >= 2 && target is null) throw new InvalidOperationException("Choose the disclosed location or holder.");
                    whereabouts = new(mode == 1 ? WhereaboutsKind.Unknown : mode == 2 ? WhereaboutsKind.Location : WhereaboutsKind.Holder,
                        target, string.IsNullOrWhiteSpace(whereaboutsLabel?.Text) ? null : whereaboutsLabel.Text);
                }
                return new(known.IsChecked == true ? safeName.Text : null,
                    known.IsChecked == true ? safeDescription.Text : null, known.IsChecked == true ? safeCondition.Text : null,
                    Whereabouts: whereabouts,
                    RequiresDiscovery: requires.IsChecked == true, PrivateInstruction: instruction.Text ?? "");
            };
        }
        if (creation is not null) metadata = baseline.Target.Kind switch
        {
            CanonKind.Fact => "Authored world truth · no attributed speaker. Established at the turn when added.",
            CanonKind.Character => "Save canon only; no character sheet is created. Last seen is set to the current turn by authoring, including when offstage.",
            _ => ""
        };
        if (!string.IsNullOrEmpty(metadata))
            _fields.Children.Add(new SelectableTextBlock { Text = metadata, TextWrapping = TextWrapping.Wrap });
        _saveButton.Click += async (_, _) => await SaveAsync();
        _cancel.Click += (_, _) => Close();
        Closing += OnClosing;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Opened += (_, _) =>
        {
            if (_creation is not null) _name?.Focus();
            if (Screens.ScreenFromWindow(this) is { } screen)
            {
                double width = screen.WorkingArea.Width / screen.Scaling;
                double height = screen.WorkingArea.Height / screen.Scaling;
                MinWidth = Math.Min(MinWidth, width); MinHeight = Math.Min(MinHeight, height);
                Width = Math.Min(Width, width); Height = Math.Min(Height, height);
                Position = new PixelPoint(
                    Math.Clamp(Position.X, screen.WorkingArea.X, Math.Max(screen.WorkingArea.X, screen.WorkingArea.Right - (int)Math.Ceiling(Width * screen.Scaling))),
                    Math.Clamp(Position.Y, screen.WorkingArea.Y, Math.Max(screen.WorkingArea.Y, screen.WorkingArea.Bottom - (int)Math.Ceiling(Height * screen.Scaling))));
            }
        };
        _ready = true; Update();
    }

    private void CharacterForm(CharacterFields c, WorldState world, SessionContext context, CanonChoice[] locations)
    {
        var name = NameField("Name", c.Name); var description = TextField("Description · this save", c.Description, true);
        var place = Single("Location", locations, c.LocationId, "Offstage / unknown");
        var status = TextField("Status", c.Status); var mood = TextField("Mood", c.Mood);
        TextBox? standing = null, summary = null;
        var standingError = new TextBlock { TextWrapping = TextWrapping.Wrap };
        if (_creation is not null || !world.Characters[_baseline.Target.Key].IsPlayer)
        {
            standing = TextField("Relationship standing (-100 to 100)", c.Relationship.Standing.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _fields.Children.Add(standingError);
            summary = TextField("Relationship to player", c.Relationship.Summary, true);
        }
        var facts = world.Facts.Select(p => new CanonChoice(p.Key, p.Value.Text)).ToArray();
        var lore = context.Pack.Lore.All.Select(l => new CanonChoice(l.Id, l.Title + (l.Common ? " · common lore; explicit learning only" : ""), l.Body)).ToArray();
        var factIds = facts.Select(f => f.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var loreIds = lore.Select(l => l.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var knownFacts = Multiple("Knowledge · facts", facts, c.Knowledge.Where(id => factIds.Contains(id)).ToArray());
        var knownLore = Multiple("Knowledge · explicitly learned lore", lore, c.Knowledge.Where(id => !factIds.Contains(id) && loreIds.Contains(id)).ToArray());
        var missingIds = c.Knowledge.Where(id => !factIds.Contains(id) && !loreIds.Contains(id)).ToArray();
        CanonReferencePicker? missing = missingIds.Length == 0 ? null : Multiple("Knowledge · missing references", [], missingIds);
        foreach (var entry in context.Pack.Lore.All)
            _fields.Children.Add(new Expander
            {
                Header = entry.Common ? $"{entry.Title} · Known by everyone · from world pack" : $"Read lore: {entry.Title}",
                Content = new SelectableTextBlock { Text = entry.Body, TextWrapping = TextWrapping.Wrap }
            });
        if (context.Pack.Lore.All.Any(l => l.Common))
            _fields.Children.Add(new TextBlock { Text = "Removing explicit learning of common lore does not remove common knowledge.", TextWrapping = TextWrapping.Wrap });
        if (context.Pack.Sheets.TryGetValue(_baseline.Target.Id, out var sheet))
            _fields.Children.Add(new Expander { Header = "Authored character sheet · from world pack",
                Content = new SelectableTextBlock { Text = sheet.Body + (sheet.Attitudes.Count == 0 ? "" : "\n\nAttitudes\n" + string.Join("\n", sheet.Attitudes.Select(p => $"{p.Key}: {p.Value}"))), TextWrapping = TextWrapping.Wrap } });
        if (_creation is not null) _fields.Children.Add(_sheetContext);
        _valid = () =>
        {
            bool valid = standing is null || standing.Text == c.Relationship.Standing.ToString(System.Globalization.CultureInfo.InvariantCulture)
                || (int.TryParse(standing.Text, out int number) && number is >= -100 and <= 100);
            standingError.Text = valid ? "" : "Enter a whole number between -100 and 100.";
            return valid;
        };
        _read = () => new CharacterFields(name.Text ?? "", description.Text ?? "", place.SelectedId, status.Text ?? "", mood.Text ?? "",
            standing is null ? c.Relationship : new Relationship(int.TryParse(standing.Text, out int number) ? number : c.Relationship.Standing, summary!.Text ?? ""),
            knownFacts.SelectedIds.Concat(knownLore.SelectedIds).Concat(missing?.SelectedIds ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private void ItemForm(ItemFields i, CanonChoice[] people, CanonChoice[] locations)
    {
        var name = NameField("Name", i.Name); var description = TextField("Description", i.Description, true);
        var status = TextField("Condition", i.Status);
        var mode = new ComboBox { ItemsSource = new[] { _creation is null ? "Keep current placement" : "Choose placement", "Held by character", "At location" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
        Add("Placement", mode);
        if (_creation is null) _fields.Children.Add(new SelectableTextBlock { Text = $"Current holder: {i.HolderId ?? "none"}\nCurrent location: {i.LocationId ?? "none"}", TextWrapping = TextWrapping.Wrap });
        if (_creation is null && (i.HolderId is null) == (i.LocationId is null))
            _fields.Children.Add(new TextBlock { Text = "Current placement is inconsistent. Keep it unchanged or explicitly choose a holder/location to correct it.", TextWrapping = TextWrapping.Wrap });
        var holder = Single("Holder", people, i.HolderId, "Choose a character");
        var location = Single("Location", locations, i.LocationId, "Choose a location");
        var placementError = new TextBlock { TextWrapping = TextWrapping.Wrap }; _fields.Children.Add(placementError);
        void Show()
        {
            _placementChanged = mode.SelectedIndex != 0;
            ((Control)holder.Parent!).IsVisible = mode.SelectedIndex == 1;
            ((Control)location.Parent!).IsVisible = mode.SelectedIndex == 2;
            Update();
        }
        mode.SelectionChanged += (_, _) => Show();
        _valid = () =>
        {
            bool valid = mode.SelectedIndex == 0 ? _creation is null : (mode.SelectedIndex == 1 ? holder.SelectedId is not null : location.SelectedId is not null);
            placementError.Text = valid ? "" : "Select a holder or location."; return valid;
        };
        _read = () => new ItemFields(name.Text ?? "", description.Text ?? "", status.Text ?? "",
            mode.SelectedIndex == 0 ? i.LocationId : mode.SelectedIndex == 2 ? location.SelectedId : null,
            mode.SelectedIndex == 0 ? i.HolderId : mode.SelectedIndex == 1 ? holder.SelectedId : null);
        Show();
    }

    private TextBox TextField(string label, string value, bool multiline = false)
    {
        var field = new TextBox { Text = value, AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap,
            MinHeight = multiline ? 95 : 0 };
        field.TextChanged += (_, _) => Update(); Add(label, field); return field;
    }
    private TextBox NameField(string label, string value, bool multiline = false)
    {
        _name = TextField(label, value, multiline);
        if (_creation is not null)
        {
            _fields.Children.Add(_requiredError);
            _name.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) { SuggestId(); Update(); } };
        }
        return _name;
    }
    private void SuggestId()
    {
        if (!_ready || _manualId || _id is null) return;
        _suggesting = true;
        try { _id.Text = Authoring.Slug(_name?.Text ?? ""); }
        finally { _suggesting = false; }
    }
    private void Add(string label, Control control)
    {
        AutomationProperties.SetName(control, label);
        _fields.Children.Add(new StackPanel { Spacing = 5, Children =
        {
            new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap }, control
        } });
    }
    private CanonReferencePicker Multiple(string label, IEnumerable<CanonChoice> choices, IEnumerable<string> selected)
    {
        var picker = new CanonReferencePicker(choices, selected); picker.Changed += Update; Add(label, picker); return picker;
    }
    private CanonReferencePicker Single(string label, IEnumerable<CanonChoice> choices, string? selected, string empty)
    {
        var picker = new CanonReferencePicker(choices, selected, empty); picker.Changed += Update; Add(label, picker); return picker;
    }
    private bool Dirty => !CanonCorrection.Same(_baseline.Fields, _read())
        || ((_creation is null || _baseline.Target.Kind == CanonKind.Character) && !_valid())
        || (_creation is not null && _placementChanged)
        || (_creation is not null && !string.IsNullOrEmpty(CreatedId));
    private void Update()
    {
        if (!_ready) return;
        bool valid = _valid();
        if (_creation is not null)
        {
            _idError.Text = _creation.IdError(CreatedId) ?? "";
            _requiredError.Text = string.IsNullOrWhiteSpace(_name?.Text)
                ? (_baseline.Target.Kind == CanonKind.Fact ? "Fact text is required." : "Name is required.") : "";
            valid = valid && _creation.InputError(CreatedId, _read()) is null;
            if (_baseline.Target.Kind == CanonKind.Character && _shownSheetId != CreatedId)
            {
                _shownSheetId = CreatedId; _sheetContext.Children.Clear();
                if (_context.Pack.Sheets.TryGetValue(CreatedId, out var sheet))
                {
                    _sheetContext.Children.Add(new TextBlock { Text = "This ID matches a pack character sheet. Its existing authored identity will apply; adding this entry does not edit the sheet.", TextWrapping = TextWrapping.Wrap });
                    _sheetContext.Children.Add(new Expander { Header = "Authored character sheet · from world pack", Content = new SelectableTextBlock
                    { Text = sheet.Body + "\n\n" + string.Join("\n", sheet.Attitudes.Select(p => $"{p.Key}: {p.Value}")), TextWrapping = TextWrapping.Wrap } });
                }
            }
        }
        _saveButton.IsEnabled = !_saving && !_failed && valid && (_creation is not null || Dirty);
    }

    private async Task SaveAsync()
    {
        if (!_saveButton.IsEnabled) return;
        // Input errors occur before the session operation and must not disable the draft
        // as if a persistence failure had left canon partially saved.
        try { if (_creation is not null) _readDiscovery(); }
        catch (InvalidOperationException error) { _error.Text = error.Message; return; }
        _saving = true; _fields.IsEnabled = false; _cancel.IsEnabled = false; Update(); _error.Text = "Saving changes…";
        try
        {
            var result = await _save(_read());
            if (result.WasRefused) { _error.Text = result.RefusedBecause; return; }
            _allowClose = true; Close(result.Value);
        }
        catch (Exception error)
        {
            _failed = true; _cancel.Content = "Close";
            _error.Text = "Saving did not complete. Copy any draft text you need, then close and reopen the playthrough before making further changes. Cancel cannot undo changes already applied in memory.\n" + error.Message;
        }
        finally { _saving = false; _fields.IsEnabled = true; _cancel.IsEnabled = true; Update(); }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_saving && !_allowClose) { e.Cancel = true; return; }
        if (_allowClose || !Dirty) return;
        e.Cancel = true;
        if (_confirming) return;
        _confirming = true;
        var dialog = new Window { Title = _failed ? "Close editor?" : "Discard changes?", Width = 400,
            SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var keep = new Button { Content = "Keep editing", IsCancel = true };
        var discard = new Button { Content = _failed ? "Close editor" : "Discard" };
        keep.Click += (_, _) => dialog.Close(false); discard.Click += (_, _) => dialog.Close(true);
        dialog.Content = new StackPanel { Margin = new Thickness(20), Spacing = 16, Children =
        {
            new TextBlock { Text = _failed ? "Closing loses the form draft. It does not undo changes that may already be in memory or on disk." : "Discard the unsaved changes in this form?", TextWrapping = TextWrapping.Wrap },
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { keep, discard } }
        } };
        try { if (await dialog.ShowDialog<bool>(this)) { _allowClose = true; Close(); } }
        finally { _confirming = false; }
    }
}
