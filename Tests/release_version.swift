import Foundation
@main struct ReleaseVersionTests {
 static func main() {
    for (remote,local,expected) in [("v0.36","0.35",true),("0.36.1","0.36",true),("0.36","0.36.0",false),("0.9","0.10",false),("v1.0","0.99",true),("0.36-rc1","0.35",false),("garbage","0.35",false),("0..36","0.35",false),("0.36","bad",false),("0.36.0.1","0.36",true),("0.036","0.36",false)] {
        precondition(KeepSakeRelease.isNewer(remote,than:local)==expected,"\(remote) / \(local)")
    }
    print("PASS: 11 release version comparisons, including malformed and prerelease tags")
 }
}
