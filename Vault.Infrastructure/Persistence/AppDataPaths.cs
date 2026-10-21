namespace Vault.Infrastructure.Persistence;

/// <summary>Resolves the per-platform application data directory.</summary>
public static class AppDataPaths
{
    /// <summary>
    /// Windows: the legacy "Vault" folder next to the executable when it already holds a database
    /// (keeps existing installs working), otherwise %LOCALAPPDATA%\Vault.
    /// macOS: ~/Library/Application Support/Vault. Linux: $XDG_DATA_HOME or ~/.local/share, plus "Vault".
    /// </summary>
    public static string GetDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var legacy = Path.Combine(AppContext.BaseDirectory, "Vault");
            if (File.Exists(Path.Combine(legacy, "vault.db")))
                return legacy;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vault");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support", "Vault");

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vault");
    }
}
