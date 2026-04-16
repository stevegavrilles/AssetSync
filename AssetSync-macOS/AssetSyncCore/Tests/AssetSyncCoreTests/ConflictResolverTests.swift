import XCTest
@testable import AssetSyncCore

final class ConflictResolverTests: XCTestCase {
    private let resolver = ConflictResolver()

    func testNameAlwaysPushedFromMdm() {
        var snipe = Device()
        snipe.deviceName = "Old Name"
        var mdm = Device()
        mdm.deviceName = "New Name"

        let updates = resolver.getUpdatesToApply(snipeItAsset: snipe, mdmDevice: mdm)
        XCTAssertEqual(updates["name"] as? String, "New Name")
    }

    func testSerialNeverOverwritten() {
        var snipe = Device()
        snipe.serialNumber = "EXISTING"
        var mdm = Device()
        mdm.serialNumber = "DIFFERENT"

        let updates = resolver.getUpdatesToApply(snipeItAsset: snipe, mdmDevice: mdm)
        XCTAssertNil(updates["serial"])
    }

    func testEmptySnipeFieldFilledByDefault() {
        var snipe = Device()
        snipe.osVersion = nil
        var mdm = Device()
        mdm.osVersion = "10.0.22631.4890"

        let updates = resolver.getUpdatesToApply(snipeItAsset: snipe, mdmDevice: mdm)
        XCTAssertEqual(updates["os_version"] as? String, "10.0.22631.4890")
    }

    func testExistingSnipeFieldNotOverwrittenByDefault() {
        var snipe = Device()
        snipe.osVersion = "10.0.19045"
        var mdm = Device()
        mdm.osVersion = "10.0.22631.4890"

        let updates = resolver.getUpdatesToApply(snipeItAsset: snipe, mdmDevice: mdm)
        XCTAssertNil(updates["os_version"])
    }

    func testMdmWinsOverwritesExisting() {
        var snipe = Device()
        snipe.osVersion = "10.0.19045"
        var mdm = Device()
        mdm.osVersion = "10.0.22631.4890"

        let updates = resolver.getUpdatesToApply(snipeItAsset: snipe, mdmDevice: mdm, mdmWins: true)
        XCTAssertEqual(updates["os_version"] as? String, "10.0.22631.4890")
    }

    func testDiscrepancyDetected() {
        var snipe = Device()
        snipe.serialNumber = "AAA"
        snipe.snipeItModelId = 1
        var mdm = Device()
        mdm.serialNumber = "BBB"
        mdm.snipeItModelId = 2

        let disc = resolver.getDiscrepancies(snipeItAsset: snipe, mdmDevice: mdm)
        XCTAssertEqual(disc.count, 2)
    }
}
