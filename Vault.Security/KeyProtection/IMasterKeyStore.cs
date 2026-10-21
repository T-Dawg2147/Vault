namespace Vault.Security.KeyProtection;

/// <summary>
/// Loads or creates the master encryption key, protected at rest by a
/// platform mechanism (DPAPI on Windows, Keychain on macOS, a 0600 file elsewhere). The unprotected key must never
/// be written to disk, config, or logs.
/// </summary>
public interface IMasterKeyStore
{
    /// <summary>Returns the 32-byte master key, creating and protecting a new one on first run.</summary>
    byte[] GetOrCreateMasterKey();
}
