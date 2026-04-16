import Foundation
import GRDB
import AssetSyncCore

public final class SQLiteMappingRepository: MappingRepositoryProtocol, @unchecked Sendable {
    private let dbPool: DatabasePool

    public init(dbPool: DatabasePool) {
        self.dbPool = dbPool
    }

    // MARK: - Model Mappings

    public func getModelMappings() async throws -> [ModelMapping] {
        try await dbPool.read { db in
            try Row.fetchAll(db, sql: "SELECT id, mdm_model_string, snipeit_model_id FROM model_mappings ORDER BY mdm_model_string")
                .map { ModelMapping(id: $0["id"], mdmModelString: $0["mdm_model_string"], snipeItModelId: $0["snipeit_model_id"]) }
        }
    }

    public func getModelMapping(mdmModelString: String) async throws -> ModelMapping? {
        try await dbPool.read { db in
            try Row.fetchOne(db, sql: "SELECT id, mdm_model_string, snipeit_model_id FROM model_mappings WHERE mdm_model_string = ? COLLATE NOCASE", arguments: [mdmModelString])
                .map { ModelMapping(id: $0["id"], mdmModelString: $0["mdm_model_string"], snipeItModelId: $0["snipeit_model_id"]) }
        }
    }

    public func saveModelMapping(_ mapping: ModelMapping) async throws {
        try await dbPool.write { db in
            if mapping.id > 0 {
                try db.execute(sql: "UPDATE model_mappings SET mdm_model_string = ?, snipeit_model_id = ? WHERE id = ?",
                               arguments: [mapping.mdmModelString, mapping.snipeItModelId, mapping.id])
            } else {
                try db.execute(sql: "INSERT OR REPLACE INTO model_mappings (mdm_model_string, snipeit_model_id) VALUES (?, ?)",
                               arguments: [mapping.mdmModelString, mapping.snipeItModelId])
            }
        }
    }

    public func deleteModelMapping(id: Int) async throws {
        try await dbPool.write { db in
            try db.execute(sql: "DELETE FROM model_mappings WHERE id = ?", arguments: [id])
        }
    }

    // MARK: - User Mappings

    public func getUserMappings() async throws -> [UserMapping] {
        try await dbPool.read { db in
            try Row.fetchAll(db, sql: "SELECT id, mdm_user_identifier, snipeit_user_id FROM user_mappings ORDER BY mdm_user_identifier")
                .map { UserMapping(id: $0["id"], mdmUserIdentifier: $0["mdm_user_identifier"], snipeItUserId: $0["snipeit_user_id"]) }
        }
    }

    public func getUserMapping(mdmUserIdentifier: String) async throws -> UserMapping? {
        try await dbPool.read { db in
            try Row.fetchOne(db, sql: "SELECT id, mdm_user_identifier, snipeit_user_id FROM user_mappings WHERE mdm_user_identifier = ? COLLATE NOCASE", arguments: [mdmUserIdentifier])
                .map { UserMapping(id: $0["id"], mdmUserIdentifier: $0["mdm_user_identifier"], snipeItUserId: $0["snipeit_user_id"]) }
        }
    }

    public func saveUserMapping(_ mapping: UserMapping) async throws {
        try await dbPool.write { db in
            if mapping.id > 0 {
                try db.execute(sql: "UPDATE user_mappings SET mdm_user_identifier = ?, snipeit_user_id = ? WHERE id = ?",
                               arguments: [mapping.mdmUserIdentifier, mapping.snipeItUserId, mapping.id])
            } else {
                try db.execute(sql: "INSERT OR REPLACE INTO user_mappings (mdm_user_identifier, snipeit_user_id) VALUES (?, ?)",
                               arguments: [mapping.mdmUserIdentifier, mapping.snipeItUserId])
            }
        }
    }

    public func deleteUserMapping(id: Int) async throws {
        try await dbPool.write { db in
            try db.execute(sql: "DELETE FROM user_mappings WHERE id = ?", arguments: [id])
        }
    }

    // MARK: - Build Mappings

    public func getBuildMappings() async throws -> [BuildMapping] {
        try await dbPool.read { db in
            try Row.fetchAll(db, sql: "SELECT id, build_number, friendly_name FROM build_mappings ORDER BY build_number")
                .map { BuildMapping(id: $0["id"], buildNumber: $0["build_number"], friendlyName: $0["friendly_name"]) }
        }
    }

