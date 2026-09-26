import Foundation

// Release metadata is public. No account token or save data is sent to GitHub.
enum KeepSakeRelease {
    static let repository = "omgkai/KeepSake"
    static let home = URL(string: "https://github.com/\(repository)")!
    static let releases = home.appendingPathComponent("releases")
    static let issues = home.appendingPathComponent("issues")
    static var version: String { Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0.36" }
    static func numericVersion(_ value: String) -> [Int]? {
        let value = value.hasPrefix("v") ? String(value.dropFirst()) : value
        let parts = value.split(separator: ".", omittingEmptySubsequences: false)
        guard (2...4).contains(parts.count), parts.allSatisfy({ !$0.isEmpty && $0.allSatisfy(\.isNumber) }) else { return nil }
        let numbers = parts.compactMap { Int($0) }
        return numbers.count == parts.count ? numbers : nil
    }
    static func isNewer(_ remote: String, than local: String) -> Bool {
        guard let a = numericVersion(remote), let b = numericVersion(local) else { return false }
        for i in 0..<max(a.count,b.count) {
            let x = i<a.count ? a[i] : 0, y = i<b.count ? b[i] : 0
            if x != y { return x>y }
        }
        return false
    }
}

