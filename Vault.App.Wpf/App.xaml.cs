using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Vault.App.Wpf.Services;
using Vault.App.Wpf.ViewModels;
using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Infrastructure.Identity;
using Vault.Infrastructure.Import;
using Vault.Infrastructure.Persistence;
using Vault.Infrastructure.Services;
using Vault.Security.Authorization;
using Vault.Security.Encryption;
using Vault.Security.KeyProtection;

namespace Vault.App.Wpf;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _services = ConfigureServices();
            var user = AuthenticateCurrentUser(_services);

            if (user is null)
            {
                MessageBox.Show(
                    "Your Windows account is not registered in the vault or has been deactivated.\n\n" +
                    "Contact a vault administrator to be granted access.",
                    "Access denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown(1);
                return;
            }

            _services.GetRequiredService<IAppSession>().SignIn(user);

            var mainWindow = _services.GetRequiredService<MainWindow>();
            mainWindow.Show();
            _services.GetRequiredService<INavigationService>().NavigateTo<DashboardViewModel>();
        }
        catch (Exception ex)
        {
            // Never include secret material in startup errors; message text comes from safe sources only.
            MessageBox.Show($"The vault failed to start.\n\n{ex.Message}",
                "Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Maps the current Windows identity to an application user. On the very first
    /// run (empty user table) the current user is provisioned as the initial admin.
    /// </summary>
    private static User? AuthenticateCurrentUser(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IRepository>();
        var identity = services.GetRequiredService<IWindowsIdentityService>();
        var audit = services.GetRequiredService<IAuditService>();
        var userService = services.GetRequiredService<IUserService>();

        User? user;
        if (repository.CountUsers() == 0)
        {
            user = AdminProvisioner.EnsureInitialAdmin(repository, identity, audit);
        }
        else
        {
            user = userService.GetCurrentUser();
        }

        audit.Log(user is not null ? AuditAction.LoginSucceeded : AuditAction.LoginDenied,
            user?.Id, "User", user?.Id.ToString(),
            user is not null
                ? $"User '{user.Username}' signed in."
                : "Unregistered or inactive Windows identity attempted to sign in.");

        return user;
    }

    private static ServiceProvider ConfigureServices()
    {
        var dataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vault");
        var databasePath = Path.Combine(dataDirectory, "vault.db");
        var keyFilePath = Path.Combine(dataDirectory, "master.key");

        var services = new ServiceCollection();

        // Persistence
        var repository = new SqliteVaultRepository(databasePath);
        repository.InitializeSchema();
        services.AddSingleton<IRepository>(repository);

        // Security
        services.AddSingleton<IMasterKeyStore>(_ => new DpapiMasterKeyStore(keyFilePath));
        services.AddSingleton<IEncryptionService>(sp =>
            new AesGcmEncryptionService(sp.GetRequiredService<IMasterKeyStore>().GetOrCreateMasterKey()));
        services.AddSingleton<IAuthorizationService, AuthorizationService>();

        // Application services
        services.AddSingleton<IWindowsIdentityService, WindowsIdentityService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<IImportService, LegacyFileImporter>();

        // UI services
        services.AddSingleton<IAppSession, AppSession>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISecureClipboard, SecureClipboard>();

        // Shell + view models
        services.AddSingleton<MainWindow>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<VaultListViewModel>();
        services.AddTransient<CredentialListViewModel>();
        services.AddTransient<CredentialEditViewModel>();
        services.AddTransient<AdminUsersViewModel>();
        services.AddTransient<AdminVaultAccessViewModel>();
        services.AddTransient<AuditLogViewModel>();
        services.AddTransient<ImportViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            if (_services.GetService<IEncryptionService>() is IDisposable disposable)
                disposable.Dispose();
            _services.Dispose();
        }

        base.OnExit(e);
    }
}
