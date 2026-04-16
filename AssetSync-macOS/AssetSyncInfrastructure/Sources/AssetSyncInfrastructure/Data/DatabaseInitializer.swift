import Foundation
import GRDB
import AssetSyncCore

/// Creates the SQLite schema and seeds default data.
/// Database lives at ~/Library/Application Support/AssetSync/assetsync.db
public struct DatabaseInitializer {
    private let dbPool: DatabasePool

    public static var defaultDatabasePath: String {
        let appSupport = FileManager.default.urls(
            for: .applicationSupportDirectory, in: .userDomainMask).first!
        let dir = appSupport.appendingPathComponent("AssetSync")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir.appendingPathComponent("assetsync.db").path
    }

    public init(path: String? = nil) throws {
        let dbPath = path ?? Self.defaultDatabasePath
        dbPool = try DatabasePool(path: dbPath)
    }

    public var pool: DatabasePool { dbPool }

    public func initialize() throws {
        try dbPool.write { db in
            try db.execute(sql: """
                CREATE TABLE IF NOT EXISTS config (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                )
                """)

            try db.execute(sql: """
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
                )
                """)
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_logs_timestamp ON logs(timestamp_utc)")
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_logs_level ON logs(level)")
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_logs_sync_run ON logs(sync_run_id)")
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_logs_serial ON logs(serial_number)")

            try db.execute(sql: """
                CREATE TABLE IF NOT EXISTS model_mappings (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    mdm_model_string TEXT NOT NULL UNIQUE,
                    snipeit_model_id INTEGER NOT NULL
                )
                """)

            try db.execute(sql: """
                CREATE TABLE IF NOT EXISTS user_mappings (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    mdm_user_identifier TEXT NOT NULL UNIQUE,
                    snipeit_user_id INTEGER NOT NULL
                )
                """)

            try db.execute(sql: """
                CREATE TABLE IF NOT EXISTS build_mappings (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    build_number TEXT NOT NULL UNIQUE,
                    friendly_name TEXT NOT NULL
                )
                """)

            try db.execute(sql: """
                CREATE TABLE IF NOT EXISTS category_mappings (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    mdm_device_type TEXT NOT NULL UNIQUE,
                    snipeit_category_id INTEGER NOT NULL
                )
                """)

            try db.execute(sql: """
                CREATE TABLE IF NOT EXISTS model_ignores (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    mdm_model_string TEXT NOT NULL UNIQUE,
                    ignored_at TEXT NOT NULL DEFAULT (datetime('now'))
                )
                """)

            // Seed default Windows build mappings
            let defaults: [(String, String)] = [
                ("19041", "Windows 10 2004"),
                ("19042", "Windows 10 20H2"),
                ("19043", "Windows 10 21H1"),
                ("19044", "Windows 10 21H2"),
                ("19045", "Windows 10 22H2"),
                ("22621", "Windows 11 23H2"),
                ("22631", "Windows 11 23H2"),
                ("26100", "Windows 11 24H2"),
            ]
            for (build, name) in defaults {
                try db.execute(
                    sql: "INSERT OR IGNORE INTO build_mappings (build_number, friendly_name) VALUES (?, ?)",
                    arguments: [build, name])
            }
        }
    }
}
