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

- **Snipe-IT wins** — MDM data never overwrites existing Snipe-IT values.
- **Serial number** is the match key (normalized for comparison).
- **Write-back** (asset tag to Intune/Iru) is off by default and can be enabled per platform.

## Getting started

1. Open `AssetSync.sln` in Visual Studio (or `dotnet build` on Windows).
2. Run **AssetSync.App** (WPF). On first run, the app creates the SQLite DB under `%LocalAppData%\AssetSync\assetsync.db` and seeds default Windows build mappings.
3. Configure Snipe-IT, Intune, and Iru in **Settings** (or complete the first-run wizard when implemented).
4. Use **Dashboard** → **Sync Now** or **Dry Run** to run a sync.

## Build and test

From the solution directory (Windows):

```bash
dotnet build
dotnet test
```

## Credentials

Stored encrypted with Windows DPAPI in the local SQLite database. No credentials are written to logs.

## License

Internal use per your organization’s policy.
