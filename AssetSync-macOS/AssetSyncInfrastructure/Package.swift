// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "AssetSyncInfrastructure",
    platforms: [.macOS(.v14)],
    products: [
        .library(name: "AssetSyncInfrastructure", targets: ["AssetSyncInfrastructure"]),
    ],
    dependencies: [
        .package(url: "https://github.com/groue/GRDB.swift.git", from: "6.24.0"),
        .package(url: "https://github.com/AzureAD/microsoft-authentication-library-for-objc.git", from: "1.4.0"),
        .package(path: "../AssetSyncCore"),
    ],
    targets: [
        .target(
            name: "AssetSyncInfrastructure",
            dependencies: [
                "AssetSyncCore",
                .product(name: "GRDB", package: "GRDB.swift"),
                .product(name: "MSAL", package: "microsoft-authentication-library-for-objc"),
            ]
        ),
        .testTarget(
            name: "AssetSyncInfrastructureTests",
            dependencies: ["AssetSyncInfrastructure"]
        ),
    ]
)
