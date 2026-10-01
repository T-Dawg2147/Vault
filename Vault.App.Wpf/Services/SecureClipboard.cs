using System.Windows;
using System.Windows.Threading;

namespace Vault.App.Wpf.Services;

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

    private readonly Dispatcher _dispatcher;
    private DispatcherTimer? _clearTimer;

    public SecureClipboard()
    {
        _dispatcher = Application.Current.Dispatcher;
    }

    public void CopySecret(string secret, TimeSpan? clearAfter = null)
    {
        ArgumentNullException.ThrowIfNull(secret);

        Clipboard.SetText(secret);
        ScheduleClear(clearAfter ?? DefaultClearAfter);
    }

    public void CopyText(string text)
    {
        Clipboard.SetText(text ?? string.Empty);
    }

    private void ScheduleClear(TimeSpan after)
    {
        _clearTimer?.Stop();
        _clearTimer = new DispatcherTimer { Interval = after };
        _clearTimer.Tick += (_, _) =>
        {
            _clearTimer.Stop();
            try
            {
                Clipboard.Clear();
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                // Clipboard may be held by another process; clearing is best-effort.
            }
        };
        _clearTimer.Start();
    }
}
