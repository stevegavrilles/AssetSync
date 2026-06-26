namespace AssetSync.Core;

public static class ConfigKeys
{
    public const string SnipeItUrl = "snipeit_url";
    public const string IntuneTenantId = "intune_tenant_id";
    public const string IntuneClientId = "intune_client_id";
    public const string IruBaseUrl = "iru_base_url";
    public const string WebhookUrl = "webhook_url";
    public const string WebhookType = "webhook_type";
    public const string LogRetentionDays = "log_retention_days";
    public const string SetupComplete = "setup_complete";
    public const string IntuneMdmWins = "sync_intune_mdm_wins";
    public const string IruMdmWins = "sync_iru_mdm_wins";

    // License-group sync (Entra group <-> Snipe-IT license seats)
    public const string LicenseUserMatchField = "license_user_match_field";       // "upn-to-username" (default) | "email"
    public const string LicenseRemovalGraceSyncs = "license_removal_grace_syncs";  // default 2
    public const string LicenseRemovalCircuitBreaker = "license_removal_breaker";  // per-mapping absolute, default 20
    public const string SnipeItSeatPathTemplate = "snipeit_seat_path_template";    // default "/api/v1/licenses/{0}/seats/{1}"
}

public static class CredentialKeys
{
    public const string SnipeItApiKey = "snipeit_api_key";
    public const string IntuneClientSecret = "intune_client_secret";
    public const string IruApiToken = "iru_api_token";
}
