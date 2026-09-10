using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StoryWeaver.Core;
using Location = StoryWeaver.Core.Location;

namespace StoryWeaver.Desktop.Views;

/// <summary>Detached author drafts. All validation, revision checks and persistence belong to Core.</summary>
public sealed class DiscoveryEditorWindow : Window
{
    private readonly WorldState _catalog;
    private readonly DiscoveryState _draft;
    private readonly StackPanel _form = new() { Spacing = 12 };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private Action? _capture;
    private bool _saving;
    private sealed record Choice(DiscoveryKind Kind, string Id, string Label) { public override string ToString() => Label; }

    public DiscoveryEditorWindow(DiscoveryEditSnapshot baseline, string saveId,
        Func<DiscoveryState, Task<SessionResult<EditReport>>> save)
    {
        _catalog = baseline.CopyCatalog(); _draft = baseline.CreateDraft();
        Title = "Player knowledge · " + saveId; Width = 900; Height = 800; MinWidth = 650; MinHeight = 450;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var choices = _catalog.Characters.Values.Select(c => new Choice(DiscoveryKind.Character, c.Id, "Character · " + c.Name + " (" + c.Id + ")"))
            .Concat(_catalog.Locations.Values.Select(l => new Choice(DiscoveryKind.Location, l.Id, "Location · " + l.Name + " (" + l.Id + ")")))
            .Concat(_catalog.Items.Values.Select(i => new Choice(DiscoveryKind.Item, i.Id, "Item · " + i.Name + " (" + i.Id + ")")))
            .Concat(_draft.Entries.Values.Where(m => !DiscoveryEngine.Exists(_catalog, m.Kind, m.Id))
                .Select(m => new Choice(m.Kind, m.Id, "Remembered · " + (DiscoveryEngine.SafeName(m) ?? m.Id))))
            .OrderBy(c => c.Label).ToList();
        var select = new ComboBox { ItemsSource = choices, HorizontalAlignment = HorizontalAlignment.Stretch };
        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock { Text = "Full canon · contains spoilers", FontWeight = FontWeight.Bold });
        header.Children.Add(new TextBlock { Text = "Edit only what the protagonist knows. Canon edits do not update these memories. Marking an identity unknown keeps independently learned facts; edit their Known by memberships separately.", TextWrapping = TextWrapping.Wrap });
        header.Children.Add(select);
        Choice? current = null; bool changing = false;
        select.SelectionChanged += (_, _) =>
        {
            if (changing) return;
            try { _capture?.Invoke(); _error.Text = ""; }
            catch (Exception ex) { _error.Text = ex.Message; changing = true; select.SelectedItem = current; changing = false; return; }
            current = select.SelectedItem as Choice;
            if (current is not null) Render(current);
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        var submit = new Button { Content = "Save player knowledge" };
        bool saveFailed = false;
        cancel.Click += (_, _) => { if (!_saving) Close(); };
        submit.Click += async (_, _) =>
        {
            bool submitted = false;
            try
            {
                _capture?.Invoke(); _saving = true; submit.IsEnabled = false; cancel.IsEnabled = false; select.IsEnabled = false; _form.IsEnabled = false;
                submitted = true;
                var result = await save(_draft);
                if (result.WasRefused) _error.Text = result.RefusedBecause;
                else { _saving = false; Close(result.Value); }
            }
            catch (Exception ex)
            {
                saveFailed = submitted;
                _error.Text = submitted ? "Saving did not complete. Close this editor and reopen the playthrough before making further changes.\n" + ex.Message : ex.Message;
                if (submitted) cancel.Content = "Close";
            }
            finally { _saving = false; submit.IsEnabled = !saveFailed; cancel.IsEnabled = true; select.IsEnabled = !saveFailed; _form.IsEnabled = !saveFailed; }
        };
        Closing += (_, e) => { if (_saving) e.Cancel = true; };
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        footer.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel); buttons.Children.Add(submit); footer.Children.Add(buttons);
        var layout = new Grid { Margin = new Thickness(20), RowDefinitions = new("Auto,*,Auto") };
        layout.Children.Add(header);
        var scroll = new ScrollViewer { Content = _form, Margin = new Thickness(0, 16, 0, 0), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll); Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;
        select.SelectedIndex = choices.Count > 0 ? 0 : -1;
    }

    private void Render(Choice choice)
    {
        _form.Children.Clear();
        string key = DiscoveryState.Key(choice.Kind, choice.Id);
        var memory = _draft.Find(choice.Kind, choice.Id) ?? new EntityMemory { Kind = choice.Kind, Id = choice.Id, IdentityKnown = false };
        Entity? actual = choice.Kind switch { DiscoveryKind.Character => _catalog.FindCharacter(choice.Id), DiscoveryKind.Location => _catalog.FindLocation(choice.Id), _ => _catalog.FindItem(choice.Id) };
        var known = new CheckBox { Content = "Identity known", IsChecked = memory.IdentityKnown };
        var protectIdentity = new CheckBox { Content = "Protect identity correction from retrying old prose", IsChecked = memory.AuthorProtected };
        known.IsCheckedChanged += (_, _) => protectIdentity.IsChecked = true;
        _form.Children.Add(known); _form.Children.Add(protectIdentity);
        var rule = _draft.Rules.GetValueOrDefault(key) ?? new();
        var concealed = new CheckBox { Content = "Requires discovery", IsChecked = rule.RequiresDiscovery };
        var instruction = Box(rule.PrivateInstruction);
        _form.Children.Add(concealed); AddLabel(_form, "Private narrative instruction", instruction);
        var name = TextAspect("Learned name / alias", memory.Name, actual?.Name);
        var description = TextAspect("Learned description", memory.Description, actual switch { Character c => c.Description, Location l => l.Description, Item i => i.Description, _ => null });
        string? condition = actual switch { Character c => c.Status, Location l => l.Status, Item i => i.Status, _ => null };
        var status = TextAspect("Observed condition / demeanour", memory.Condition, condition);
        var aliases = Box(string.Join("\n", memory.Aliases)); AddLabel(_form, "Previously disclosed aliases (one per line)", aliases);
        Func<MemoryAspect<Whereabouts>>? whereabouts = null;
        if (choice.Kind != DiscoveryKind.Location)
        {
            Whereabouts? actualWhere = actual switch
            {
                Character c => c.LocationId is null ? new(WhereaboutsKind.Unknown) : new(WhereaboutsKind.Location, c.LocationId),
                Item i => i.HolderId is not null ? new(WhereaboutsKind.Holder, i.HolderId) : i.LocationId is not null ? new(WhereaboutsKind.Location, i.LocationId) : new(WhereaboutsKind.Unknown), _ => null
            };
            whereabouts = Aspect("Whereabouts", memory.Whereabouts, WhereEditor, actualWhere);
            _form.Children.Add(new TextBlock { Text = memory.LastSighting is { } last ? $"Earlier direct sighting: {last.Value.Kind} {last.Value.TargetId} · turn {last.Turn}" : "No earlier direct sighting.", TextWrapping = TextWrapping.Wrap });
            var clearLast = new Button { Content = "Clear earlier sighting", HorizontalAlignment = HorizontalAlignment.Left };
            clearLast.Click += (_, _) => { memory.LastSighting = null; clearLast.Content = "Earlier sighting cleared in draft"; }; _form.Children.Add(clearLast);
        }
        var routes = new List<(string Id, Func<MemoryAspect<string>> Read, CheckBox Requires, TextBox Instruction)>();
        if (choice.Kind == DiscoveryKind.Location)
        {
            _form.Children.Add(new TextBlock { Text = "Directed routes · each disclosure and presentation rule is independent", FontWeight = FontWeight.Bold });
            var targets = memory.Connections.Keys.Concat((actual as Location)?.Connections ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            void AddRoute(string destination)
            {
                if (routes.Any(r => r.Id == destination)) return;
                var routeKey = $"Route:{choice.Id}:{destination}";
                var routeRule = _draft.Rules.GetValueOrDefault(routeKey) ?? new();
                var requires = new CheckBox { Content = "This route requires discovery", IsChecked = routeRule.RequiresDiscovery };
                var hint = Box(routeRule.PrivateInstruction);
                var read = TextAspect("Route to " + (_catalog.FindLocation(destination)?.Name ?? destination), memory.Connections.GetValueOrDefault(destination) ?? new(), _catalog.FindLocation(destination)?.Name);
                _form.Children.Add(requires); AddLabel(_form, "Private route instruction", hint); routes.Add((destination, read, requires, hint));
            }
            foreach (var target in targets) AddRoute(target);
            var destinationPicker = new ComboBox { ItemsSource = _catalog.Locations.Values.Select(l => new Choice(DiscoveryKind.Location, l.Id, l.Name + " (" + l.Id + ")")).ToList(), HorizontalAlignment = HorizontalAlignment.Stretch };
            var addRoute = new Button { Content = "Add remembered route / route rule", HorizontalAlignment = HorizontalAlignment.Left };
            addRoute.Click += (_, _) => { if (destinationPicker.SelectedItem is Choice target) AddRoute(target.Id); };
            _form.Children.Add(destinationPicker); _form.Children.Add(addRoute);
        }
        _capture = () =>
        {
            memory.IdentityKnown = known.IsChecked == true; memory.AuthorProtected = protectIdentity.IsChecked == true;
            memory.Name = name(); memory.Description = description(); memory.Condition = status();
            memory.Aliases = (aliases.Text ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
            if (whereabouts is not null)
            {
                memory.Whereabouts = whereabouts();
            }
            _draft.Entries[key] = memory;
            _draft.Rules[key] = new(concealed.IsChecked == true, instruction.Text ?? "");
            foreach (var route in routes)
            {
                memory.Connections[route.Id] = route.Read();
                _draft.Rules[$"Route:{choice.Id}:{route.Id}"] = new(route.Requires.IsChecked == true, route.Instruction.Text ?? "");
            }
        };
    }

    private Func<MemoryAspect<string>> TextAspect(string title, MemoryAspect<string> value, string? actual) =>
        Aspect(title, value, initial => { var box = Box(initial ?? ""); return (box, () => box.Text ?? "", v => box.Text = v); }, actual);

    private Func<MemoryAspect<T>> Aspect<T>(string title, MemoryAspect<T> value,
        Func<T?, (Control Control, Func<T> Read, Action<T> Set)> editor, T? actual)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.Bold });
        if (actual is not null) panel.Children.Add(new SelectableTextBlock { Text = "Actual canon: " + actual, TextWrapping = TextWrapping.Wrap });
        var protect = new CheckBox { Content = "Protect corrections from retrying old prose", IsChecked = value.AuthorProtected };
        panel.Children.Add(protect);
        Func<Learned<T>?> Slot(Learned<T>? initial, bool reported)
        {
            var enabled = new CheckBox { Content = reported ? "Reported information" : "Direct / starting knowledge", IsChecked = initial is not null };
            var input = editor(initial is null ? default : initial.Value);
            var turn = new NumericUpDown { Minimum = 0, Maximum = _catalog.TurnNumber, Value = initial?.Turn ?? _catalog.TurnNumber, Increment = 1, FormatString = "0" };
            var provenance = new ComboBox { ItemsSource = new[] { DiscoveryProvenance.Observed, DiscoveryProvenance.Starting }, SelectedItem = initial?.Provenance ?? DiscoveryProvenance.Observed };
            var claims = _catalog.Facts.Values.Where(f => f.SourceId is not null && _catalog.Player?.Knows.Contains(f.Id) == true)
                .Select(f => new Claim(f.Id, f.Text + " (" + f.Id + ")")).ToList();
            if (initial?.FactId is { } id && claims.All(c => c.Id != id)) claims.Add(new(id, "Unresolved claim (" + id + ")"));
            var fact = new ComboBox { ItemsSource = claims, SelectedItem = claims.FirstOrDefault(c => c.Id == initial?.FactId), HorizontalAlignment = HorizontalAlignment.Stretch };
            var evidence = Box(initial?.Evidence ?? "Author correction");
            input.Control.AddHandler(Avalonia.Input.InputElement.TextInputEvent, (_, _) => protect.IsChecked = true, Avalonia.Interactivity.RoutingStrategies.Bubble);
            input.Control.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, (_, _) => protect.IsChecked = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            input.Control.AddHandler(Avalonia.Input.InputElement.KeyDownEvent, (_, _) => protect.IsChecked = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            enabled.IsCheckedChanged += (_, _) => protect.IsChecked = true;
            turn.ValueChanged += (_, _) => protect.IsChecked = true;
            provenance.SelectionChanged += (_, _) => protect.IsChecked = true;
            fact.SelectionChanged += (_, _) => protect.IsChecked = true;
            var slotPanel = new StackPanel { Spacing = 6 };
            // Report controls are collapsed initially unless there is a report to review.
            var slotHost = reported ? slotPanel : panel;
            slotHost.Children.Add(enabled); slotHost.Children.Add(input.Control);
            if (actual is not null && !reported)
            {
                var copy = new Button { Content = "Copy this field from canon", HorizontalAlignment = HorizontalAlignment.Left };
                copy.Click += (_, _) => { input.Set(actual); enabled.IsChecked = true; protect.IsChecked = true; }; panel.Children.Add(copy);
            }
            AddLabel(slotHost, "Learned turn", turn);
            if (reported) AddLabel(slotHost, "Attributed fact already learned by player", fact); else AddLabel(slotHost, "Provenance", provenance);
            AddLabel(slotHost, "Evidence / author note", evidence);
            if (reported) panel.Children.Add(new Expander { Header = "Reported information", IsExpanded = initial is not null, Content = slotPanel, HorizontalAlignment = HorizontalAlignment.Stretch });
            return () =>
            {
                if (enabled.IsChecked != true) return null;
                var source = reported ? DiscoveryProvenance.Reported : (DiscoveryProvenance)(provenance.SelectedItem ?? DiscoveryProvenance.Observed);
                return new(input.Read(), source, source == DiscoveryProvenance.Starting ? 0 : (int)(turn.Value ?? 0), evidence.Text ?? "",
                    reported ? (fact.SelectedItem as Claim)?.Id : null, initial?.Revision ?? 0);
            };
        }
        var readKnown = Slot(value.Known, false); var readReport = Slot(value.Report, true);
        _form.Children.Add(new Border { BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Padding = new Thickness(12), Child = panel });
        return () => new() { Known = readKnown(), Report = readReport(), AuthorProtected = protect.IsChecked == true, ProtectedThroughTurn = value.ProtectedThroughTurn };
    }

    private sealed record Claim(string Id, string Label) { public override string ToString() => Label; }
    private (Control, Func<Whereabouts>, Action<Whereabouts>) WhereEditor(Whereabouts? value)
    {
        var panel = new StackPanel { Spacing = 6 };
        var kind = new ComboBox { ItemsSource = Enum.GetValues<WhereaboutsKind>(), SelectedItem = value?.Kind ?? WhereaboutsKind.Unknown };
        var targetHost = new StackPanel();
        CanonReferencePicker? target = null;
        void Targets(string? selected)
        {
            var selectedKind = (WhereaboutsKind)(kind.SelectedItem ?? WhereaboutsKind.Unknown);
            IEnumerable<CanonChoice> choices = selectedKind == WhereaboutsKind.Holder
                ? _catalog.Characters.Values.Select(c => new CanonChoice(c.Id, c.Name))
                : _catalog.Locations.Values.Select(l => new CanonChoice(l.Id, l.Name));
            target = new CanonReferencePicker(choices, selected, "No identified target");
            targetHost.Children.Clear();
            if (selectedKind != WhereaboutsKind.Unknown) targetHost.Children.Add(target);
        }
        Targets(value?.TargetId);
        kind.SelectionChanged += (_, _) => Targets(null);
        var label = Box(value?.DisclosedLabel ?? "");
        panel.Children.Add(kind); panel.Children.Add(targetHost); AddLabel(panel, "Disclosed label if target identity is unknown", label);
        return (panel, () => new((WhereaboutsKind)(kind.SelectedItem ?? WhereaboutsKind.Unknown),
            (WhereaboutsKind)(kind.SelectedItem ?? WhereaboutsKind.Unknown) == WhereaboutsKind.Unknown ? null : target?.SelectedId,
            string.IsNullOrWhiteSpace(label.Text) ? null : label.Text.Trim()),
            v => { kind.SelectedItem = v.Kind; Targets(v.TargetId); label.Text = v.DisclosedLabel; });
    }
    private static TextBox Box(string text) => new() { Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 48 };
    private static void AddLabel(StackPanel panel, string label, Control control)
    { panel.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }); panel.Children.Add(control); }
}
