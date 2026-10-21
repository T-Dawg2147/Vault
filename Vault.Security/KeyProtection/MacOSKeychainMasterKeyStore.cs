using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Vault.Security.Encryption;

namespace Vault.Security.KeyProtection;

/// <summary>Result of running the macOS <c>security</c> command-line tool.</summary>
public readonly record struct KeychainCommandResult(int ExitCode, string StandardOutput);

/// <summary>Runs <c>security</c> with the given arguments. Throws when the tool cannot be started.</summary>
public interface IKeychainCommandRunner
{
    KeychainCommandResult Run(IReadOnlyList<string> arguments);
}

internal sealed class SecurityCliRunner : IKeychainCommandRunner
{
    public KeychainCommandResult Run(IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException("Could not start the macOS security tool.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            process.Kill();
            throw new TimeoutException("The macOS security tool timed out.");
        }
        Task.WaitAll(output, error);
        return new KeychainCommandResult(process.ExitCode, output.Result);
    }
}

/// <summary>
/// Protects the master key with the macOS login Keychain (generic password item).
/// The item is keyed by service "Vault" and an account derived from the key file path,
/// so separate data directories get separate keys. If the Keychain is unavailable the
/// key is kept in a 0600 file (<see cref="FileMasterKeyStore"/>); an existing fallback
/// file is honoured so previously created data stays readable.
/// Note: the <c>security</c> tool receives the key (hex) as a command-line argument,
/// which is briefly visible to other processes of the same user.
/// </summary>
public sealed class MacOSKeychainMasterKeyStore : IMasterKeyStore
{
    private const string ServiceName = "Vault";
    private const int ItemNotFoundExitCode = 44;

    private readonly FileMasterKeyStore _fallback;
    private readonly IKeychainCommandRunner _runner;
    private readonly string _account;

    public MacOSKeychainMasterKeyStore(string keyFilePath, IKeychainCommandRunner? runner = null)
    {
        if (string.IsNullOrWhiteSpace(keyFilePath))
            throw new ArgumentException("Key file path is required.", nameof(keyFilePath));

        _fallback = new FileMasterKeyStore(keyFilePath);
        _runner = runner ?? new SecurityCliRunner();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(keyFilePath)));
        _account = "master-key-" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }

    public byte[] GetOrCreateMasterKey()
    {
        var keychainAvailable = true;
        KeychainCommandResult? found = null;
        try
        {
            found = _runner.Run(["find-generic-password", "-s", ServiceName, "-a", _account, "-w"]);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException or IOException)
        {
            keychainAvailable = false;
        }

        if (found is { ExitCode: 0 } hit)
        {
            return TryParse(hit.StandardOutput)
                ?? throw new InvalidOperationException("The master key stored in the Keychain is corrupt.");
        }
        if (found is { } miss && miss.ExitCode != ItemNotFoundExitCode)
            keychainAvailable = false;

        var existing = _fallback.TryLoad();
        if (existing is not null)
            return existing;

        var created = AesGcmEncryptionService.GenerateMasterKey();
        if (keychainAvailable && TryStoreInKeychain(created))
            return created;

        _fallback.Save(created);
        return created;
    }

    private bool TryStoreInKeychain(byte[] key)
    {
        try
        {
            var hex = Convert.ToHexString(key);
            var result = _runner.Run(["add-generic-password", "-s", ServiceName, "-a", _account, "-w", hex, "-U"]);
            return result.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException or IOException)
        {
            return false;
        }
    }

    private static byte[]? TryParse(string output)
    {
        var text = output.Trim();
        if (text.Length != AesGcmEncryptionService.KeySize * 2)
            return null;
        try
        {
            return Convert.FromHexString(text);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
