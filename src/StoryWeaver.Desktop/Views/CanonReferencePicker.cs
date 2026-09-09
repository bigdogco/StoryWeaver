using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace StoryWeaver.Desktop.Views;

public sealed record CanonChoice(string? Id, string Label, string? Detail = null)
{
    public override string ToString() => Id is null ? Label : $"{Label} ({Id})";
}

/// <summary>Filtering changes visibility only; selection is held independently of visible rows.</summary>
public sealed class CanonReferencePicker : StackPanel
{
    private readonly HashSet<string> _selected;
    private readonly List<CanonChoice> _choices;
    private string? _single;
    public event Action? Changed;
    public IReadOnlyList<string> SelectedIds => _selected.ToArray();
    public string? SelectedId => _single;

    public CanonReferencePicker(IEnumerable<CanonChoice> choices, IEnumerable<string> selected)
    {
        Spacing = 6;
        _selected = new(selected, StringComparer.OrdinalIgnoreCase);
        _choices = Complete(choices, _selected);
        var search = new TextBox { PlaceholderText = "Search names, text or IDs" };
        var rows = new StackPanel { Spacing = 4 };
        var count = new TextBlock();
        void Count() => count.Text = $"{_selected.Count} selected";
        var controls = new List<(CanonChoice Choice, CheckBox Control)>();
        foreach (var choice in _choices)
        {
            if (choice.Id is null) continue;
            var box = new CheckBox
            {
                Content = new TextBlock { Text = choice.ToString(), TextWrapping = TextWrapping.Wrap },
                IsChecked = _selected.Contains(choice.Id), HorizontalContentAlignment = HorizontalAlignment.Stretch
            };
            if (choice.Detail is not null) ToolTip.SetTip(box, choice.Detail);
            box.IsCheckedChanged += (_, _) =>
            {
                if (box.IsChecked == true) _selected.Add(choice.Id); else _selected.Remove(choice.Id);
                Count(); Changed?.Invoke();
            };
            rows.Children.Add(box);
            controls.Add((choice, box));
        }
        search.TextChanged += (_, _) =>
        {
            foreach (var row in controls) row.Control.IsVisible = Matches(row.Choice, search.Text);
        };
        Children.Add(search);
        Children.Add(count);
        Children.Add(new ScrollViewer { Content = rows, MaxHeight = 200,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Count();
    }

    public CanonReferencePicker(IEnumerable<CanonChoice> choices, string? selected, string emptyLabel)
    {
        Spacing = 6;
        _single = selected;
        _selected = new(StringComparer.OrdinalIgnoreCase);
        _choices = Complete(choices, selected is null ? [] : [selected]);
        _choices.Insert(0, new(null, emptyLabel));
        var search = new TextBox { PlaceholderText = "Search names or IDs" };
        var chosen = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var list = new ListBox { Height = 135, ItemsSource = _choices };
        bool filtering = false;
        void Caption() => chosen.Text = "Selected: " + _choices.First(c => Same(c.Id, _single));
        list.SelectedItem = _choices.First(c => Same(c.Id, _single));
        list.SelectionChanged += (_, _) =>
        {
            if (filtering || list.SelectedItem is not CanonChoice choice) return;
            _single = choice.Id; Caption(); Changed?.Invoke();
        };
        search.TextChanged += (_, _) =>
        {
            filtering = true;
            var visible = _choices.Where(c => c.Id is null || Matches(c, search.Text)).ToList();
            list.ItemsSource = visible;
            list.SelectedItem = visible.FirstOrDefault(c => Same(c.Id, _single));
            filtering = false;
        };
        Children.Add(search); Children.Add(chosen); Children.Add(list); Caption();
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static bool Matches(CanonChoice choice, string? text) => choice.ToString().Contains(text ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    private static List<CanonChoice> Complete(IEnumerable<CanonChoice> source, IEnumerable<string> selected)
    {
        var choices = source.DistinctBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Label, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (string id in selected)
            if (!choices.Any(c => Same(c.Id, id))) choices.Add(new(id, "Missing reference"));
        return choices;
    }
}
