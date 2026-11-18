// swift-tools-version: 6.0

import PackageDescription

// vdb: sends apps to a running VentureOS. It lives in VentureOS's repository for now, beside the kernel it talks
// to, and will move to one of its own. Swift Argument Parser and swift-log are approved for it alone.
let package = Package(
    name: "vdb",
    products: [
        .executable(name: "vdb", targets: ["vdb"]),
    ],
    dependencies: [
        .package(url: "https://github.com/apple/swift-argument-parser.git", "1.8.2"..<"1.9.0"),
        .package(url: "https://github.com/apple/swift-log.git", "1.16.2"..<"1.17.0"),
    ],
    targets: [
        // What vdb/1 is, apart from the command line, so it can be tested.
        .target(name: "VdbKit"),
        .executableTarget(
            name: "vdb",
            dependencies: [
                "VdbKit",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
                .product(name: "Logging", package: "swift-log"),
            ]
        ),
        .testTarget(name: "VdbKitTests", dependencies: ["VdbKit"]),
    ]
)
