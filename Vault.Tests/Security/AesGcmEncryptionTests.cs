using System.Security.Cryptography;
using System.Text;
using Vault.Security.Encryption;
using Xunit;

namespace Vault.Tests.Security;

public class AesGcmEncryptionTests
{
    [Fact]
    public void EncryptDecrypt_RoundTrips()
    {
        using var encryption = TestHelpers.CreateEncryption();
        const string secret = "Sup3rSecret!P@ssw0rd";

        var ciphertext = encryption.Encrypt(secret);
        var decrypted = encryption.Decrypt(ciphertext);

        Assert.Equal(secret, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesAuthenticatedPayload_WithNonceAndTag()
    {
        using var encryption = TestHelpers.CreateEncryption();

        var ciphertext = encryption.Encrypt("data");

        var plaintextLength = Encoding.UTF8.GetByteCount("data");
        Assert.Equal(AesGcmEncryptionService.NonceSize + AesGcmEncryptionService.TagSize + plaintextLength,
            ciphertext.Length);
    }

    [Fact]
    public void Encrypt_UsesRandomNonce_PerRecord()
    {
        using var encryption = TestHelpers.CreateEncryption();

        var first = encryption.Encrypt("same-value");
        var second = encryption.Encrypt("same-value");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Decrypt_RejectsTamperedCiphertext()
    {
        using var encryption = TestHelpers.CreateEncryption();
        var ciphertext = encryption.Encrypt("sensitive");
        ciphertext[^1] ^= 0xFF; // flip a bit

        Assert.Throws<CryptographicException>(() => encryption.Decrypt(ciphertext));
    }

    [Fact]
    public void Decrypt_RejectsMalformedPayload()
    {
        using var encryption = TestHelpers.CreateEncryption();

        Assert.Throws<CryptographicException>(() => encryption.Decrypt(new byte[4]));
    }

    [Fact]
    public void Decrypt_WithDifferentKey_Fails()
    {
        using var first = TestHelpers.CreateEncryption();
        using var second = TestHelpers.CreateEncryption();
        var ciphertext = first.Encrypt("secret");

        Assert.Throws<CryptographicException>(() => second.Decrypt(ciphertext));
    }

    [Fact]
    public void Constructor_RejectsWrongKeySize()
    {
        Assert.Throws<ArgumentException>(() => new AesGcmEncryptionService(new byte[16]));
    }

    [Fact]
    public void RoundTrip_HandlesUnicodeAndLongValues()
    {
        using var encryption = TestHelpers.CreateEncryption();
        var secret = new string('x', 5000) + " — pässwörd 🔒";

        var decrypted = encryption.Decrypt(encryption.Encrypt(secret));

        Assert.Equal(secret, decrypted);
    }
}
