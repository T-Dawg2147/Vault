using System.Security.Cryptography;
using Vault.Security.Encryption;

namespace Vault.Security.KeyProtection;

/// <summary>
/// Stores the raw master key in a file readable only by the current user
/// (mode 0600). Used on Linux and as the macOS fallback when the Keychain is
/// unavailable. On Windows use <see cref="DpapiMasterKeyStore"/> instead.
/// </summary>
public sealed class FileMasterKeyStore : IMasterKeyStore
{
    private readonly string _keyFilePath;

    public FileMasterKeyStore(string keyFilePath)
    {
        if (string.IsNullOrWhiteSpace(keyFilePath))
            throw new ArgumentException("Key file path is required.", nameof(keyFilePath));

        _keyFilePath = keyFilePath;
    }

    /// <summary>Returns the key from the file, or null when no key file exists.</summary>
    public byte[]? TryLoad()
    {
        if (!File.Exists(_keyFilePath))
            return null;

        var key = File.ReadAllBytes(_keyFilePath);
        if (key.Length != AesGcmEncryptionService.KeySize)
            throw new InvalidOperationException("The master key file is corrupt.");

        EnsureRestrictivePermissions();
        return key;
    }

    public void Save(byte[] key)
    {
        var directory = Path.GetDirectoryName(_keyFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Create the temp file with restrictive permissions from the start, then move it into place.
        var tempPath = _keyFilePath + ".tmp";
        File.Delete(tempPath);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var stream = new FileStream(tempPath, options))
            stream.Write(key);

        File.Move(tempPath, _keyFilePath, overwrite: true);
        EnsureRestrictivePermissions();
    }

    public byte[] GetOrCreateMasterKey()
    {
        var existing = TryLoad();
        if (existing is not null)
            return existing;

        var key = AesGcmEncryptionService.GenerateMasterKey();
        Save(key);
        return key;
    }

    private void EnsureRestrictivePermissions()
    {
        if (OperatingSystem.IsWindows())
            return;

        var wanted = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        if (File.GetUnixFileMode(_keyFilePath) != wanted)
            File.SetUnixFileMode(_keyFilePath, wanted);
    }
}
