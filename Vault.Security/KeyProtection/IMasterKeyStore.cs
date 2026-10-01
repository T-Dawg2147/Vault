namespace Vault.Security.KeyProtection;

/// <summary>
/// Loads or creates the master encryption key, protected at rest by a
/// Windows-bound mechanism (DPAPI on Windows). The unprotected key must never
/// be written to disk, config, or logs.
/// </summary>
public interface IMasterKeyStore
{
    /// <summary>Returns the 32-byte master key, creating and protecting a new one on first run.</summary>
    byte[] GetOrCreateMasterKey();
}
