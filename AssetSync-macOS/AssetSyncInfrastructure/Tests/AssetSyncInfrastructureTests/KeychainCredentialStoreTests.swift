import XCTest
@testable import AssetSyncInfrastructure

final class KeychainCredentialStoreTests: XCTestCase {
    // Use a unique service name to avoid polluting the real keychain
    private let store = KeychainCredentialStore(service: "com.assetsync.tests")

    override func tearDown() async throws {
        try? store.remove(key: "test_key")
    }

    func testSetAndGet() throws {
        try store.set(key: "test_key", value: "secret123")
        let value = try store.get(key: "test_key")
        XCTAssertEqual(value, "secret123")
    }

    func testGetMissingKeyReturnsNil() throws {
        let value = try store.get(key: "nonexistent_key_\(UUID().uuidString)")
        XCTAssertNil(value)
    }

    func testRemove() throws {
        try store.set(key: "test_key", value: "to_delete")
        try store.remove(key: "test_key")
        XCTAssertNil(try store.get(key: "test_key"))
    }

    func testExists() throws {
        XCTAssertFalse(try store.exists(key: "test_key"))
        try store.set(key: "test_key", value: "exists")
        XCTAssertTrue(try store.exists(key: "test_key"))
    }

    func testOverwrite() throws {
        try store.set(key: "test_key", value: "v1")
        try store.set(key: "test_key", value: "v2")
        XCTAssertEqual(try store.get(key: "test_key"), "v2")
    }
}
