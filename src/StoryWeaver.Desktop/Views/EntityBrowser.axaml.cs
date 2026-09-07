using Avalonia.Controls;
using Avalonia.Interactivity;
using System.ComponentModel;
using StoryWeaver.Desktop.ViewModels;

namespace StoryWeaver.Desktop.Views;

public sealed partial class EntityBrowser : UserControl
{
    private EntityTabViewModel? _model;

    public EntityBrowser()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (_model is not null) _model.PropertyChanged -= ModelChanged;
            _model = DataContext as EntityTabViewModel;
            if (_model is not null) _model.PropertyChanged += ModelChanged;
        };
    }

    private void ModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(EntityTabViewModel.Selected) && _model?.Selected is not null)
            DetailScroll.Offset = default;
    }

    private void BackToList(object? sender, RoutedEventArgs args)
    {
        if (DataContext is EntityTabViewModel model) model.Selected = null;
    }
}
