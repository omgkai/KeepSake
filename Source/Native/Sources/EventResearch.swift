import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct EventResearchEntry: Codable, Identifiable {
    let id: String
    let index: Int
    let section, name, category, value, kind: String
    let choices: [Choice]
    let named: Bool
}
struct EventRecordSelection: Identifiable { let id = UUID(); let entry: EventResearchEntry; let revision: Int }
struct EventResearchData: Codable { let revision: Int; let supported: Bool; let entries: [EventResearchEntry] }
struct EventChange: Codable, Identifiable { let id: String; let index: Int; let section, name, before, after: String }
struct EventComparisonData: Codable { let token, before, after: String; let unchanged: Int; let entries: [EventChange]; let report: String }

struct EventFlagsView: View {
    @EnvironmentObject var model: EditorModel
    @Environment(\.gameTheme) private var theme
    @State private var data: EventResearchData?
    @State private var search = ""
    @State private var section = "Flags"
    @State private var category = "All categories"
    @State private var namedOnly = true
    @State private var setOnly = false
    @State private var selected: EventRecordSelection?
    @State private var comparing = false
    @State private var loadError: String?
    private var sections: [String] { ["Flags", "System flags", "Variables"].filter { key in data?.entries.contains { $0.section == key } == true } }
    private var sectionRows: [EventResearchEntry] { data?.entries.filter { $0.section == section } ?? [] }
    private var categories: [String] { Array(Set(sectionRows.filter(\.named).map(\.category))).sorted() }
    private var hasNames: Bool { sectionRows.contains(where: \.named) }
    private var visible: [EventResearchEntry] {
        sectionRows.filter { row in
            (!namedOnly || !hasNames || row.named) && (category == "All categories" || row.category == category) &&
            (!setOnly || row.kind != "bool" || row.value == "true") &&
            (search.isEmpty || row.name.localizedCaseInsensitiveContains(search) || String(row.index).contains(search))
        }
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack {
                VStack(alignment: .leading, spacing: 6) {
                    Label("Story & event records", systemImage: "flag.checkered").font(.title2.bold())
                    Text("Explore PKHeX’s researched labels, stored variables and changes between saves.").foregroundStyle(.secondary)
                }
                Spacer()
                Button { comparing = true } label: { Label("Compare Saves…", systemImage: "arrow.left.arrow.right") }.disabled(!model.state.canEvents)
            }
            if !model.state.canEvents {
                WorkspaceEmptyState(title: "Event records unavailable", icon: "flag", message: "Open a supported game save. Games with keyed Switch records also have a block browser in Save Tools.")
            } else if let data {
                Picker("Records", selection: $section) { ForEach(sections, id: \.self) { Text($0).tag($0) } }.pickerStyle(.segmented)
                    .onChange(of: section) { _, _ in category = "All categories"; setOnly = false }
                HStack {
                    TextField("Find a name or index…", text: $search).textFieldStyle(.roundedBorder)
                    if hasNames { Toggle("Known labels", isOn: $namedOnly).toggleStyle(.checkbox) }
                    if section != "Variables" { Toggle("Set only", isOn: $setOnly).toggleStyle(.checkbox) }
                }
                HStack {
                    if !categories.isEmpty {
                        Picker("Category", selection: $category) { Text("All categories").tag("All categories"); ForEach(categories, id: \.self) { Text($0).tag($0) } }.frame(maxWidth: 330)
                    }
                    Spacer()
                    Text("\(visible.count) shown · \(sectionRows.count) stored").font(.caption).foregroundStyle(.secondary)
                }
                ScrollView {
                    LazyVStack(spacing: 9) {
                        ForEach(visible) { row in
                            Button { selected = EventRecordSelection(entry: row, revision: data.revision) } label: {
                                HStack(spacing: 14) {
                                    Image(systemName: row.kind == "bool" ? (row.value == "true" ? "checkmark.circle.fill" : "circle") : "number.square.fill")
                                        .font(.title2).foregroundStyle(row.value == "true" || row.kind != "bool" ? theme.accent : .secondary)
                                        .frame(width: 32)
                                    VStack(alignment: .leading, spacing: 4) {
                                        Text(row.name).font(.headline).multilineTextAlignment(.leading)
                                        Text("\(row.category) · Index \(row.index)").font(.caption).foregroundStyle(.secondary)
                                    }
                                    Spacer()
                                    Text(display(row)).font(.callout.weight(.medium)).foregroundStyle(theme.accent).lineLimit(2).frame(maxWidth: 200, alignment: .trailing)
                                    Image(systemName: "chevron.right").font(.caption).foregroundStyle(.tertiary)
                                }.padding(14).background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 15))
                                    .overlay(RoundedRectangle(cornerRadius: 15).stroke(theme.accent.opacity(0.14)))
                            }.buttonStyle(.plain)
                        }
                    }
                }.overlay { if visible.isEmpty { ContentUnavailableView.search(text: search) } }
                Text("Labels follow the loaded game. Unlabeled indices remain available; edits can change story progress. Undo restores the previous values.").font(.caption).foregroundStyle(.secondary)
                // Keep the selected entry tied to the revision that supplied its values.
                .sheet(item: $selected) { entry in EventRecordEditor(entry: entry.entry, revision: entry.revision) }
            } else if let loadError {
                WorkspaceEmptyState(title: "Couldn’t load event records", icon: "arrow.clockwise", message: loadError) { Button("Try Again") { Task { await load() } } }
            } else { ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity) }
        }.padding(28).frame(maxWidth: 1100, maxHeight: .infinity, alignment: .topLeading).frame(maxWidth: .infinity)
            .task(id: model.state.revision) { await load() }
            .sheet(isPresented: $comparing) { EventCompareView() }
    }
    private func display(_ row: EventResearchEntry) -> String { row.kind == "bool" ? (row.value == "true" ? "Set" : "Not set") : row.choices.first { $0.value == row.value }?.label ?? row.value }
    private func load() async {
        guard model.state.canEvents else { data = nil; return }
        do { data = try await model.bridge.send(["op": "eventResearch"], as: EventResearchData.self); loadError = nil; if !sections.contains(section) { section = sections.first ?? "Flags" } }
        catch { data = nil; loadError = error.localizedDescription }
    }
}

