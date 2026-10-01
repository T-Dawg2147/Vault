using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;
using Vault.Infrastructure.Import;
using Vault.Infrastructure.Services;
using Vault.Security.Authorization;
using Xunit;

namespace Vault.Tests.Infrastructure;

public class ImportTests : IDisposable
{
    private readonly string _tempFile;
    private readonly global::Vault.Infrastructure.Persistence.SqliteVaultRepository _repository;
    private readonly LegacyFileImporter _importer;
    private readonly User _admin;
    private readonly User _viewer;
    private readonly VaultGroup _vault;

    public ImportTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"vault-import-{Guid.NewGuid():N}.csv");
        _repository = TestHelpers.CreateRepository();
        var encryption = TestHelpers.CreateEncryption();
        var authorization = new AuthorizationService(_repository);
        var audit = new AuditService(_repository, authorization);
        var credentials = new CredentialService(_repository, authorization, encryption, audit);
        _importer = new LegacyFileImporter(credentials, authorization, audit);

        _admin = _repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Admin));
        _viewer = _repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Viewer));
        _vault = _repository.InsertVault(new VaultGroup { Name = "Migrated", CreatedByUserId = _admin.Id });
    }

    private void WriteCsv(string content) => File.WriteAllText(_tempFile, content);

    private static ImportColumnMapping DefaultMapping() => new()
    {
        LabelColumn = 0, UsernameColumn = 1, PasswordColumn = 2, UrlColumn = 3, HasHeaderRow = true
    };

    [Fact]
    public void ReadColumns_ReturnsHeaders()
    {
        WriteCsv("Label,Username,Password,Url\nMail,admin,pw,https://mail\n");

        var columns = _importer.ReadColumns(_tempFile, hasHeaderRow: true);

        Assert.Equal(new[] { "Label", "Username", "Password", "Url" }, columns);
    }

    [Fact]
    public void Preview_ParsesQuotedCsv_AndValidates()
    {
        WriteCsv("Label,Username,Password,Url\n" +
                 "\"Mail, primary\",admin,\"p@ss,;w\"\"ord\",https://mail.internal\n" +
                 "MissingPassword,user,,https://x.internal\n" +
                 "BadUrl,user,pw,not-a-url\n");

        var preview = _importer.Preview(_tempFile, DefaultMapping());

        Assert.Equal(3, preview.Rows.Count);
        Assert.True(preview.Rows[0].IsValid);
        Assert.Equal("Mail, primary", preview.Rows[0].Label);
        Assert.Equal("p@ss,;w\"ord", preview.Rows[0].Password);

        Assert.False(preview.Rows[1].IsValid);
        Assert.Contains(preview.Rows[1].Errors, e => e.Contains("Password"));

        Assert.False(preview.Rows[2].IsValid);
        Assert.Contains(preview.Rows[2].Errors, e => e.Contains("URL"));
    }

    [Fact]
    public void Preview_HandlesTabDelimitedTxt()
    {
        File.WriteAllText(_tempFile, "Label\tUsername\tPassword\nWeb\tadmin\tpw123\n");
        File.Move(_tempFile, _tempFile + ".txt");
        _tempFileList.Add(_tempFile + ".txt");

        var preview = _importer.Preview(_tempFile + ".txt",
            new ImportColumnMapping { LabelColumn = 0, UsernameColumn = 1, PasswordColumn = 2 });

        Assert.Single(preview.Rows);
        Assert.Equal("Web", preview.Rows[0].Label);
    }

    [Fact]
    public void Commit_EncryptsAndPersists_ValidRows_Only()
    {
        WriteCsv("Label,Username,Password,Url\nMail,admin,secret-one,https://mail.internal\nBroken,user,,\n");

        var preview = _importer.Preview(_tempFile, DefaultMapping());
        var imported = _importer.Commit(_admin, _vault.Id, preview);

        Assert.Equal(1, imported);
        var stored = _repository.GetCredentialsByVault(_vault.Id);
        Assert.Single(stored);
        Assert.Equal("Mail", stored[0].Label);

        // Secret must not appear in raw storage.
        var rawText = System.Text.Encoding.UTF8.GetString(stored[0].PasswordEncrypted);
        Assert.DoesNotContain("secret-one", rawText);
    }

    [Fact]
    public void Commit_WritesAuditTrail()
    {
        WriteCsv("Label,Username,Password\nMail,admin,pw\n");

        var preview = _importer.Preview(_tempFile, DefaultMapping());
        _importer.Commit(_admin, _vault.Id, preview);

        var entries = _repository.GetAuditEntries(100);
        Assert.Contains(entries, e => e.Action == AuditAction.ImportStarted);
        Assert.Contains(entries, e => e.Action == AuditAction.ImportCompleted);
        Assert.All(entries, e =>
        {
            if (e.Details is not null)
                Assert.DoesNotContain("pw", e.Details.Split(',', ';', ' ').Where(w => w == "pw"));
        });
    }

    [Fact]
    public void Commit_RequiresAdmin()
    {
        WriteCsv("Label,Username,Password\nMail,admin,pw\n");
        var preview = _importer.Preview(_tempFile, DefaultMapping());

        Assert.Throws<VaultAccessDeniedException>(() => _importer.Commit(_viewer, _vault.Id, preview));
    }

    [Fact]
    public void UnsupportedExtension_IsRejected()
    {
        File.WriteAllText(_tempFile + ".xls", "binary-not-supported");
        _tempFileList.Add(_tempFile + ".xls");

        Assert.Throws<ValidationException>(() => _importer.ReadColumns(_tempFile + ".xls", true));
    }

    private readonly List<string> _tempFileList = new();

    public void Dispose()
    {
        foreach (var file in _tempFileList.Concat(new[] { _tempFile }))
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}
