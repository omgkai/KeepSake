import SwiftUI
import Sparkle

/// One updater per application, shared by all independent save windows.
@MainActor final class UpdateChecker: NSObject, ObservableObject, SPUUpdaterDelegate {
    static let shared = UpdateChecker()
    @Published private(set) var canCheck = false
    @Published private(set) var automaticChecks = true
    @Published private(set) var automaticDownloads = false
    @Published private(set) var message = "Updates are downloaded and installed securely by KeepSake."
    private var started = false
    private lazy var controller = SPUStandardUpdaterController(startingUpdater:false,updaterDelegate:self,userDriverDelegate:nil)
    static var feedURL: String {
        #if KEEPSAKE_UPDATE_QA
        // Compiled only into the isolated updater-test host, never release builds.
        if Bundle.main.bundleIdentifier == "io.keepsake.updater-test",
           let value=Bundle.main.object(forInfoDictionaryKey:"SUFeedURL") as? String,
           let url=URL(string:value),url.host == "127.0.0.1" {return value}
        #endif
        #if arch(arm64)
        return "https://raw.githubusercontent.com/omgkai/KeepSake/main/updates/appcast-arm64.xml"
        #else
        return "https://raw.githubusercontent.com/omgkai/KeepSake/main/updates/appcast-x86_64.xml"
        #endif
    }
    func start() {
        guard !started else{return};started=true
        let defaults=UserDefaults.standard
        if defaults.object(forKey:"SUEnableAutomaticChecks") == nil,let previous=defaults.object(forKey:"automaticUpdateChecks") as? Bool {
            defaults.set(previous,forKey:"SUEnableAutomaticChecks")
        }
        let updater=controller.updater
        updater.publisher(for: \.canCheckForUpdates).assign(to:&$canCheck)
        updater.publisher(for: \.automaticallyChecksForUpdates).assign(to:&$automaticChecks)
        updater.publisher(for: \.automaticallyDownloadsUpdates).assign(to:&$automaticDownloads)
        controller.startUpdater()
    }
    func check(){start();guard canCheck else{return};controller.checkForUpdates(nil)}
    func setAutomaticChecks(_ value:Bool){controller.updater.automaticallyChecksForUpdates=value}
    func setAutomaticDownloads(_ value:Bool){controller.updater.automaticallyDownloadsUpdates=value}
    func feedURLString(for updater:SPUUpdater)->String?{Self.feedURL}
    func allowedSystemProfileKeys(for updater:SPUUpdater)->[String]?{[]}
    func updater(_ updater:SPUUpdater,didFindValidUpdate item:SUAppcastItem){message="KeepSake \(item.displayVersionString) is ready to download."}
    func updaterDidNotFindUpdate(_ updater:SPUUpdater){message="You’re up to date — KeepSake \(KeepSakeRelease.version)."}
    func updater(_ updater:SPUUpdater,didAbortWithError error:Error){message=error.localizedDescription}
    func updater(_ updater:SPUUpdater,didExtractUpdate item:SUAppcastItem){message="Update verified. Ready to install and relaunch."}
    // Sparkle terminates through NSApplication; AppDelegate checks every workspace
    // and can cancel termination without discarding buffers or stopping their engines.
}