private struct EventRecordEditor: View {
    @EnvironmentObject var model: EditorModel
    @Environment(\.dismiss) private var dismiss
    let entry: EventResearchEntry
    let revision: Int
    @State private var value = ""
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            Label(entry.name, systemImage: entry.kind == "bool" ? "flag.fill" : "number.square.fill").font(.title2.bold())
            Text("\(entry.section) · \(entry.category) · Index \(entry.index)").font(.callout).foregroundStyle(.secondary)
            if entry.kind == "bool" {
                Toggle("Flag is set", isOn: Binding(get: { value == "true" }, set: { value = $0 ? "true" : "false" })).toggleStyle(.switch)
            } else {
                if !entry.choices.isEmpty {
                    Picker("Known value", selection: $value) {
                        if !entry.choices.contains(where: { $0.value == value }) { Text("Custom · \(value)").tag(value) }
                        ForEach(entry.choices) { Text($0.label).tag($0.value) }
                    }
                }
                TextField("Stored value", text: $value).textFieldStyle(.roundedBorder)
                Text(bounds).font(.caption).foregroundStyle(.secondary)
            }
            Spacer(minLength: 0)
            HStack { Button("Cancel") { dismiss() }.keyboardShortcut(.cancelAction); Spacer(); Button("Save Changes") { Task { await model.command(["op": "eventResearchSet", "id": entry.id, "value": value, "revision": revision]); if model.error == nil { dismiss() } } }.buttonStyle(.borderedProminent).keyboardShortcut(.defaultAction).disabled(value == entry.value || model.busy || model.fieldDrafts) }
        }.padding(26).frame(width: 560, height: entry.kind == "bool" ? 260 : 340).onAppear { value = entry.value }
    }
    private var bounds: String { switch entry.kind { case "Byte": "0–255"; case "UInt16": "0–65,535"; case "UInt32": "0–4,294,967,295"; case "Int32": "−2,147,483,648–2,147,483,647"; default: "A finite number in this record’s storage range" } }
}

