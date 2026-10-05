using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Vault.Security.Encryption;

namespace Vault.Security.KeyProtection;

/// <summary>
/// Protects the master key with DPAPI (LocalMachine scope), binding it to the
/// machine so that every Windows user who runs the shared installation can open
/// it (per-user scope broke first-time users). Access to the key file itself must
/// be restricted with NTFS permissions. The key file on disk contains only the
/// DPAPI-protected blob — never the raw key.
///
/// First-run behavior: generates a cryptographically random 32-byte master key,
/// protects it with DPAPI, and writes the blob next to the application database.
/// </summary>
public sealed class DpapiMasterKeyStore : IMasterKeyStore
{
    private static readonly byte[] OptionalEntropy =
        "VaultMasterKey.v1"u8.ToArray();

    private readonly string _keyFilePath;

    public DpapiMasterKeyStore(string keyFilePath)
    {
        if (string.IsNullOrWhiteSpace(keyFilePath))
            throw new ArgumentException("Key file path is required.", nameof(keyFilePath));

        _keyFilePath = keyFilePath;
    }

    public byte[] GetOrCreateMasterKey()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException(
                "DPAPI key protection requires Windows. Use a platform-appropriate IMasterKeyStore for development.");

        if (File.Exists(_keyFilePath))
            return LoadExisting();

        var key = AesGcmEncryptionService.GenerateMasterKey();
        var protectedBlob = Protect(key);

        var directory = Path.GetDirectoryName(_keyFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write to a temp file then move to avoid a partially written key file.
        var tempPath = _keyFilePath + ".tmp";
        File.WriteAllBytes(tempPath, protectedBlob);
        File.Move(tempPath, _keyFilePath, overwrite: true);

        return key;
    }

    private byte[] LoadExisting()
    {
        var blob = File.ReadAllBytes(_keyFilePath);
        try
        {
            return ProtectedData.Unprotect(blob, OptionalEntropy, DataProtectionScope.LocalMachine);
        }
        catch (CryptographicException)
        {
            // Legacy key file protected with CurrentUser scope: migrate it to machine scope.
        }

        var key = Unprotect(blob, DataProtectionScope.CurrentUser);
        var tempPath = _keyFilePath + ".tmp";
        File.WriteAllBytes(tempPath, ProtectedData.Protect(key, OptionalEntropy, DataProtectionScope.LocalMachine));
        File.Move(tempPath, _keyFilePath, overwrite: true);
        return key;
    }

    private static byte[] Protect(byte[] key)
    {
        var protectedBlob = ProtectedData.Protect(key, OptionalEntropy, DataProtectionScope.LocalMachine);
        CryptographicOperations.ZeroMemory(key);
        return protectedBlob;
    }

    private static byte[] Unprotect(byte[] protectedBlob, DataProtectionScope scope)
    {
        try
        {
            return ProtectedData.Unprotect(protectedBlob, OptionalEntropy, scope);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException(
                "The master key could not be unprotected. It may belong to a different machine.",
                ex);
        }
    }
}
