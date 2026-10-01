using System.Text.Json;
using Microsoft.Data.Sqlite;
using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;

namespace Vault.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed repository. All queries use parameters — no string-built SQL.
/// Secret fields are stored only as encrypted blobs. Each call opens a short-lived
/// connection; readers are materialized before returning.
/// </summary>
public sealed class SqliteVaultRepository : IRepository
{
    private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss.fffffff";

    private readonly string _connectionString;

    public SqliteVaultRepository(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path is required.", nameof(databasePath));

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public void InitializeSchema()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                WindowsSid TEXT NOT NULL UNIQUE,
                Domain TEXT NOT NULL DEFAULT '',
                Username TEXT NOT NULL,
                DisplayName TEXT NOT NULL DEFAULT '',
                Role INTEGER NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Vaults (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Description TEXT NOT NULL DEFAULT '',
                CreatedByUserId INTEGER NOT NULL REFERENCES Users(Id),
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsArchived INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS VaultAccess (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VaultId INTEGER NOT NULL REFERENCES Vaults(Id),
                UserId INTEGER NOT NULL REFERENCES Users(Id),
                Permission INTEGER NOT NULL,
                GrantedByUserId INTEGER NOT NULL REFERENCES Users(Id),
                GrantedAt TEXT NOT NULL,
                UNIQUE (VaultId, UserId)
            );

            CREATE TABLE IF NOT EXISTS Credentials (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                VaultId INTEGER NOT NULL REFERENCES Vaults(Id),
                Label TEXT NOT NULL,
                UsernameOrEmail TEXT NOT NULL DEFAULT '',
                PasswordEncrypted BLOB NOT NULL,
                NotesEncrypted BLOB NULL,
                Url TEXT NULL,
                Tags TEXT NOT NULL DEFAULT '[]',
                RotationDueAt TEXT NULL,
                CreatedByUserId INTEGER NOT NULL REFERENCES Users(Id),
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastAccessedAt TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_Credentials_Vault ON Credentials(VaultId, IsDeleted);

            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NULL,
                Action INTEGER NOT NULL,
                TargetType TEXT NULL,
                TargetId TEXT NULL,
                Timestamp TEXT NOT NULL,
                Details TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_AuditLog_Timestamp ON AuditLog(Timestamp);
        ";
        command.ExecuteNonQuery();
    }

    // ---------- Users ----------

    public User? FindUserBySid(string windowsSid)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE WindowsSid = $sid";
        command.Parameters.AddWithValue("$sid", windowsSid);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    public User? FindUserById(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    public IReadOnlyList<User> GetAllUsers()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users ORDER BY Username";
        using var reader = command.ExecuteReader();
        var users = new List<User>();
        while (reader.Read())
            users.Add(MapUser(reader));
        return users;
    }

    public int CountUsers()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    public User InsertUser(User user)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Users (WindowsSid, Domain, Username, DisplayName, Role, IsActive, CreatedAt, UpdatedAt)
            VALUES ($sid, $domain, $username, $displayName, $role, $isActive, $createdAt, $updatedAt);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$sid", user.WindowsSid);
        command.Parameters.AddWithValue("$domain", user.Domain);
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$displayName", user.DisplayName);
        command.Parameters.AddWithValue("$role", (int)user.Role);
        command.Parameters.AddWithValue("$isActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", Format(user.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", Format(user.UpdatedAt));
        user.Id = Convert.ToInt32(command.ExecuteScalar());
        return user;
    }

    public void UpdateUser(User user)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Users
            SET Domain = $domain, Username = $username, DisplayName = $displayName,
                Role = $role, IsActive = $isActive, UpdatedAt = $updatedAt
            WHERE Id = $id";
        command.Parameters.AddWithValue("$id", user.Id);
        command.Parameters.AddWithValue("$domain", user.Domain);
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$displayName", user.DisplayName);
        command.Parameters.AddWithValue("$role", (int)user.Role);
        command.Parameters.AddWithValue("$isActive", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", Format(DateTime.UtcNow));
        command.ExecuteNonQuery();
    }

    // ---------- Vaults ----------

    public VaultGroup? FindVaultById(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Vaults WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapVault(reader) : null;
    }

    public IReadOnlyList<VaultGroup> GetAllVaults(bool includeArchived = false)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = includeArchived
            ? "SELECT * FROM Vaults ORDER BY Name"
            : "SELECT * FROM Vaults WHERE IsArchived = 0 ORDER BY Name";
        using var reader = command.ExecuteReader();
        var vaults = new List<VaultGroup>();
        while (reader.Read())
            vaults.Add(MapVault(reader));
        return vaults;
    }

    public VaultGroup InsertVault(VaultGroup vault)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Vaults (Name, Description, CreatedByUserId, CreatedAt, UpdatedAt, IsArchived)
            VALUES ($name, $description, $createdBy, $createdAt, $updatedAt, $isArchived);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", vault.Name);
        command.Parameters.AddWithValue("$description", vault.Description);
        command.Parameters.AddWithValue("$createdBy", vault.CreatedByUserId);
        command.Parameters.AddWithValue("$createdAt", Format(vault.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", Format(vault.UpdatedAt));
        command.Parameters.AddWithValue("$isArchived", vault.IsArchived ? 1 : 0);
        vault.Id = Convert.ToInt32(command.ExecuteScalar());
        return vault;
    }

    public void UpdateVault(VaultGroup vault)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Vaults
            SET Name = $name, Description = $description, IsArchived = $isArchived, UpdatedAt = $updatedAt
            WHERE Id = $id";
        command.Parameters.AddWithValue("$id", vault.Id);
        command.Parameters.AddWithValue("$name", vault.Name);
        command.Parameters.AddWithValue("$description", vault.Description);
        command.Parameters.AddWithValue("$isArchived", vault.IsArchived ? 1 : 0);
        command.Parameters.AddWithValue("$updatedAt", Format(DateTime.UtcNow));
        command.ExecuteNonQuery();
    }

    // ---------- Vault access ----------

    public IReadOnlyList<VaultAccess> GetAccessForVault(int vaultId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM VaultAccess WHERE VaultId = $vaultId";
        command.Parameters.AddWithValue("$vaultId", vaultId);
        using var reader = command.ExecuteReader();
        var list = new List<VaultAccess>();
        while (reader.Read())
            list.Add(MapAccess(reader));
        return list;
    }

    public IReadOnlyList<VaultAccess> GetAccessForUser(int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM VaultAccess WHERE UserId = $userId";
        command.Parameters.AddWithValue("$userId", userId);
        using var reader = command.ExecuteReader();
        var list = new List<VaultAccess>();
        while (reader.Read())
            list.Add(MapAccess(reader));
        return list;
    }

    public VaultAccess? FindAccess(int vaultId, int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM VaultAccess WHERE VaultId = $vaultId AND UserId = $userId";
        command.Parameters.AddWithValue("$vaultId", vaultId);
        command.Parameters.AddWithValue("$userId", userId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapAccess(reader) : null;
    }

    public VaultAccess InsertAccess(VaultAccess access)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO VaultAccess (VaultId, UserId, Permission, GrantedByUserId, GrantedAt)
            VALUES ($vaultId, $userId, $permission, $grantedBy, $grantedAt);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$vaultId", access.VaultId);
        command.Parameters.AddWithValue("$userId", access.UserId);
        command.Parameters.AddWithValue("$permission", (int)access.Permission);
        command.Parameters.AddWithValue("$grantedBy", access.GrantedByUserId);
        command.Parameters.AddWithValue("$grantedAt", Format(access.GrantedAt));
        access.Id = Convert.ToInt32(command.ExecuteScalar());
        return access;
    }

    public void UpdateAccess(VaultAccess access)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE VaultAccess SET Permission = $permission WHERE Id = $id";
        command.Parameters.AddWithValue("$id", access.Id);
        command.Parameters.AddWithValue("$permission", (int)access.Permission);
        command.ExecuteNonQuery();
    }

    public void DeleteAccess(int vaultId, int userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM VaultAccess WHERE VaultId = $vaultId AND UserId = $userId";
        command.Parameters.AddWithValue("$vaultId", vaultId);
        command.Parameters.AddWithValue("$userId", userId);
        command.ExecuteNonQuery();
    }

    // ---------- Credentials ----------

    public Credential? FindCredentialById(int id)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Credentials WHERE Id = $id AND IsDeleted = 0";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read() ? MapCredential(reader) : null;
    }

