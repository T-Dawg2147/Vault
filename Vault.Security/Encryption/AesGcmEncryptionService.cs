using System.Security.Cryptography;
using System.Text;
using Vault.Core.Interfaces;

namespace Vault.Security.Encryption;

/// <summary>
/// AES-256-GCM authenticated encryption.
/// Payload layout: [12-byte nonce][16-byte tag][ciphertext].
/// Each record is encrypted with a fresh random nonce using the master key.
/// Plaintext is never logged and never appears in exception messages.
/// </summary>
public sealed class AesGcmEncryptionService : IEncryptionService, IDisposable
{
    public const int NonceSize = 12;
    public const int TagSize = 16;
    public const int KeySize = 32;

    private readonly byte[] _key;
    private bool _disposed;

    public AesGcmEncryptionService(byte[] masterKey)
    {
        if (masterKey is null || masterKey.Length != KeySize)
            throw new ArgumentException($"Master key must be {KeySize} bytes.", nameof(masterKey));

        _key = new byte[KeySize];
        Array.Copy(masterKey, _key, KeySize);
    }

    public byte[] Encrypt(string plaintext)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var tag = new byte[TagSize];
        var ciphertext = new byte[plaintextBytes.Length];

        using (var aes = new AesGcm(_key, TagSize))
        {
            aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }

        CryptographicOperations.ZeroMemory(plaintextBytes);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, payload, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, payload, NonceSize + TagSize, ciphertext.Length);
        return payload;
    }

    public string Decrypt(byte[] payload)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(payload);

        if (payload.Length < NonceSize + TagSize)
            throw new CryptographicException("Encrypted payload is malformed.");

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize + TagSize);
        var plaintextBytes = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);
            return Encoding.UTF8.GetString(plaintextBytes);
        }
        catch (CryptographicException)
        {
            // Do not include payload data in the error — it may be attacker controlled.
            throw new CryptographicException("Decryption failed: data is corrupted or tampered with.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public static byte[] GenerateMasterKey() => RandomNumberGenerator.GetBytes(KeySize);

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AesGcmEncryptionService));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_key);
            _disposed = true;
        }
    }
}
