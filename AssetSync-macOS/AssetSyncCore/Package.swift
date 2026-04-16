// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AssetSyncCore",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "AssetSyncCore", targets: ["AssetSyncCore"]),
    ],
    targets: [
        .target(name: "AssetSyncCore"),
        .testTarget(name: "AssetSyncCoreTests", dependencies: ["AssetSyncCore"]),
    ]
)
