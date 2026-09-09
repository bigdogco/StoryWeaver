using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using StoryWeaver.Core;

namespace StoryWeaver.Desktop.Views;

public sealed class CanonRemovalWindow : Window
{
    private CanonRemovalPlan _plan;
    private readonly Func<CanonRemovalPlan, Task<SessionResult<CanonRemovalOutcome>>> _remove;
    private readonly SelectableTextBlock _preview = new() { TextWrapping = TextWrapping.Wrap };
    private readonly SelectableTextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _confirm = new();
    private readonly Button _cancel = new() { Content = "Cancel" };
    private bool _saving, _failed, _allowClose;

    public CanonRemovalWindow(CanonRemovalPlan plan, string packName, string saveId,
        Func<CanonRemovalPlan, Task<SessionResult<CanonRemovalOutcome>>> remove)
    {
        _plan = plan; _remove = remove;
        Width = 720; Height = 620; MinWidth = 400; MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var layout = new Grid { Margin = new Thickness(20), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        layout.Children.Add(new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 16), Children =
        {
            new TextBlock { Text = $"{packName} · {saveId}", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Text = "Changes apply to this playthrough", TextWrapping = TextWrapping.Wrap },
            new SelectableTextBlock { Text = $"ID: {plan.Target.Id}" + (plan.Target.Key == plan.Target.Id ? "" : $"\nStored under key: {plan.Target.Key}"), TextWrapping = TextWrapping.Wrap }
        } });
        var scroll = new ScrollViewer { Content = _preview, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        var footer = new StackPanel { Spacing = 12, Margin = new Thickness(0, 16, 0, 0), Children =
        {
            new ScrollViewer { Content = _status, MaxHeight = 120, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled },
            new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 10, Children = { _cancel, _confirm } }
        } };
        Grid.SetRow(footer, 2); layout.Children.Add(footer); Content = layout;
        _cancel.Click += (_, _) => Close();
        _confirm.Click += async (_, _) =>
        {
            if (_saving || _failed || !_confirm.IsEnabled) return;
            _saving = true; _confirm.IsEnabled = false; _cancel.IsEnabled = false;
            _status.Text = "Removing…";
            try
            {
                var result = await _remove(_plan);
                if (result.WasRefused) { _status.Text = result.RefusedBecause; return; }
                if (result.Value!.UpdatedPlan is { } updated)
                {
                    _plan = updated; Display(); scroll.Offset = default;
                    _status.Text = "Canon changed. Nothing was removed. Review the updated consequences, then choose Remove again to confirm.";
                    return;
                }
                _allowClose = true; Close(result.Value.Report);
            }
            catch (Exception error)
            {
                _failed = true; _cancel.Content = "Close";
                _status.Text = "Removal did not complete. Reopen the playthrough before making further changes. Closing cannot undo changes already applied in memory or on disk.\n" + error.Message;
            }
            finally { _saving = false; _cancel.IsEnabled = true; _confirm.IsEnabled = !_failed && _plan.RefusedBecause is null; }
        };
        Closing += (_, e) => { if (_saving && !_allowClose) e.Cancel = true; };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        Opened += (_, _) =>
        {
            if (Screens.ScreenFromWindow(this) is not { } screen) return;
            double width = screen.WorkingArea.Width / screen.Scaling, height = screen.WorkingArea.Height / screen.Scaling;
            MinWidth = Math.Min(MinWidth, width); MinHeight = Math.Min(MinHeight, height);
            Width = Math.Min(Width, width); Height = Math.Min(Height, height);
            Position = new PixelPoint(
                Math.Clamp(Position.X, screen.WorkingArea.X, Math.Max(screen.WorkingArea.X, screen.WorkingArea.Right - (int)Math.Ceiling(Width * screen.Scaling))),
                Math.Clamp(Position.Y, screen.WorkingArea.Y, Math.Max(screen.WorkingArea.Y, screen.WorkingArea.Bottom - (int)Math.Ceiling(Height * screen.Scaling))));
        };
        Display();
    }

    private void Display()
    {
        string kind = _plan.Target.Kind.ToString().ToLowerInvariant();
        Title = $"Remove {kind} · {_plan.Label}?";
        _confirm.Content = $"Remove {kind}";
        _confirm.IsEnabled = !_saving && !_failed && _plan.RefusedBecause is null;
        _preview.Text = string.Join("\n\n", _plan.Consequences.Select(c => "• " + c));
        _status.Text = _plan.RefusedBecause ?? "Review these consequences before removing the entry.";
    }
}
