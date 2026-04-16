import Foundation
import GRDB
import AssetSyncCore

public final class SQLiteConfigRepository: ConfigRepositoryProtocol, @unchecked Sendable {
    private let dbPool: DatabasePool

    public init(dbPool: DatabasePool) {
        self.dbPool = dbPool
    }

    public func get(key: String) async throws -> String? {
        try await dbPool.read { db in
            try String.fetchOne(db, sql: "SELECT value FROM config WHERE key = ?", arguments: [key])
        }
    }

    public func set(key: String, value: String) async throws {
        try await dbPool.write { db in
            try db.execute(
                sql: "INSERT OR REPLACE INTO config (key, value) VALUES (?, ?)",
                arguments: [key, value])
        }
    }

    public func getSyncIntervalHours() async throws -> Int {
        guard let v = try await get(key: "sync_interval_hours"), let h = Int(v), (1...72).contains(h) else {
            return 1
        }
        return h
    }

    public func setSyncIntervalHours(_ hours: Int) async throws {
        try await set(key: "sync_interval_hours", value: String(hours))
    }

    public func getDryRunDefault() async throws -> Bool {
        try await getBool(key: "dry_run_default")
    }

    public func setDryRunDefault(_ value: Bool) async throws {
        try await set(key: "dry_run_default", value: value ? "1" : "0")
    }

    public func getWriteBackIntuneEnabled() async throws -> Bool {
        try await getBool(key: "write_back_intune")
    }

    public func setWriteBackIntuneEnabled(_ value: Bool) async throws {
        try await set(key: "write_back_intune", value: value ? "1" : "0")
    }

    public func getWriteBackIruEnabled() async throws -> Bool {
        try await getBool(key: "write_back_iru")
    }

    public func setWriteBackIruEnabled(_ value: Bool) async throws {
        try await set(key: "write_back_iru", value: value ? "1" : "0")
    }

    public func getIntuneMdmWins() async throws -> Bool {
        try await getBool(key: "sync_intune_mdm_wins")
    }

    public func setIntuneMdmWins(_ value: Bool) async throws {
        try await set(key: "sync_intune_mdm_wins", value: value ? "1" : "0")
    }

    public func getIruMdmWins() async throws -> Bool {
        try await getBool(key: "sync_iru_mdm_wins")
    }

    public func setIruMdmWins(_ value: Bool) async throws {
        try await set(key: "sync_iru_mdm_wins", value: value ? "1" : "0")
    }

    private func getBool(key: String) async throws -> Bool {
        guard let v = try await get(key: key) else { return false }
        return v == "1" || v.lowercased() == "true"
    }
}
