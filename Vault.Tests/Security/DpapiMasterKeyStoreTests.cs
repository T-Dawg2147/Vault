using Vault.Security.KeyProtection;

namespace Vault.Tests.Security;

public class DpapiMasterKeyStoreTests
{
    [Fact]
    public void GetOrCreateMasterKey_ReturnsSameKeyAfterReopeningOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = Path.Combine(Path.GetTempPath(), $"vault-key-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "master.key");

        try
        {
            var first = new DpapiMasterKeyStore(path).GetOrCreateMasterKey();
            var second = new DpapiMasterKeyStore(path).GetOrCreateMasterKey();

            Assert.Equal(32, first.Length);
            Assert.Equal(first, second);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
