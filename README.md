# Vault — Secure Internal Password Vault

A cross-platform (Windows, macOS, Linux) [Avalonia UI](https://avaloniaui.net/) desktop application that
replaces plaintext CSV/TXT/XLS/XLSX password files with an encrypted, role-based, audited credential store.

> Internal use only. All credentials are encrypted at rest with AES-256-GCM. The master key is
> protected by the platform: Windows DPAPI (LocalMachine scope), the macOS Keychain, or a
> `0600` file on Linux.

## Supported platforms

| Platform | Status | Master key protection | Identity |
| --- | --- | --- | --- |
| Windows 10/11 (x64) | Supported | DPAPI (LocalMachine) | Windows SID |
| macOS 12+ (arm64, x64) | Supported | Login Keychain (falls back to a `0600` file) | `posix:user@machine` |
| Linux (x64) | Best effort | `0600` file | `posix:user@machine` |

## Solution layout

| Project | Purpose |
| --- | --- |
| `Vault.Core` | Domain models, enums, service/repository interfaces, input validation |
| `Vault.Security` | AES-GCM encryption, master-key stores (DPAPI / macOS Keychain / file) + factory, centralized authorization |
| `Vault.Infrastructure` | SQLite persistence, per-platform identity services, services, legacy file importer |
| `Vault.App` | Avalonia shell, MVVM view models, views, DI startup |
| `Vault.Tests` | Unit/integration tests (xunit) |

## Security model

- **Identity** — the current OS user is mapped to an application user by a stable identifier:
  the Windows SID (`WindowsIdentity.GetCurrent()`) on Windows, or `posix:<username>@<machine>`
  on macOS/Linux (`ICurrentUserIdentityService`). Unregistered or deactivated identities are denied at startup.
- **First run** — the first Windows user to launch the app with an empty database becomes
  the initial admin (see `AdminProvisioner`). All further users are registered by an admin
  (Users screen); the identifier is captured on the user's first sign-in.
- **Roles** — Admin, Editor, Viewer. Deny-by-default: everything flows through
  `IAuthorizationService` in `Vault.Security`, enforced in the service layer — never
  just by hiding UI elements.
- **Vaults** — credentials live in vaults (e.g. IT, Finance, HR). Per-user access is granted
  per vault at Viewer or Editor level by an admin.
- **Encryption** — per-record AES-256-GCM (random nonce per write). Passwords and notes are
  only ever stored encrypted. The 32-byte master key is generated on first run and never
  written unprotected, except in the file fallback below:
  - **Windows** — DPAPI-protected blob (`master.key`, LocalMachine scope) in the data folder.
    Restrict that folder with NTFS permissions; legacy CurrentUser-protected keys are
    migrated automatically.
  - **macOS** — a generic-password item in the login Keychain (service `Vault`), accessed via
    the `security` tool. If the Keychain is unavailable, a `master.key` file with mode `0600`
    is used instead. Note the `security` tool receives the key as a command-line argument for
    a brief moment when it is first stored.
  - **Linux** — `master.key` with mode `0600`.
- **Data location** (`vault.db` and, where applicable, `master.key`):
  - Windows — `%LOCALAPPDATA%\Vault`, or the `Vault` folder next to the executable if an
    existing install already has a `vault.db` there.
  - macOS — `~/Library/Application Support/Vault`
  - Linux — `~/.local/share/Vault`
- **Audit** — logins (allowed/denied), credential views/reveals/creates/updates/deletes,
  vault and access changes, role changes, and imports are appended to the `AuditLog` table.
  Audit details never contain secrets.
- **Clipboard** — copied passwords auto-clear after 20 seconds.

## Build, test and run

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) (or newer).

```bash
dotnet restore Vault.slnx
dotnet build Vault.slnx
dotnet test Vault.Tests/Vault.Tests.csproj
dotnet run --project Vault.App
```

## Publish

```bash
dotnet publish Vault.App -c Release -r win-x64   --self-contained -o publish/win-x64
dotnet publish Vault.App -c Release -r osx-arm64 --self-contained -o publish/osx-arm64
dotnet publish Vault.App -c Release -r osx-x64   --self-contained -o publish/osx-x64
```

To bundle a macOS `.app` from a published folder:

```bash
./scripts/bundle-macos.sh publish/osx-arm64 Vault.app
```

The bundle is unsigned; sign/notarize it (`codesign`, `notarytool`) before distributing,
otherwise Gatekeeper requires users to approve it manually.

CI (`.github/workflows/build.yml`) builds and tests on Windows, macOS and Ubuntu and uploads
published artifacts for `win-x64`, `osx-arm64` and `osx-x64`.

## Notes for existing Windows users

- Existing databases and DPAPI-protected `master.key` files keep working unchanged; the app
  keeps using the `Vault` folder next to the executable if it already contains `vault.db`.
- Stored SIDs are still used for Windows identities. Data is not portable between machines or
  operating systems (the key is bound to the machine/Keychain); migrate by re-importing.
- Behaviour that could not be ported exactly: the WPF `MessageBox` is replaced by a simple
  in-app dialog, and the date picker/file picker use Avalonia's native controls.

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
