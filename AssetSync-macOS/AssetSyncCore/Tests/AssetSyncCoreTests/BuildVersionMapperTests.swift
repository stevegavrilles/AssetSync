import XCTest
@testable import AssetSyncCore

final class BuildVersionMapperTests: XCTestCase {
    func testExtractBuildNumberFromFullVersion() {
        XCTAssertEqual(BuildVersionMapper.extractBuildNumber(from: "10.0.22631.4890"), "22631")
    }

    func testExtractBuildNumberFromThreeParts() {
        XCTAssertEqual(BuildVersionMapper.extractBuildNumber(from: "10.0.19045"), "19045")
    }

    func testNilInput() {
        XCTAssertNil(BuildVersionMapper.extractBuildNumber(from: nil))
    }

    func testEmptyInput() {
        XCTAssertNil(BuildVersionMapper.extractBuildNumber(from: ""))
    }

    func testNonNumericThirdPart() {
        XCTAssertNil(BuildVersionMapper.extractBuildNumber(from: "macOS.Ventura.abc"))
    }

    func testTwoPartsOnly() {
        XCTAssertNil(BuildVersionMapper.extractBuildNumber(from: "10.0"))
    }
}
