import Foundation
import GRDB
import AssetSyncCore

public final class SQLiteLogRepository: LogRepositoryProtocol, @unchecked Sendable {
    private let dbPool: DatabasePool

    public init(dbPool: DatabasePool) {
        self.dbPool = dbPool
    }

    public func append(_ entry: LogEntry) async throws {
        try await dbPool.write { db in
            let iso = ISO8601DateFormatter()
            iso.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
            try db.execute(
                sql: """
                    INSERT INTO logs
                    (timestamp_utc, level, source_system, action, serial_number, device_name,
                     field_name, old_value, new_value, success, error_detail, sync_run_id)
                    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                arguments: [
                    iso.string(from: entry.timestampUtc),
                    entry.level.rawValue,
                    entry.sourceSystem.rawValue,
                    entry.action,
                    entry.serialNumber,
                    entry.deviceName,
                    entry.fieldName,
                    entry.oldValue,
                    entry.newValue,
                    entry.success ? 1 : 0,
                    entry.errorDetail,
                    entry.syncRunId,
                ])
        }
    }

    public func getEntries(filter: LogFilter) async throws -> [LogEntry] {
        try await dbPool.read { db in
            var conditions = ["1=1"]
            var arguments: [DatabaseValueConvertible?] = []
            let iso = ISO8601DateFormatter()
            iso.formatOptions = [.withInternetDateTime, .withFractionalSeconds]

            if let from = filter.fromUtc {
                conditions.append("timestamp_utc >= ?")
                arguments.append(iso.string(from: from))
            }
            if let to = filter.toUtc {
                conditions.append("timestamp_utc <= ?")
                arguments.append(iso.string(from: to))
            }
            if let level = filter.minLevel {
                conditions.append("level >= ?")
                arguments.append(level.rawValue)
            }
            if let source = filter.sourceSystem {
                conditions.append("source_system = ?")
                arguments.append(source.rawValue)
            }
            if let action = filter.action {
                conditions.append("action = ?")
                arguments.append(action)
            }
            if let serial = filter.serialNumber {
                conditions.append("serial_number = ?")
                arguments.append(serial)
            }
            if let runId = filter.syncRunId {
                conditions.append("sync_run_id = ?")
                arguments.append(runId)
            }
            if let text = filter.freeText {
                conditions.append("(error_detail LIKE ? OR device_name LIKE ? OR serial_number LIKE ?)")
                let pattern = "%\(text)%"
                arguments.append(contentsOf: [pattern, pattern, pattern] as [DatabaseValueConvertible?])
            }

            var sql = """
                SELECT id, timestamp_utc, level, source_system, action, serial_number,
                       device_name, field_name, old_value, new_value, success, error_detail, sync_run_id
                FROM logs WHERE \(conditions.joined(separator: " AND "))
                ORDER BY timestamp_utc DESC
                """
            if let limit = filter.limit { sql += " LIMIT \(limit)" }
            if let offset = filter.offset { sql += " OFFSET \(offset)" }

            let rows = try Row.fetchAll(db, sql: sql, arguments: StatementArguments(arguments.map { $0 as DatabaseValueConvertible? }))
            return rows.map { row in
                var entry = LogEntry()
                entry.id = row["id"]
                entry.timestampUtc = iso.date(from: row["timestamp_utc"]) ?? Date()
                entry.level = LogLevel(rawValue: row["level"]) ?? .info
                entry.sourceSystem = SourceSystem(rawValue: row["source_system"]) ?? .application
                entry.action = row["action"]
                entry.serialNumber = row["serial_number"]
                entry.deviceName = row["device_name"]
                entry.fieldName = row["field_name"]
                entry.oldValue = row["old_value"]
                entry.newValue = row["new_value"]
                entry.success = (row["success"] as Int) != 0
                entry.errorDetail = row["error_detail"]
                entry.syncRunId = row["sync_run_id"]
                return entry
            }
        }
    }

    public func purgeOlderThan(_ retention: TimeInterval) async throws {
        let iso = ISO8601DateFormatter()
        iso.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let cutoff = iso.string(from: Date().addingTimeInterval(-retention))
        try await dbPool.write { db in
            try db.execute(sql: "DELETE FROM logs WHERE timestamp_utc < ?", arguments: [cutoff])
        }
    }
}
