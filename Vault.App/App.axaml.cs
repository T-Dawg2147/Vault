using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Vault.App.Services;
using Vault.App.ViewModels;
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

namespace Vault.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => DisposeServices();
            _ = StartAsync(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task StartAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        IDialogService dialogs = new DialogService();
        try
        {
            _services = ConfigureServices();
            var user = AuthenticateCurrentUser(_services);

            if (user is null)
            {
                await dialogs.ShowMessageAsync("Access denied",
                    "Your account is not registered in the vault or has been deactivated.\n\n" +
                    "Contact a vault administrator to be granted access.");
                desktop.Shutdown(1);
                return;
            }

            _services.GetRequiredService<IAppSession>().SignIn(user);

            var mainWindow = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            _services.GetRequiredService<INavigationService>().NavigateTo<DashboardViewModel>();
        }
        catch (Exception ex)
        {
            // Never include secret material in startup errors; message text comes from safe sources only.
            await dialogs.ShowMessageAsync("Startup error", $"The vault failed to start.\n\n{ex.Message}");
            desktop.Shutdown(1);
        }
    }

    /// <summary>
    /// Maps the current operating system identity to an application user. On the very first
    /// run (empty user table) the current user is provisioned as the initial admin.
    /// </summary>
    private static User? AuthenticateCurrentUser(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IRepository>();
        var identity = services.GetRequiredService<ICurrentUserIdentityService>();
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
                : "Unregistered or inactive identity attempted to sign in.");

        return user;
    }

    private static ServiceProvider ConfigureServices()
    {
        var dataDirectory = AppDataPaths.GetDataDirectory();
        var databasePath = Path.Combine(dataDirectory, "vault.db");
        var keyFilePath = Path.Combine(dataDirectory, "master.key");

        var services = new ServiceCollection();

        // Persistence
        var repository = new SqliteVaultRepository(databasePath);
        repository.InitializeSchema();
        services.AddSingleton<IRepository>(repository);

        // Security
        services.AddSingleton<IMasterKeyStore>(_ => MasterKeyStoreFactory.Create(keyFilePath));
        services.AddSingleton<IEncryptionService>(sp =>
            new AesGcmEncryptionService(sp.GetRequiredService<IMasterKeyStore>().GetOrCreateMasterKey()));
        services.AddSingleton<IAuthorizationService, AuthorizationService>();

        // Application services
        services.AddSingleton(_ => CurrentUserIdentityServiceFactory.Create());
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<ICredentialService, CredentialService>();
        services.AddSingleton<IImportService, LegacyFileImporter>();

        // UI services
        services.AddSingleton<IAppSession, AppSession>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISecureClipboard, SecureClipboard>();
        services.AddSingleton<IDialogService, DialogService>();

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

    private void DisposeServices()
    {
        if (_services is null)
            return;

        if (_services.GetService<IEncryptionService>() is IDisposable disposable)
            disposable.Dispose();
        _services.Dispose();
        _services = null;
    }
}
