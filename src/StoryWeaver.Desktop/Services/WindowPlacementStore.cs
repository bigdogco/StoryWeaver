using System.Text.Json;
using Avalonia;
using Avalonia.Controls;

namespace StoryWeaver.Desktop.Services;

/// <summary>Host-specific placement in logical sizes and physical screen coordinates.</summary>
internal static class WindowPlacementStore
{
    private sealed record Placement(double Width, double Height, int X, int Y, bool Maximized);

    public static void Attach(Window window, string key, Action<string> warning)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StoryWeaver", $"window-{key}.json");
        Placement? normal = null;
        bool opened = false;
        try
        {
            if (File.Exists(path)) normal = JsonSerializer.Deserialize<Placement>(File.ReadAllText(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            warning("Could not restore window placement: " + error.Message);
        }

        if (normal is { } saved && double.IsFinite(saved.Width) && double.IsFinite(saved.Height)
            && saved.Width > 0 && saved.Height > 0)
        {
            var point = new PixelPoint(saved.X, saved.Y);
            var screen = window.Screens.ScreenFromPoint(point) ?? window.Screens.Primary;
            if (screen is not null)
            {
                var area = screen.WorkingArea;
                double scale = screen.Scaling;
                window.Width = Math.Max(window.MinWidth, Math.Min(saved.Width, area.Width / scale));
                window.Height = Math.Max(window.MinHeight, Math.Min(saved.Height, area.Height / scale));
                int right = Math.Max(area.X, area.Right - (int)(window.Width * scale));
                int bottom = Math.Max(area.Y, area.Bottom - (int)(window.Height * scale));
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(Math.Clamp(saved.X, area.X, right), Math.Clamp(saved.Y, area.Y, bottom));
            }
            if (saved.Maximized) window.WindowState = WindowState.Maximized;
        }
        else normal = null;
        bool maximized = window.WindowState == WindowState.Maximized;
        window.PropertyChanged += (_, change) =>
        {
            if (change.Property == Window.WindowStateProperty && window.WindowState != WindowState.Minimized)
                maximized = window.WindowState == WindowState.Maximized;
        };

        void Capture()
        {
            if (!opened || window.WindowState != WindowState.Normal) return;
            normal = new Placement(window.Width, window.Height, window.Position.X, window.Position.Y, false);
        }

        window.Opened += (_, _) => { opened = true; Capture(); };
        window.PositionChanged += (_, _) => Capture();
        window.SizeChanged += (_, _) => Capture();
        window.Closing += (_, _) =>
        {
            Capture();
            if (normal is null) return;
            var placement = normal with { Maximized = maximized };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(placement));
                File.Move(path + ".tmp", path, true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                warning("Could not save window placement: " + error.Message);
            }
        };
    }
}
