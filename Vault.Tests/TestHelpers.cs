using Vault.Core.Enums;
using Vault.Core.Models;
using Vault.Infrastructure.Persistence;
using Vault.Security.Encryption;

namespace Vault.Tests;

/// <summary>
/// Shared fixtures: an in-file SQLite database per test plus a random-key encryption service.
/// </summary>
public static class TestHelpers
{
    public static SqliteVaultRepository CreateRepository()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vault-test-{Guid.NewGuid():N}.db");
        var repository = new SqliteVaultRepository(path);
        repository.InitializeSchema();
        return repository;
    }

    public static AesGcmEncryptionService CreateEncryption()
        => new(AesGcmEncryptionService.GenerateMasterKey());

    private static int _sidCounter;

    public static User MakeUser(int id, UserRole role, bool isActive = true)
    {
        var unique = id != 0 ? id : System.Threading.Interlocked.Increment(ref _sidCounter);
        return new User
        {
            Id = id,
            WindowsSid = $"S-1-5-21-1000-{unique}-{Guid.NewGuid():N}",
            Domain = "CORP",
            Username = $"user{unique}-{Guid.NewGuid():N}",
            DisplayName = $"User {unique}",
            Role = role,
            IsActive = isActive
        };
    }
}
