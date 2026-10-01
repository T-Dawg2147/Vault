using Vault.Core.Models;

namespace Vault.App.Wpf.Services;

/// <summary>
/// Holds the authenticated application user for the running session.
/// Populated once at startup after the Windows identity check succeeds.
/// </summary>
public interface IAppSession
{
    User CurrentUser { get; }

    bool IsAdmin { get; }

    void SignIn(User user);
}

public sealed class AppSession : IAppSession
{
    private User? _currentUser;

    public User CurrentUser =>
        _currentUser ?? throw new InvalidOperationException("No user is signed in.");

    public bool IsAdmin => _currentUser?.Role == Core.Enums.UserRole.Admin;

    public void SignIn(User user)
    {
        _currentUser = user ?? throw new ArgumentNullException(nameof(user));
    }
}
