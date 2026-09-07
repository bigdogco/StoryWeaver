using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using StoryWeaver.Desktop.Services;
using StoryWeaver.Desktop.ViewModels;
using StoryWeaver.Desktop.Views;

namespace StoryWeaver.Desktop;

public sealed partial class DesktopApplication : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new PreferencesStore();
            var (preferences, warning) = store.Load();
            var model = new PlayShellViewModel(preferences);
            if (desktop.Args?.Contains("--preview", StringComparer.OrdinalIgnoreCase) == true)
                model.LoadPreview();
            model.Notice = warning ?? string.Empty;
            desktop.MainWindow = new MainWindow(model, store);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