    public func getBuildMapping(buildNumber: String) async throws -> BuildMapping? {
        try await dbPool.read { db in
            try Row.fetchOne(db, sql: "SELECT id, build_number, friendly_name FROM build_mappings WHERE build_number = ?", arguments: [buildNumber])
                .map { BuildMapping(id: $0["id"], buildNumber: $0["build_number"], friendlyName: $0["friendly_name"]) }
        }
    }

    public func saveBuildMapping(_ mapping: BuildMapping) async throws {
        try await dbPool.write { db in
            if mapping.id > 0 {
                try db.execute(sql: "UPDATE build_mappings SET build_number = ?, friendly_name = ? WHERE id = ?",
                               arguments: [mapping.buildNumber, mapping.friendlyName, mapping.id])
            } else {
                try db.execute(sql: "INSERT OR REPLACE INTO build_mappings (build_number, friendly_name) VALUES (?, ?)",
                               arguments: [mapping.buildNumber, mapping.friendlyName])
            }
        }
    }

    public func deleteBuildMapping(id: Int) async throws {
        try await dbPool.write { db in
            try db.execute(sql: "DELETE FROM build_mappings WHERE id = ?", arguments: [id])
        }
    }

    // MARK: - Category Mappings

    public func getCategoryMappings() async throws -> [CategoryMapping] {
        try await dbPool.read { db in
            try Row.fetchAll(db, sql: "SELECT id, mdm_device_type, snipeit_category_id FROM category_mappings ORDER BY mdm_device_type")
                .map { CategoryMapping(id: $0["id"], mdmDeviceType: $0["mdm_device_type"], snipeItCategoryId: $0["snipeit_category_id"]) }
        }
    }

    public func getCategoryMapping(mdmDeviceType: String) async throws -> CategoryMapping? {
        try await dbPool.read { db in
            try Row.fetchOne(db, sql: "SELECT id, mdm_device_type, snipeit_category_id FROM category_mappings WHERE mdm_device_type = ? COLLATE NOCASE", arguments: [mdmDeviceType])
                .map { CategoryMapping(id: $0["id"], mdmDeviceType: $0["mdm_device_type"], snipeItCategoryId: $0["snipeit_category_id"]) }
        }
    }

    public func saveCategoryMapping(_ mapping: CategoryMapping) async throws {
        try await dbPool.write { db in
            if mapping.id > 0 {
                try db.execute(sql: "UPDATE category_mappings SET mdm_device_type = ?, snipeit_category_id = ? WHERE id = ?",
                               arguments: [mapping.mdmDeviceType, mapping.snipeItCategoryId, mapping.id])
            } else {
                try db.execute(sql: "INSERT OR REPLACE INTO category_mappings (mdm_device_type, snipeit_category_id) VALUES (?, ?)",
                               arguments: [mapping.mdmDeviceType, mapping.snipeItCategoryId])
            }
        }
    }

    public func deleteCategoryMapping(id: Int) async throws {
        try await dbPool.write { db in
            try db.execute(sql: "DELETE FROM category_mappings WHERE id = ?", arguments: [id])
        }
    }

    // MARK: - Model Ignores

    public func getIgnoredModels() async throws -> [String] {
        try await dbPool.read { db in
            try String.fetchAll(db, sql: "SELECT mdm_model_string FROM model_ignores ORDER BY mdm_model_string")
        }
    }

    public func isModelIgnored(_ mdmModelString: String) async throws -> Bool {
        try await dbPool.read { db in
            try Int.fetchOne(db, sql: "SELECT 1 FROM model_ignores WHERE mdm_model_string = ? COLLATE NOCASE LIMIT 1",
                             arguments: [mdmModelString]) != nil
        }
    }

    public func addModelIgnore(_ mdmModelString: String) async throws {
        try await dbPool.write { db in
            try db.execute(sql: "INSERT OR IGNORE INTO model_ignores (mdm_model_string) VALUES (?)",
                           arguments: [mdmModelString])
        }
    }

    public func removeModelIgnore(_ mdmModelString: String) async throws {
        try await dbPool.write { db in
            try db.execute(sql: "DELETE FROM model_ignores WHERE mdm_model_string = ? COLLATE NOCASE",
                           arguments: [mdmModelString])
        }
    }
}
