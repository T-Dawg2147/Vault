namespace Vault.Security.KeyProtection;

/// <summary>Selects the <see cref="IMasterKeyStore"/> implementation for the running operating system.</summary>
public static class MasterKeyStoreFactory
{
    public static IMasterKeyStore Create(string keyFilePath)
    {
        if (OperatingSystem.IsWindows())
            return new DpapiMasterKeyStore(keyFilePath);
        if (OperatingSystem.IsMacOS())
            return new MacOSKeychainMasterKeyStore(keyFilePath);
        return new FileMasterKeyStore(keyFilePath);
    }
}
