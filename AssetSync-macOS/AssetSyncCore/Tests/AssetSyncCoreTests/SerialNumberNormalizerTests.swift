import XCTest
@testable import AssetSyncCore

final class SerialNumberNormalizerTests: XCTestCase {
    func testNilReturnsEmpty() {
        XCTAssertEqual(SerialNumberNormalizer.normalize(nil), "")
    }

    func testEmptyStringReturnsEmpty() {
        XCTAssertEqual(SerialNumberNormalizer.normalize(""), "")
    }

    func testWhitespaceOnlyReturnsEmpty() {
        XCTAssertEqual(SerialNumberNormalizer.normalize("   "), "")
    }

    func testTrimsAndUppercases() {
        XCTAssertEqual(SerialNumberNormalizer.normalize("  abc123  "), "ABC123")
    }

    func testRemovesSpecialCharacters() {
        XCTAssertEqual(SerialNumberNormalizer.normalize("AB-CD.12/34"), "ABCD1234")
    }

    func testRealAppleSerial() {
        XCTAssertEqual(SerialNumberNormalizer.normalize("LPG2V3Q402"), "LPG2V3Q402")
    }

    func testMixedCaseWithSpaces() {
        XCTAssertEqual(SerialNumberNormalizer.normalize(" lpg2 v3q 402 "), "LPG2V3Q402")
    }
}
