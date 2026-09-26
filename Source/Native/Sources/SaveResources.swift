import SwiftUI
import AppKit

private struct DiscoveredSave: Decodable, Identifiable {
    var id: String { path }
    let path, name, game, trainer, modified: String
    let size: Int64
}
private struct SaveDiscovery: Decodable {
    let entries: [DiscoveredSave]
    let examined, unreadable: Int
    let limited: Bool
}
private struct OriginalBackup: Decodable, Identifiable {
    let id, name, created: String
    let size: Int64
}
struct SaveResourcesView: View {
    @State private var showTroubleshooter=false
    @EnvironmentObject private var model: EditorModel
    @Environment(\.gameTheme) private var theme
    @AppStorage("reopenLastDocument") private var reopen = false
    @AppStorage("saveDiscoveryFolder") private var folder = ""
    @AppStorage("saveDiscoveryRecursive") private var recursive = true
    @State private var discovery: SaveDiscovery?
    @State private var backups: [OriginalBackup] = []
    @State private var working = false
    var body: some View {
        ScrollView {
            VStack(alignment:.leading, spacing:24) {
                GroupBox {
                    VStack(alignment:.leading,spacing:12) {
                        Button("Open with Format Options…"){showTroubleshooter=true}.sheet(isPresented:$showTroubleshooter){SaveTroubleshooterView()}
                        Toggle("Reopen my last save or Pokémon file", isOn:$reopen)
                        Text("KeepSake opens the file from disk. Unsaved edits are not restored.").font(.caption).foregroundStyle(.secondary)
                        if let field = model.fields.first(where:{$0.id == "BackupOnOpen"}) { FieldRow(field:field,target:"settings") }
                        Text("Original saves are backed up before opening. Identical copies share one snapshot; earlier revisions are kept.").font(.caption).foregroundStyle(.secondary)
                    }.padding(10)
                } label: { Label("A familiar starting point",systemImage:"sunrise.fill").foregroundStyle(theme.accent) }
                GroupBox {
                    VStack(alignment:.leading,spacing:14) {
                        HStack {
                            VStack(alignment:.leading,spacing:4) { Text(folder.isEmpty ? "Choose where your saves live" : URL(fileURLWithPath:folder).lastPathComponent).font(.headline); Text(folder.isEmpty ? "Search a folder for saves recognized by PKHeX." : folder).font(.caption).foregroundStyle(.secondary).lineLimit(2) }
                            Spacer()
                            Button("Choose Folder…",action:chooseFolder)
                            Button("Scan",action:scan).disabled(folder.isEmpty)
                        }
                        Toggle("Include subfolders",isOn:$recursive).toggleStyle(.checkbox)
                        if let discovery {
                            Text("\(discovery.entries.count) saves · \(discovery.examined) files checked" + (discovery.limited ? " · Scan limit reached; choose a smaller folder." : "") + (discovery.unreadable > 0 ? " · \(discovery.unreadable) files could not be read." : "")).font(.caption).foregroundStyle(.secondary)
                            if discovery.entries.isEmpty { Label("No recognized saves in this folder",systemImage:"folder.badge.questionmark").frame(maxWidth:.infinity).padding(24).foregroundStyle(.secondary) }
                            ForEach(discovery.entries) { entry in
                                HStack { Image(systemName:"doc.fill").foregroundStyle(theme.accent); VStack(alignment:.leading) { Text(entry.game).font(.headline); Text("\(entry.trainer) · \(entry.name)").font(.caption).foregroundStyle(.secondary) }; Spacer(); Button("Open") { model.openURL(URL(fileURLWithPath:entry.path)) } }.padding(.vertical,5).help(entry.path)
                            }
                        }
                    }.padding(10)
                } label: { Label("Your save collection",systemImage:"folder.fill").foregroundStyle(theme.accent) }
                GroupBox {
                    VStack(alignment:.leading,spacing:12) {
                        HStack { Text("Original save snapshots").font(.headline); Spacer(); Button("Refresh") { Task { await refresh() } } }
                        Text("Export a backup as a new file, then open it to restore that revision. Existing saves are never replaced.").font(.caption).foregroundStyle(.secondary)
                        if backups.isEmpty { Label("Backups appear after opening a save",systemImage:"clock.arrow.circlepath").frame(maxWidth:.infinity).padding(24).foregroundStyle(.secondary) }
                        ForEach(backups) { backup in
                            HStack { Image(systemName:"externaldrive.badge.timemachine").foregroundStyle(theme.accent); VStack(alignment:.leading) { Text(backup.name).font(.headline); Text("\(String(backup.created.prefix(19)).replacingOccurrences(of:"T",with:" ")) UTC · \(ByteCountFormatter.string(fromByteCount:backup.size,countStyle:.file))").font(.caption).foregroundStyle(.secondary) }; Spacer(); Button("Export Copy…") { export(backup) } }.padding(.vertical,5)
                        }
                    }.padding(10)
                } label: { Label("Always a way back",systemImage:"arrow.uturn.backward.circle.fill").foregroundStyle(theme.accent) }
                if working { ProgressView("Working with your local files…") }
            }.padding(26)
        }.disabled(working || model.busy).task { await refresh() }
    }
    private func chooseFolder() {
        let panel=NSOpenPanel(); panel.canChooseDirectories=true; panel.canChooseFiles=false; panel.title="Choose your save folder"
        if panel.runModal() == .OK, let url=panel.url { folder=url.path; discovery=nil; scan() }
    }
    private func scan() {
        guard !working, !model.busy else { return }; working=true
        Task { do { discovery=try await model.bridge.send(["op":"saveDiscover","path":folder,"recursive":recursive],as:SaveDiscovery.self) } catch { model.error=error.localizedDescription }; working=false }
    }
    private func refresh() async {
        guard !working else { return }; working=true
        do { backups=try await model.bridge.send(["op":"saveBackups"],as:[OriginalBackup].self) } catch { model.error=error.localizedDescription }; working=false
    }
    private func export(_ backup: OriginalBackup) {
        let panel=NSSavePanel();panel.title="Export original save backup";panel.nameFieldStringValue=backup.name + ".restored"
        guard panel.runModal() == .OK, let url=panel.url else { return }; working=true
        Task { struct Result:Decodable { let path:String }; do { _ = try await model.bridge.send(["op":"saveBackupExport","id":backup.id,"path":url.path],as:Result.self); model.status="Exported original save backup" } catch { model.error=error.localizedDescription }; working=false }
    }
}
