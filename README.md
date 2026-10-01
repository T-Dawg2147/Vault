# Vault — Secure Internal Password Vault

A Windows-internal WPF desktop application that replaces plaintext CSV/TXT/XLS/XLSX
password files with an encrypted, role-based, audited credential store.

> Internal use only. All credentials are encrypted at rest with AES-256-GCM;
> the master key is protected by Windows DPAPI bound to the current user profile.

## Solution layout

| Project | Purpose |
| --- | --- |
| `Vault.Core` | Domain models, enums, service/repository interfaces, input validation |
| `Vault.Security` | AES-GCM encryption, DPAPI master-key protection, centralized authorization |
| `Vault.Infrastructure` | SQLite persistence, Windows identity integration, services, legacy file importer |
| `Vault.App.Wpf` | WPF shell, MVVM view models, views, DI startup |
| `Vault.Tests` | Unit/integration tests (xunit) |

## Security model

- **Identity** — the current Windows user (`WindowsIdentity.GetCurrent()`) is mapped to an
  application user by SID. Unregistered or deactivated identities are denied at startup.
- **First run** — the first Windows user to launch the app with an empty database becomes
  the initial admin (see `AdminProvisioner`). All further users are registered by an admin
  (Users screen), identified by their Windows SID (`whoami /user`).
- **Roles** — Admin, Editor, Viewer. Deny-by-default: everything flows through
  `IAuthorizationService` in `Vault.Security`, enforced in the service layer — never
  just by hiding UI elements.
- **Vaults** — credentials live in vaults (e.g. IT, Finance, HR). Per-user access is granted
  per vault at Viewer or Editor level by an admin.
- **Encryption** — per-record AES-256-GCM (random nonce per write). Passwords and notes are
  only ever stored encrypted. The 32-byte master key is generated on first run and stored
  only as a DPAPI-protected blob (`master.key`, CurrentUser scope) next to the database in
  `%LOCALAPPDATA%\Vault`.
- **Audit** — logins (allowed/denied), credential views/reveals/creates/updates/deletes,
  vault and access changes, role changes, and imports are appended to the `AuditLog` table.
  Audit details never contain secrets.
- **Clipboard** — copied passwords auto-clear after 20 seconds.

## Build and test

```bash
cd Vault
dotnet restore Vault.slnx
dotnet build Vault.slnx
dotnet test Vault.Tests/Vault.Tests.csproj
```

WPF targets `net8.0-windows` with `EnableWindowsTargeting` so the solution builds on
non-Windows CI, but the app itself runs on Windows only (DPAPI + Windows identity).

## Importing legacy files

Admins get an Import screen supporting CSV, TXT (comma/semicolon/tab, quoting-aware) and
XLSX (first worksheet). Flow: pick file → map columns by index → preview with per-row
validation → commit. Valid rows are encrypted and written to the chosen vault; the import
is audited. Legacy binary `.xls` must be saved as `.xlsx` first.

## Extending

- Add fields to `Credential`, bump the schema in `SqliteVaultRepository.InitializeSchema`,
  and map the columns in the repository helpers.
- Add a permission by extending `IAuthorizationService` and pinning its behavior in
  `Vault.Tests/Security/AuthorizationTests.cs`.
- For a future networked backend, replace `IRepository` with an API-backed implementation;
  services and authorization already operate against the abstraction.
