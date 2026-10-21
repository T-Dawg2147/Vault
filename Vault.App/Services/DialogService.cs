using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace Vault.App.Services;

/// <summary>Abstraction over message boxes and file pickers so view models stay UI-framework free.</summary>
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);

    /// <summary>Returns the selected local file path, or null when cancelled.</summary>
    Task<string?> PickFileAsync(string title, IReadOnlyList<string> filePatterns);
}

public sealed class DialogService : IDialogService
{
    private static Window? MainWindow =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public Task ShowMessageAsync(string title, string message)
    {
        var completion = new TaskCompletionSource();
        var window = CreateMessageWindow(title, message);
        window.Closed += (_, _) => completion.TrySetResult();

        var owner = MainWindow;
        if (owner is { IsVisible: true })
            _ = window.ShowDialog(owner);
        else
            window.Show();

        return completion.Task;
    }

    public async Task<string?> PickFileAsync(string title, IReadOnlyList<string> filePatterns)
    {
        var owner = MainWindow;
        if (owner is null)
            return null;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Password files") { Patterns = filePatterns },
                FilePickerFileTypes.All
            ]
        });

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static Window CreateMessageWindow(string title, string message)
    {
        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80 };
        var window = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    ok
                }
            }
        };
        ok.Click += (_, _) => window.Close();
        return window;
    }
}
