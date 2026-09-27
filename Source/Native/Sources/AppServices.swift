import SwiftUI
import AppKit

struct UpdateSettingsView: View {
    @ObservedObject private var updater=UpdateChecker.shared
    var body: some View {
        VStack(alignment:.leading,spacing:14) {
            Label("A new chapter, always within reach",systemImage:"arrow.triangle.2.circlepath").font(.headline)
            Toggle("Automatically check for updates",isOn:Binding(get:{updater.automaticChecks},set:{updater.setAutomaticChecks($0)}))
            Toggle("Download and install updates automatically",isOn:Binding(get:{updater.automaticDownloads},set:{updater.setAutomaticDownloads($0)}))
                .disabled(!updater.automaticChecks)
            Text("KeepSake downloads, verifies and installs updates for this Mac. Choose Install and Relaunch when ready. Automatic downloads install when you quit. Unsaved work in every open window is checked before quitting.").font(.caption).foregroundStyle(.secondary)
            Button("Check for Updates") {updater.check()}.disabled(!updater.canCheck).buttonStyle(.borderedProminent)
            Text(updater.message).font(.callout).foregroundStyle(.secondary)
        }.padding(22).frame(maxWidth:.infinity,alignment:.leading).background(.quaternary.opacity(0.4),in:RoundedRectangle(cornerRadius:20))
    }
}

@MainActor final class PersonalBackup: ObservableObject {
    static let shared=PersonalBackup()
    @Published var message="Choose a folder in iCloud Drive to keep dated copies of your memories and original-save backups."
    @Published var working=false
    private let key="personalBackupFolder"
    var configured: Bool { UserDefaults.standard.data(forKey:key) != nil }
    func choose() {
        let panel=NSOpenPanel();panel.title="Choose a KeepSake backup folder";panel.message="Select a folder in iCloud Drive, or another backup location.";panel.canChooseDirectories=true;panel.canChooseFiles=false;panel.canCreateDirectories=true
        guard panel.runModal() == .OK, let url=panel.url else{return}
        do { let bookmark=try url.bookmarkData(options:.withSecurityScope,includingResourceValuesForKeys:nil,relativeTo:nil);UserDefaults.standard.set(bookmark,forKey:key);message="Backup folder: \(url.lastPathComponent)" } catch { message=error.localizedDescription }
    }
    func backup(journals:JournalStore) async {
        guard !working else{return};working=true;defer{working=false}
        do {
            guard let bookmark=UserDefaults.standard.data(forKey:key) else {throw JournalError("Choose a backup folder first.")}
            var stale=false
            let folder=try URL(resolvingBookmarkData:bookmark,options:.withSecurityScope,relativeTo:nil,bookmarkDataIsStale:&stale)
            guard !stale else{throw JournalError("Choose your backup folder again to renew access.")}
            let scoped=folder.startAccessingSecurityScopedResource();defer{if scoped{folder.stopAccessingSecurityScopedResource()}}
            let journal=try journals.exportData()
            let preferences=UserDefaults.standard.dictionaryRepresentation().filter { key,_ in
                ["appearance","gameTheme","matchGameTheme","customThemeColors","themeAccentHex","themeCompanionHex","pokemonArtworkStyle","keepsakeOwnerName"].contains(key)
            }
            let preferencesData=try PropertyListSerialization.data(fromPropertyList:preferences,format:.xml,options:0)
            let fm=FileManager.default
            let support=fm.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0]
            let saveBackups=support.appendingPathComponent("PKHeXSwift/Save Backups")
            let stamp=ISO8601DateFormatter().string(from:Date()).replacingOccurrences(of:":",with:"-")
            let resolvedFolder=folder.resolvingSymlinksInPath().standardizedFileURL.path
            let resolvedBackups=saveBackups.resolvingSymlinksInPath().standardizedFileURL.path
            guard resolvedFolder != resolvedBackups, !resolvedFolder.hasPrefix(resolvedBackups+"/") else { throw JournalError("Choose a folder outside the original-save backup folder.") }
            let destination=folder.appendingPathComponent("KeepSake-\(stamp)-\(UUID().uuidString.prefix(8))",isDirectory:true)
            try await Task.detached(priority:.utility) {
                let fm=FileManager.default
                try fm.createDirectory(at:destination,withIntermediateDirectories:true)
                do {
                    try journal.write(to:destination.appendingPathComponent("Journal.json"),options:.atomic)
                    try preferencesData.write(to:destination.appendingPathComponent("Appearance.plist"),options:.atomic)
                    if fm.fileExists(atPath:saveBackups.path) {try fm.copyItem(at:saveBackups,to:destination.appendingPathComponent("Original Save Backups"))}
                    let readme="KeepSake backup\nImport Journal.json from My Journal to merge memories and teams. Original Save Backups contains immutable .bak saves; open a copy in KeepSake. Appearance.plist records your basic visual preferences. Unsaved editor changes and arbitrary source files are not included. Cloud upload is managed by macOS, not confirmed by KeepSake.\n"
                    try readme.write(to:destination.appendingPathComponent("RESTORE.txt"),atomically:true,encoding:.utf8)
                } catch {try? fm.removeItem(at:destination);throw error}
            }.value
            message="Backup created: \(destination.lastPathComponent). macOS manages any iCloud upload."
            UserDefaults.standard.set(Date().timeIntervalSince1970,forKey:"lastPersonalBackup")
        } catch {message="Backup not completed: "+error.localizedDescription}
    }
}

