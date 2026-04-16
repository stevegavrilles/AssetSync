import XCTest
@testable import AssetSyncCore

final class DeviceMergerTests: XCTestCase {
    private let merger = DeviceMerger()

    func testIntuneOnlyDevice() {
        var d = Device()
        d.serialNumber = "ABC123"
        d.normalizedSerial = "ABC123"
        d.deviceName = "Test-PC"
        d.platformSource = "Intune"

        let result = merger.merge(intuneDevices: [d], iruDevices: [])
        XCTAssertEqual(result.count, 1)
        XCTAssertEqual(result[0].deviceName, "Test-PC")
        XCTAssertEqual(result[0].platformSource, "Intune")
    }

    func testIruOnlyDevice() {
        var d = Device()
        d.serialNumber = "LPG2V3Q402"
        d.normalizedSerial = "LPG2V3Q402"
        d.deviceName = "MacBook"
        d.platformSource = "Iru"

        let result = merger.merge(intuneDevices: [], iruDevices: [d])
        XCTAssertEqual(result.count, 1)
        XCTAssertEqual(result[0].platformSource, "Iru")
    }

    func testMergedDeviceFillsEmptyFields() {
        var intune = Device()
        intune.serialNumber = "ABC123"
        intune.normalizedSerial = "ABC123"
        intune.deviceName = "Test-PC"
        intune.platformSource = "Intune"

        var iru = Device()
        iru.serialNumber = "ABC123"
        iru.normalizedSerial = "ABC123"
        iru.model = "MacBook Pro"
        iru.platformSource = "Iru"

        let result = merger.merge(intuneDevices: [intune], iruDevices: [iru])
        XCTAssertEqual(result.count, 1)
        XCTAssertEqual(result[0].deviceName, "Test-PC")  // Intune first
        XCTAssertEqual(result[0].model, "MacBook Pro")   // Filled from Iru
    }

    func testEmptySerialSkipped() {
        var d = Device()
        d.serialNumber = ""
        d.normalizedSerial = ""
        d.platformSource = "Intune"

        let result = merger.merge(intuneDevices: [d], iruDevices: [])
        XCTAssertEqual(result.count, 0)
    }
}
