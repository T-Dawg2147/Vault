using Vault.Security.KeyProtection;

namespace Vault.Tests.Security;

public class MasterKeyStoreTests
{
    private static string TempKeyPath()
        => Path.Combine(Path.GetTempPath(), $"vault-key-{Guid.NewGuid():N}", "master.key");

    private sealed class FakeKeychain : IKeychainCommandRunner
    {
        public string? Stored { get; private set; }
        public bool Unavailable { get; set; }

        public KeychainCommandResult Run(IReadOnlyList<string> arguments)
        {
            if (Unavailable) throw new System.ComponentModel.Win32Exception("no security tool");
            if (arguments[0] == "find-generic-password")
                return Stored is null ? new(44, "") : new(0, Stored + "\n");
            Stored = arguments[arguments.ToList().IndexOf("-w") + 1];
            return new(0, "");
        }
    }

    [Fact]
    public void Factory_SelectsImplementationForPlatform()
    {
        var store = MasterKeyStoreFactory.Create(TempKeyPath());

        if (OperatingSystem.IsWindows()) Assert.IsType<DpapiMasterKeyStore>(store);
        else if (OperatingSystem.IsMacOS()) Assert.IsType<MacOSKeychainMasterKeyStore>(store);
        else Assert.IsType<FileMasterKeyStore>(store);
    }

    [Fact]
    public void FileStore_CreatesStableKeyWithRestrictivePermissions()
    {
        var path = TempKeyPath();
        var store = new FileMasterKeyStore(path);

        var first = store.GetOrCreateMasterKey();
        var second = store.GetOrCreateMasterKey();

        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public void Keychain_StoresKeyInKeychainNotOnDisk()
    {
        var path = TempKeyPath();
        var keychain = new FakeKeychain();
        var store = new MacOSKeychainMasterKeyStore(path, keychain);

        var first = store.GetOrCreateMasterKey();
        var second = store.GetOrCreateMasterKey();

        Assert.Equal(first, second);
        Assert.Equal(Convert.ToHexString(first), keychain.Stored);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Keychain_FallsBackToFileWhenUnavailable()
    {
        var path = TempKeyPath();
        var keychain = new FakeKeychain { Unavailable = true };
        var store = new MacOSKeychainMasterKeyStore(path, keychain);

        var first = store.GetOrCreateMasterKey();

        Assert.True(File.Exists(path));
        Assert.Equal(first, store.GetOrCreateMasterKey());
    }

    [Fact]
    public void Dpapi_RoundTripsOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        var path = TempKeyPath();
        var first = new DpapiMasterKeyStore(path).GetOrCreateMasterKey();
        Assert.Equal(first, new DpapiMasterKeyStore(path).GetOrCreateMasterKey());
    }
}
