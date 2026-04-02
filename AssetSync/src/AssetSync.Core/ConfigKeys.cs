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
}

public static class CredentialKeys
{
    public const string SnipeItApiKey = "snipeit_api_key";
    public const string IntuneClientSecret = "intune_client_secret";
    public const string IruApiToken = "iru_api_token";
}
