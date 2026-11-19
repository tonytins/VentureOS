// swift-tools-version: 6.0

import PackageDescription

// vdb: sends apps to a running VentureOS. It lives in VentureOS's repository for now, beside the kernel it talks
// to, and will move to one of its own. Swift Argument Parser, swift-log, and swift-crypto are approved for it alone.
let package = Package(
    name: "vdb",
    products: [
        .executable(name: "vdb", targets: ["vdb"]),
    ],
    dependencies: [
        .package(url: "https://github.com/apple/swift-argument-parser.git", "1.8.2"..<"1.9.0"),
        .package(url: "https://github.com/apple/swift-log.git", "1.16.2"..<"1.17.0"),
        // SHA-256, HKDF, and ChaCha20-Poly1305 for the sealed connection, on every platform, as CryptoKit is on Apple's.
        .package(url: "https://github.com/apple/swift-crypto.git", "5.1.2"..<"5.2.0"),
        // main, for GITVERSION_IGNORE_TAG_VERSION in GitVersion.env, until a release has it.
        .package(url: "https://github.com/tonytins/GitVersion.git", branch: "main"),
    ],
    targets: [
        // What vdb/1 is, apart from the command line, so it can be tested.
        .target(name: "VdbKit", dependencies: [.product(name: "Crypto", package: "swift-crypto")]),
        .executableTarget(
            name: "vdb",
            dependencies: [
                "VdbKit",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
                .product(name: "Logging", package: "swift-log"),
                .product(name: "GitVersion", package: "GitVersion"),
            ],
            plugins: [.plugin(name: "GitVersionPlugin", package: "GitVersion")]
        ),
        .testTarget(name: "VdbKitTests", dependencies: ["VdbKit"]),
    ]
)