struct EventCompareView: View {
    var operation = "eventCompare"
    @EnvironmentObject var model: EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var previous = ""
    @State private var updated = ""
    @State private var useWorkspace = true
    @State private var result: EventComparisonData?
    @State private var busy = false
    @State private var message = ""
    @State private var search = ""
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack { Label("Compare event records", systemImage: "arrow.left.arrow.right").font(.title2.bold()); Spacer(); Button("Done") { dismiss() }.keyboardShortcut(.cancelAction) }
            Text("Compare two saves from the same game family, or compare an earlier save with your current workspace. This creates a read-only snapshot.").foregroundStyle(.secondary)
            HStack(alignment: .top, spacing: 18) {
                sourceCard("Before", path: previous) { choose(before: true) }
                sourceCard("After", path: useWorkspace ? "Current workspace" : updated) { choose(before: false) }.disabled(useWorkspace)
            }.disabled(busy)
            HStack { Toggle("Use current workspace as After", isOn: $useWorkspace).onChange(of: useWorkspace) { _, _ in result = nil }.disabled(busy); if busy { ProgressView().controlSize(.small) }; Text(message).font(.caption).foregroundStyle(.secondary); Spacer(); Button("Compare") { compare() }.buttonStyle(.borderedProminent).disabled(previous.isEmpty || (!useWorkspace && updated.isEmpty) || busy) }
            Divider()
            if let result {
                HStack { Text("\(result.entries.count) changed · \(result.unchanged) unchanged").font(.headline); Spacer(); Button("Copy Report") { NSPasteboard.general.clearContents(); NSPasteboard.general.setString(result.report, forType: .string) }; Button("Export Report…") { export(result) } }
                TextField("Find a changed name or index…", text: $search).textFieldStyle(.roundedBorder)
                ScrollView { LazyVStack(alignment: .leading, spacing: 10) {
                    ForEach(result.entries.filter { search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || String($0.index).contains(search) }) { row in
                        HStack { VStack(alignment: .leading, spacing: 4) { Text(row.name).font(.headline); Text("\(row.section) · Index \(row.index)").font(.caption).foregroundStyle(.secondary) }; Spacer(); Text(row.before).foregroundStyle(.secondary); Image(systemName: "arrow.right").foregroundStyle(.tertiary); Text(row.after).fontWeight(.semibold).foregroundStyle(.tint) }.padding(12).frame(maxWidth: .infinity).background(.quaternary.opacity(0.35), in: RoundedRectangle(cornerRadius: 12))
                    }
                }}.overlay { if result.entries.isEmpty { ContentUnavailableView("No event changes", systemImage: "checkmark.circle", description: Text("The compared event records match.")) } }
            } else { ContentUnavailableView("Follow your adventure’s changes", systemImage: "flag.2.crossed", description: Text("Choose an earlier save to see which flags and variables changed.")).frame(maxWidth: .infinity, maxHeight: .infinity) }
        }.padding(26).frame(width: 850, height: 650)
    }
    private func sourceCard(_ title: String, path: String, action: @escaping () -> Void) -> some View {
        VStack(alignment: .leading, spacing: 10) { Text(title).font(.headline); Label(path.isEmpty ? "Choose a save file" : URL(fileURLWithPath: path).lastPathComponent, systemImage: "doc").font(.callout).lineLimit(1).truncationMode(.middle); Button("Choose File…", action: action) }.padding(16).frame(maxWidth: .infinity, alignment: .leading).background(.quaternary.opacity(0.3), in: RoundedRectangle(cornerRadius: 15))
    }
    private func choose(before: Bool) { let panel = NSOpenPanel(); panel.allowsMultipleSelection = false; guard panel.runModal() == .OK, let path = panel.url?.path else { return }; if before { previous = path } else { updated = path }; result = nil; message = "" }
    private func compare() { busy = true; result = nil; message = ""; Task { defer { busy = false }; do { result = try await model.bridge.send(["op": operation, "previous": previous, "updated": useWorkspace ? "" : updated], as: EventComparisonData.self) } catch { message = error.localizedDescription } } }
    private func export(_ result: EventComparisonData) { let panel = NSSavePanel(); panel.allowedContentTypes = [.plainText]; panel.nameFieldStringValue = "KeepSake-Event-Changes.txt"; guard panel.runModal() == .OK, let url = panel.url else { return }; Task { do { _ = try await model.bridge.send(["op": "eventCompareExport", "token": result.token, "path": url.path], as: PathResult.self); message = "Report exported." } catch { message = error.localizedDescription } } }
}
