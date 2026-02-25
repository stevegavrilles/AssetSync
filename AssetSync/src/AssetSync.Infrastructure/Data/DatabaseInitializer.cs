using Microsoft.Data.Sqlite;

namespace AssetSync.Infrastructure.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    public void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS config (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );");

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp_utc TEXT NOT NULL,
                level TEXT NOT NULL,
                source_system TEXT NOT NULL,
                action TEXT NOT NULL,
                serial_number TEXT,
                device_name TEXT,
                field_name TEXT,
                old_value TEXT,
                new_value TEXT,
                success INTEGER NOT NULL DEFAULT 1,
                error_detail TEXT,
                sync_run_id TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp_utc);
            CREATE INDEX IF NOT EXISTS idx_logs_level ON logs(level);
            CREATE INDEX IF NOT EXISTS idx_logs_sync_run ON logs(sync_run_id);
            CREATE INDEX IF NOT EXISTS idx_logs_serial ON logs(serial_number);");

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS model_mappings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                mdm_model_string TEXT NOT NULL UNIQUE,
                snipeit_model_id INTEGER NOT NULL
            );");

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS user_mappings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                mdm_user_identifier TEXT NOT NULL UNIQUE,
                snipeit_user_id INTEGER NOT NULL
            );");

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS build_mappings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                build_number TEXT NOT NULL UNIQUE,
                friendly_name TEXT NOT NULL
            );");

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS category_mappings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                mdm_device_type TEXT NOT NULL UNIQUE,
                snipeit_category_id INTEGER NOT NULL
            );");

        Execute(conn, @"
            CREATE TABLE IF NOT EXISTS credentials (
                key TEXT PRIMARY KEY,
                encrypted_value BLOB NOT NULL
            );");

        SeedDefaultBuildMappings(conn);
    }

    private static void Execute(SqliteConnection conn, string sql)
    {
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var s = statement.Trim();
            if (s.Length == 0) continue;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = s;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedDefaultBuildMappings(SqliteConnection conn)
    {
        var defaults = new (string Build, string Name)[]
        {
            ("19041", "Windows 10 2004"),
            ("19042", "Windows 10 20H2"),
            ("19043", "Windows 10 21H1"),
            ("19044", "Windows 10 21H2"),
            ("19045", "Windows 10 22H2"),
            ("22621", "Windows 11 23H2"),
            ("22631", "Windows 11 23H2"),
            ("26100", "Windows 11 24H2")
        };
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO build_mappings (build_number, friendly_name) VALUES ($b, $n)";
        foreach (var (b, n) in defaults)
        {
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$b", b);
            cmd.Parameters.AddWithValue("$n", n);
            cmd.ExecuteNonQuery();
        }
    }
}
