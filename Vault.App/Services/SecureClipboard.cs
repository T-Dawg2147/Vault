using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace Vault.App.Services;

/// <summary>
/// Clipboard helper that auto-clears copied secrets after a short timeout
/// so passwords do not linger in the clipboard history.
/// </summary>
public interface ISecureClipboard
{
    void CopySecret(string secret, TimeSpan? clearAfter = null);

    void CopyText(string text);
}

public sealed class SecureClipboard : ISecureClipboard
{
    private static readonly TimeSpan DefaultClearAfter = TimeSpan.FromSeconds(20);

    private DispatcherTimer? _clearTimer;

    private static IClipboard? Clipboard =>
        ((Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow
            is { } window ? TopLevel.GetTopLevel(window) : null)?.Clipboard;

    public void CopySecret(string secret, TimeSpan? clearAfter = null)
    {
        ArgumentNullException.ThrowIfNull(secret);

        SetText(secret);
        ScheduleClear(clearAfter ?? DefaultClearAfter);
    }

    public void CopyText(string text) => SetText(text ?? string.Empty);

    private static void SetText(string text)
    {
        var clipboard = Clipboard ?? throw new InvalidOperationException("The clipboard is not available.");
        _ = ObserveAsync(clipboard.SetTextAsync(text));
    }

    private void ScheduleClear(TimeSpan after)
    {
        _clearTimer?.Stop();
        var timer = new DispatcherTimer { Interval = after };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (Clipboard is { } clipboard)
                _ = ObserveAsync(clipboard.ClearAsync());
        };
        _clearTimer = timer;
        timer.Start();
    }

    private static async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception)
        {
            // Clipboard may be held by another process; copying/clearing is best-effort.
        }
    }
}
