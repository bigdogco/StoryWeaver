using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Media;
using StoryWeaver.Desktop.Presentation;
using StoryWeaver.Desktop.ViewModels;

namespace StoryWeaver.Desktop.Views;

/// <summary>Renders supplied references. Does not parse names, infer identity or change canon.</summary>
public sealed class NarrativeText : TextBlock
{
    public static readonly StyledProperty<NarrativeParagraph?> ParagraphProperty =
        AvaloniaProperty.Register<NarrativeText, NarrativeParagraph?>(nameof(Paragraph));
    public static readonly StyledProperty<PlayShellViewModel?> ShellProperty =
        AvaloniaProperty.Register<NarrativeText, PlayShellViewModel?>(nameof(Shell));

    public NarrativeParagraph? Paragraph
    {
        get => GetValue(ParagraphProperty);
        set => SetValue(ParagraphProperty, value);
    }
    public PlayShellViewModel? Shell
    {
        get => GetValue(ShellProperty);
        set => SetValue(ShellProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ParagraphProperty || change.Property == ShellProperty) Render();
    }

    private void Render()
    {
        Inlines ??= new InlineCollection();
        Inlines.Clear();
        if (Paragraph is not { } paragraph) return;
        foreach (var span in paragraph.Spans)
        {
            if (span.Target is not { } target || Shell is not { } shell || !shell.CanNavigate(target))
            {
                Inlines.Add(new Run(span.Text));
                continue;
            }
            var button = new NarrationLinkButton(span.Text)
            {
                Command = new UiCommand(() => shell.Navigate(target))
            };
            button.Bind(FontSizeProperty, new Binding(nameof(FontSize)) { Source = this });
            button.Bind(FontFamilyProperty, new Binding(nameof(FontFamily)) { Source = this });
            button.Bind(FontStyleProperty, new Binding(nameof(FontStyle)) { Source = this });
            button.Bind(FontWeightProperty, new Binding(nameof(FontWeight)) { Source = this });
            button.Bind(FontStretchProperty, new Binding(nameof(FontStretch)) { Source = this });
            ToolTip.SetTip(button, $"{target.Kind}: {span.Text} — open details");
            Inlines.Add(new InlineUIContainer(button));
        }
    }
}
