// swift-tools-version: 6.0
import PackageDescription
let package = Package(name: "PKHeXSwift", platforms: [.macOS(.v14)], targets: [
    .executableTarget(name: "PKHeXSwift", path: "Sources", swiftSettings: [.swiftLanguageMode(.v5)])
])