    public IReadOnlyList<Credential> GetCredentialsByVault(int vaultId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Credentials WHERE VaultId = $vaultId AND IsDeleted = 0 ORDER BY Label";
        command.Parameters.AddWithValue("$vaultId", vaultId);
        using var reader = command.ExecuteReader();
        var list = new List<Credential>();
        while (reader.Read())
            list.Add(MapCredential(reader));
        return list;
    }

    public IReadOnlyList<Credential> GetCredentialsByVaults(IEnumerable<int> vaultIds)
    {
        var ids = vaultIds.Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<Credential>();

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        var parameterNames = new List<string>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            var name = "$id" + i;
            command.Parameters.AddWithValue(name, ids[i]);
            parameterNames.Add(name);
        }

        command.CommandText =
            $"SELECT * FROM Credentials WHERE VaultId IN ({string.Join(", ", parameterNames)}) AND IsDeleted = 0 ORDER BY Label";

        using var reader = command.ExecuteReader();
        var list = new List<Credential>();
        while (reader.Read())
            list.Add(MapCredential(reader));
        return list;
    }

    public Credential InsertCredential(Credential credential)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Credentials
                (VaultId, Label, UsernameOrEmail, PasswordEncrypted, NotesEncrypted, Url, Tags,
                 RotationDueAt, CreatedByUserId, CreatedAt, UpdatedAt, LastAccessedAt, IsDeleted)
            VALUES
                ($vaultId, $label, $username, $password, $notes, $url, $tags,
                 $rotation, $createdBy, $createdAt, $updatedAt, $lastAccessed, 0);
            SELECT last_insert_rowid();";
        AddCredentialParameters(command, credential);
        credential.Id = Convert.ToInt32(command.ExecuteScalar());
        return credential;
    }

    public void UpdateCredential(Credential credential)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Credentials
            SET Label = $label, UsernameOrEmail = $username, PasswordEncrypted = $password,
                NotesEncrypted = $notes, Url = $url, Tags = $tags, RotationDueAt = $rotation,
                IsDeleted = $isDeleted, UpdatedAt = $updatedAt, LastAccessedAt = $lastAccessed
            WHERE Id = $id";
        AddCredentialParameters(command, credential);
        command.Parameters.AddWithValue("$id", credential.Id);
        command.Parameters.AddWithValue("$isDeleted", credential.IsDeleted ? 1 : 0);
        command.ExecuteNonQuery();
    }

    public void TouchLastAccessed(int credentialId, DateTime accessedAtUtc)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Credentials SET LastAccessedAt = $at WHERE Id = $id";
        command.Parameters.AddWithValue("$id", credentialId);
        command.Parameters.AddWithValue("$at", Format(accessedAtUtc));
        command.ExecuteNonQuery();
    }

    // ---------- Audit ----------

    public void InsertAuditEntry(AuditLogEntry entry)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO AuditLog (UserId, Action, TargetType, TargetId, Timestamp, Details)
            VALUES ($userId, $action, $targetType, $targetId, $timestamp, $details);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$userId", entry.UserId.HasValue ? entry.UserId.Value : DBNull.Value);
        command.Parameters.AddWithValue("$action", (int)entry.Action);
        command.Parameters.AddWithValue("$targetType", (object?)entry.TargetType ?? DBNull.Value);
        command.Parameters.AddWithValue("$targetId", (object?)entry.TargetId ?? DBNull.Value);
        command.Parameters.AddWithValue("$timestamp", Format(entry.Timestamp));
        command.Parameters.AddWithValue("$details", (object?)entry.Details ?? DBNull.Value);
        entry.Id = Convert.ToInt64(command.ExecuteScalar());
    }

    public IReadOnlyList<AuditLogEntry> GetAuditEntries(int maxCount)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AuditLog ORDER BY Id DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", maxCount);
        using var reader = command.ExecuteReader();
        var list = new List<AuditLogEntry>();
        while (reader.Read())
            list.Add(MapAudit(reader));
        return list;
    }

    public IReadOnlyList<AuditLogEntry> SearchAuditEntries(string? textFilter, DateTime? fromUtc, DateTime? toUtc, int maxCount)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        var clauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(textFilter))
        {
            clauses.Add("(Details LIKE $filter OR TargetType LIKE $filter OR TargetId LIKE $filter)");
            command.Parameters.AddWithValue("$filter", "%" + EscapeLike(textFilter) + "%");
        }
        if (fromUtc.HasValue)
        {
            clauses.Add("Timestamp >= $from");
            command.Parameters.AddWithValue("$from", Format(fromUtc.Value));
        }
        if (toUtc.HasValue)
        {
            clauses.Add("Timestamp <= $to");
            command.Parameters.AddWithValue("$to", Format(toUtc.Value));
        }

        var where = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : string.Empty;
        command.CommandText = $"SELECT * FROM AuditLog {where} ORDER BY Id DESC LIMIT $limit";
        command.Parameters.AddWithValue("$limit", maxCount);

        using var reader = command.ExecuteReader();
        var list = new List<AuditLogEntry>();
        while (reader.Read())
            list.Add(MapAudit(reader));
        return list;
    }

    // ---------- Mapping helpers ----------

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static string EscapeLike(string value)
        => value.Replace("[", "[[]", StringComparison.Ordinal);

    private static string Format(DateTime value)
        => value.ToUniversalTime().ToString(DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string value)
        => DateTime.SpecifyKind(
            DateTime.ParseExact(value, DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture),
            DateTimeKind.Utc);

    private static User MapUser(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        WindowsSid = reader.GetString(reader.GetOrdinal("WindowsSid")),
        Domain = reader.GetString(reader.GetOrdinal("Domain")),
        Username = reader.GetString(reader.GetOrdinal("Username")),
        DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
        Role = (UserRole)reader.GetInt32(reader.GetOrdinal("Role")),
        IsActive = reader.GetInt32(reader.GetOrdinal("IsActive")) == 1,
        CreatedAt = ParseDate(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        UpdatedAt = ParseDate(reader.GetString(reader.GetOrdinal("UpdatedAt")))
    };

    private static VaultGroup MapVault(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        Name = reader.GetString(reader.GetOrdinal("Name")),
        Description = reader.GetString(reader.GetOrdinal("Description")),
        CreatedByUserId = reader.GetInt32(reader.GetOrdinal("CreatedByUserId")),
        CreatedAt = ParseDate(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        UpdatedAt = ParseDate(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
        IsArchived = reader.GetInt32(reader.GetOrdinal("IsArchived")) == 1
    };

    private static VaultAccess MapAccess(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(reader.GetOrdinal("Id")),
        VaultId = reader.GetInt32(reader.GetOrdinal("VaultId")),
        UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
        Permission = (PermissionLevel)reader.GetInt32(reader.GetOrdinal("Permission")),
        GrantedByUserId = reader.GetInt32(reader.GetOrdinal("GrantedByUserId")),
        GrantedAt = ParseDate(reader.GetString(reader.GetOrdinal("GrantedAt")))
    };

    private static Credential MapCredential(SqliteDataReader reader)
    {
        var tagsJson = reader.GetString(reader.GetOrdinal("Tags"));
        List<string> tags;
        try
        {
            tags = JsonSerializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            tags = new List<string>();
        }

        var rotationOrdinal = reader.GetOrdinal("RotationDueAt");
        var lastAccessedOrdinal = reader.GetOrdinal("LastAccessedAt");
        var notesOrdinal = reader.GetOrdinal("NotesEncrypted");
        var urlOrdinal = reader.GetOrdinal("Url");

        return new Credential
        {
            Id = reader.GetInt32(reader.GetOrdinal("Id")),
            VaultId = reader.GetInt32(reader.GetOrdinal("VaultId")),
            Label = reader.GetString(reader.GetOrdinal("Label")),
            UsernameOrEmail = reader.GetString(reader.GetOrdinal("UsernameOrEmail")),
            PasswordEncrypted = (byte[])reader.GetValue(reader.GetOrdinal("PasswordEncrypted")),
            NotesEncrypted = reader.IsDBNull(notesOrdinal) ? null : (byte[])reader.GetValue(notesOrdinal),
            Url = reader.IsDBNull(urlOrdinal) ? null : reader.GetString(urlOrdinal),
            Tags = tags,
            RotationDueAt = reader.IsDBNull(rotationOrdinal) ? null : ParseDate(reader.GetString(rotationOrdinal)),
            CreatedByUserId = reader.GetInt32(reader.GetOrdinal("CreatedByUserId")),
            CreatedAt = ParseDate(reader.GetString(reader.GetOrdinal("CreatedAt"))),
            UpdatedAt = ParseDate(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
            LastAccessedAt = reader.IsDBNull(lastAccessedOrdinal) ? null : ParseDate(reader.GetString(lastAccessedOrdinal)),
            IsDeleted = reader.GetInt32(reader.GetOrdinal("IsDeleted")) == 1
        };
    }

    private static AuditLogEntry MapAudit(SqliteDataReader reader)
    {
        var userIdOrdinal = reader.GetOrdinal("UserId");
        var targetTypeOrdinal = reader.GetOrdinal("TargetType");
        var targetIdOrdinal = reader.GetOrdinal("TargetId");
        var detailsOrdinal = reader.GetOrdinal("Details");

        return new AuditLogEntry
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            UserId = reader.IsDBNull(userIdOrdinal) ? null : reader.GetInt32(userIdOrdinal),
            Action = (AuditAction)reader.GetInt32(reader.GetOrdinal("Action")),
            TargetType = reader.IsDBNull(targetTypeOrdinal) ? null : reader.GetString(targetTypeOrdinal),
            TargetId = reader.IsDBNull(targetIdOrdinal) ? null : reader.GetString(targetIdOrdinal),
            Timestamp = ParseDate(reader.GetString(reader.GetOrdinal("Timestamp"))),
            Details = reader.IsDBNull(detailsOrdinal) ? null : reader.GetString(detailsOrdinal)
        };
    }

    private static void AddCredentialParameters(SqliteCommand command, Credential credential)
    {
        command.Parameters.AddWithValue("$vaultId", credential.VaultId);
        command.Parameters.AddWithValue("$label", credential.Label);
        command.Parameters.AddWithValue("$username", credential.UsernameOrEmail);
        command.Parameters.AddWithValue("$password", credential.PasswordEncrypted);
        command.Parameters.AddWithValue("$notes", (object?)credential.NotesEncrypted ?? DBNull.Value);
        command.Parameters.AddWithValue("$url", (object?)credential.Url ?? DBNull.Value);
        command.Parameters.AddWithValue("$tags", JsonSerializer.Serialize(credential.Tags));
        command.Parameters.AddWithValue("$rotation",
            credential.RotationDueAt.HasValue ? Format(credential.RotationDueAt.Value) : DBNull.Value);
        command.Parameters.AddWithValue("$createdBy", credential.CreatedByUserId);
        command.Parameters.AddWithValue("$createdAt", Format(credential.CreatedAt));
        command.Parameters.AddWithValue("$updatedAt", Format(credential.UpdatedAt));
        command.Parameters.AddWithValue("$lastAccessed",
            credential.LastAccessedAt.HasValue ? Format(credential.LastAccessedAt.Value) : DBNull.Value);
    }
}
