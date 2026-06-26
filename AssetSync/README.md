# Asset Sync

Windows desktop application that synchronizes device asset data between **Microsoft Intune**, **Iru (formerly Kandji)**, and **Snipe-IT**. Snipe-IT is the source of truth; the app reads from Intune and Iru and creates/updates assets in Snipe-IT.

## Tech stack

- **.NET 8**, **WPF**, **SQLite**
- **Microsoft Graph SDK** (Intune), **HttpClient** (Iru, Snipe-IT)
- **DPAPI** for encrypted credential storage
- **CommunityToolkit.Mvvm** for MVVM

## Solution layout

- **AssetSync.Core** — Domain models, interfaces, sync engine (SerialNumberNormalizer, DeviceMerger, ConflictResolver, BuildVersionMapper, SyncEngine)
- **AssetSync.Infrastructure** — SQLite (config, logs, mappings, credentials), Intune/Iru/Snipe-IT API clients, WebhookService, ConnectivityTester, DpapiCredentialStore
- **AssetSync.App** — WPF UI (Dashboard, Settings, Mappings, Logs, Queues, Setup Wizard)
- **AssetSync.Service** — Optional Windows Service host for scheduled sync
- **AssetSync.Core.Tests** / **AssetSync.Infrastructure.Tests** — Unit and integration tests

## Design principles

- **Snipe-IT wins for most fields** — MDM never overwrites existing Snipe-IT values, except **device name**, where MDM (Intune/Iru) is the source of truth and any difference is pushed to Snipe-IT. Asset tags remain owned by Snipe-IT.
- **Serial number** is the match key (normalized for comparison).
- **Write-back** (asset tag to Intune/Iru) is off by default and can be enabled per platform.

## Getting started

1. Open `AssetSync.sln` in Visual Studio (or `dotnet build` on Windows).
2. Run **AssetSync.App** (WPF). On first run, the app creates the SQLite DB under `%LocalAppData%\AssetSync\assetsync.db` and seeds default Windows build mappings.
3. On first run a setup wizard walks you through connecting Snipe-IT, Intune, and Iru and running an initial dry run. You can also configure or change these later in **Settings**.
4. Use **Dashboard** → **Sync Now** or **Dry Run** to run a sync.

## Build and test

From the solution directory (Windows):

```bash
dotnet build
dotnet test
```

## Credentials

Stored encrypted with Windows DPAPI in the local SQLite database. No credentials are written to logs.

## Graph API permissions (License Groups feature)

The License Groups feature **reuses the existing Intune app registration and client-secret credential** — there is no new app registration to create. An admin must add the following as **Application permissions** on that same app registration and click **Grant admin consent**:

- **Read path** (read-only mappings, Entra → Snipe-IT): `GroupMember.Read.All` + `User.ReadBasic.All`
- **Write / provisioning path** (read-only-OFF mappings, Snipe-IT → Entra): `GroupMember.ReadWrite.All`

Until these are granted and consented, the feature's Graph calls fail at runtime and the affected mapping shows an **error / halted** state (the rest of the app is unaffected).

## License

Internal use per your organization’s policy.
