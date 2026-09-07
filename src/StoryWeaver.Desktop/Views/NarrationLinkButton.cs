using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace StoryWeaver.Desktop.Views;

/// <summary>A keyboard-accessible inline link that reports its label's text baseline.</summary>
internal sealed class NarrationLinkButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public TextBlock Label { get; }

    public NarrationLinkButton(string text)
    {
        Label = new TextBlock { Text = text, TextWrapping = TextWrapping.NoWrap };
        Content = Label;
        VerticalContentAlignment = VerticalAlignment.Top;
        Classes.Add("entityLink");
        PointerEntered += (_, _) => Label.TextDecorations = TextDecorations.Underline;
        PointerExited += (_, _) => Label.TextDecorations = null;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        // InlineUIContainer otherwise treats the control's bottom edge as its
        // baseline, lifting the letters by the font's descent. Recompute from
        // the actual label on every measure so font-size changes remain aligned.
        TextBlock.SetBaselineOffset(this,
            BorderThickness.Top + Padding.Top + Label.Padding.Top + Label.TextLayout.Baseline);
        return size;
    }
}