struct PersonalBackupView: View {
    @EnvironmentObject var journals:JournalStore
    @ObservedObject private var backup=PersonalBackup.shared
    var body:some View {
        VStack(alignment:.leading,spacing:14) {
            Label("Keep your memories close",systemImage:"icloud.and.arrow.up").font(.headline)
            Text("Journal pages, teams, basic appearance preferences, and existing original-save backups. Choose iCloud Drive for cloud storage. Keychain is reserved for credentials.").font(.caption).foregroundStyle(.secondary)
            HStack {Button("Choose Backup Folder…"){backup.choose()};Button(backup.working ? "Backing Up…":"Back Up Now"){Task{await backup.backup(journals:journals)}}.disabled(!backup.configured || backup.working)}
            Text(backup.message).font(.caption).foregroundStyle(.secondary)
        }.padding(22).frame(maxWidth:.infinity,alignment:.leading).background(.quaternary.opacity(0.4),in:RoundedRectangle(cornerRadius:20))
    }
}

struct AboutKeepSakeView:View {
    @Environment(\.gameTheme) private var theme
    @Environment(\.openWindow) private var openWindow
    var body:some View {
        ScrollView {
            VStack(spacing:22) {
                JournalMark().frame(width:94,height:94).shadow(color:theme.accent.opacity(0.25),radius:24,y:10)
                VStack(spacing:7){Text("KeepSake").font(.system(size:38,weight:.bold,design:.rounded));Text("Every companion has a story.").font(.title3).foregroundStyle(.secondary);Text("Version \(KeepSakeRelease.version)").font(.caption.monospaced()).foregroundStyle(.secondary)}
                Text("A personal home for your Pokémon adventures. Native editing, thoughtful details, and a journal for the memories that make each team yours.").multilineTextAlignment(.center).lineSpacing(4)
                HStack(spacing:22){Label("Made for Mac",systemImage:"apple.logo");Label("Your own story",systemImage:"book.closed.fill")}.font(.caption).foregroundStyle(theme.accent)
                UpdateSettingsView()
                HStack {Link("Source & Credits",destination:KeepSakeRelease.home);Spacer();Button("Support"){openWindow(id:"support")}}
                Text("Powered by PKHeX, by Kaphotics and contributors, and Auto-Legality Mod. KeepSake is an unofficial project distributed under GPL-3.0-or-later. Pokémon and related artwork belong to their respective owners.").font(.caption).foregroundStyle(.secondary).multilineTextAlignment(.center)
                Button("Licenses & Artwork Notices") { if let url=Bundle.main.url(forResource:"THIRD-PARTY-NOTICES",withExtension:"md"){NSWorkspace.shared.open(url)} }.buttonStyle(.link)
            }.padding(36).frame(maxWidth:620)
                .frame(maxWidth:.infinity)
        }.background(LinearGradient(colors:[theme.accent.opacity(0.12),Color(nsColor:.windowBackgroundColor),theme.companion.opacity(0.08)],startPoint:.topLeading,endPoint:.bottomTrailing))
    }
}

struct KeepSakeSupportView:View {
    @Environment(\.gameTheme) private var theme
    @State private var copied=false
    var body:some View {
        ScrollView {
            VStack(alignment:.leading,spacing:24) {
                HStack(spacing:18){Image(systemName:"heart.text.clipboard.fill").font(.system(size:40)).foregroundStyle(theme.accent);VStack(alignment:.leading,spacing:5){Text("Here for your next chapter").font(.system(size:27,weight:.bold,design:.rounded));Text("Help, updates, and a safe place for your memories.").foregroundStyle(.secondary)}}
                GroupBox {VStack(alignment:.leading,spacing:12){Text("Something not quite right?").font(.headline);Text("Describe the game, what you expected, and what happened. Keep personal saves and trainer details out of public reports.").foregroundStyle(.secondary);HStack {Link("Report an Issue",destination:KeepSakeRelease.issues.appendingPathComponent("new"));Spacer();Button(copied ? "Copied":"Copy App Details"){let text="KeepSake \(KeepSakeRelease.version)\nmacOS \(ProcessInfo.processInfo.operatingSystemVersionString)\nArchitecture: \(architecture)";NSPasteboard.general.clearContents();NSPasteboard.general.setString(text,forType:.string);copied=true}}}.padding(12).frame(maxWidth:.infinity,alignment:.leading)}
                PersonalBackupView()
                GroupBox {VStack(alignment:.leading,spacing:12){Label("Start with a copy",systemImage:"doc.on.doc").font(.headline);Text("Open a save or drop it into the editor. Export Copy writes a separate edited save. Journal backups can be imported from My Journal to merge memories and teams.");Link("Read the Guide",destination:KeepSakeRelease.home.appendingPathComponent("blob/main/README.md"))}.padding(12).frame(maxWidth:.infinity,alignment:.leading)}
                UpdateSettingsView()
            }.padding(32).frame(maxWidth:700).frame(maxWidth:.infinity)
        }.background(LinearGradient(colors:[theme.accent.opacity(0.08),Color(nsColor:.windowBackgroundColor)],startPoint:.top,endPoint:.bottom))
    }
    private var architecture:String {
        #if arch(arm64)
        return "Apple Silicon"
        #else
        return "Intel"
        #endif
    }
}
