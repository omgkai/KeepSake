import SwiftUI

struct MemoryPage: Codable { let revision: Int; let entityKey: String; let entries: [ExtraRow] }
private struct MemorySelection: Identifiable { let id = UUID(); let row: ExtraRow; let revision: Int; let entityKey: String }
struct MemoriesAndCareView: View {
    @EnvironmentObject var model: EditorModel
    @Environment(\.gameTheme) private var theme
    @State private var page: MemoryPage?
    @State private var selection: MemorySelection?
    @State private var error: String?
    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("Memories & care").font(.title3.bold())
            Text("The people, places and little moments stored with your Pokémon.").font(.callout).foregroundStyle(.secondary)
            if let page {
                ForEach(page.entries) { row in
                    Button { selection = MemorySelection(row: row, revision: page.revision, entityKey: page.entityKey) } label: {
                        VStack(alignment: .leading, spacing: 12) {
                            HStack { Image(systemName: row.id == "residences" ? "globe.americas.fill" : row.id == "care" ? "heart.fill" : "person.crop.circle.fill").foregroundStyle(theme.accent); Text(row.name).font(.headline); Spacer(); Image(systemName: "chevron.right").font(.caption).foregroundStyle(.secondary) }
                            Text(row.detail).font(.callout).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                            if let friendship = row.fields.first(where: { $0.id.hasSuffix("Friendship") }) {
                                HStack { Label("Friendship", systemImage: "heart"); Spacer(); Text(friendship.value + " / 255").monospacedDigit() }.font(.caption).foregroundStyle(theme.accent)
                                ProgressView(value: Double(friendship.value) ?? 0, total: 255).tint(theme.accent).accessibilityHidden(true)
                            }
                        }.padding(17).frame(maxWidth: .infinity, alignment: .leading).background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 18)).overlay(RoundedRectangle(cornerRadius: 18).stroke(theme.accent.opacity(0.18)))
                    }.buttonStyle(.plain).accessibilityElement(children: .ignore).accessibilityLabel(row.name + ". " + row.detail).accessibilityHint("Edit memories and care").disabled(model.busy || model.fieldDrafts)
                }
            } else if let error { Text(error).foregroundStyle(.secondary); Button("Try Again") { Task { await load() } } }
            else { ProgressView() }
        }.task(id: String(model.state.revision) + model.state.entityData) { await load() }.sheet(item: $selection) { item in MemoryCardEditor(original: item.row, revision: item.revision, entityKey: item.entityKey) }
    }
    private func load() async { do { page = try await model.bridge.send(["op": "memoryInfo"], as: MemoryPage.self); error = nil } catch { page = nil; self.error = error.localizedDescription } }
}

private struct MemoryCardEditor: View {
    @EnvironmentObject var model: EditorModel
    @Environment(\.dismiss) private var dismiss
    let original: ExtraRow
    let revision: Int
    let entityKey: String
    @State private var row: ExtraRow?
    @State private var values: [String: String] = [:]
    @State private var previewing = false
    @State private var message = ""
    private var current: ExtraRow { row ?? original }
    private var changes: [[String: String]] { original.fields.compactMap { f in guard let v = values[f.id], v != f.value else { return nil }; return ["field": f.id, "value": v] } }
    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            HStack { Label(original.name, systemImage: original.id == "residences" ? "globe.americas.fill" : "heart.text.square.fill").font(.title2.bold()); Spacer(); Button("Cancel") { dismiss() }.keyboardShortcut(.cancelAction); Button("Save Changes") { save() }.buttonStyle(.borderedProminent).keyboardShortcut(.defaultAction).disabled(changes.isEmpty || previewing || model.busy || model.fieldDrafts) }
            Text(current.detail).font(.callout).fixedSize(horizontal: false, vertical: true).padding(16).frame(maxWidth: .infinity, alignment: .leading).background(.tint.opacity(0.08), in: RoundedRectangle(cornerRadius: 16))
            Form {
                ForEach(current.fields.filter { $0.kind != "readonly" && !($0.id.hasSuffix("MemoryVariable") && $0.choices.count == 1 && $0.choices.first?.value == "0") }) { field in
                    if field.kind == "enum" {
                        CatalogChoiceButton(title: field.label, options: choices(field), value: Binding(get: { values[field.id] ?? field.value }, set: { value in
                            values[field.id] = value
                            if field.id.hasSuffix("Memory") { for suffix in ["Variable", "Intensity", "Feeling"] { values.removeValue(forKey: field.id + suffix) } }
                            if field.id.hasSuffix("_Country") { values[field.id.replacingOccurrences(of: "_Country", with: "_Region")] = "0" }
                            preview()
                        }))
                    } else {
                        VStack(alignment: .leading, spacing: 5) { TextField(field.label, text: Binding(get: { values[field.id] ?? field.value }, set: { values[field.id] = $0 })).textFieldStyle(.roundedBorder); Text("\(field.min)–\(field.max)").font(.caption).foregroundStyle(.secondary) }
                    }
                }
            }.formStyle(.grouped).disabled(previewing || model.busy)
            HStack {
                if original.id == "residences" { Button("Clear Residence History") { for f in original.fields { values[f.id] = "0" }; preview() } }
                else if original.fields.contains(where: { $0.id.hasSuffix("Memory") && $0.kind != "readonly" }) { Button("Clear Memory") { for f in original.fields where f.id.contains("Memory") { values[f.id] = "0" }; preview() } }
                Spacer(); if previewing { ProgressView().controlSize(.small) }
                Text(message.isEmpty ? "Save, then Set to Slot to keep this in your game save." : message).font(.caption).foregroundStyle(.secondary)
            }.disabled(previewing)
        }.padding(24).frame(width: 720, height: original.id == "residences" ? 700 : 520).onAppear { values = Dictionary(uniqueKeysWithValues: original.fields.map { ($0.id, $0.value) }) }
    }
    private func choices(_ field: ExtraValue) -> [Choice] { let value = values[field.id] ?? field.value; return field.choices.contains { $0.value == value } ? field.choices : field.choices + [Choice(value: value, label: "Stored value · " + value)] }
    private func request(_ operation: String) -> [String: Any] { ["op": operation, "revision": revision, "entityKey": entityKey, "id": original.id, "edits": changes] }
    private func preview() {
        let payload = request("memoryInfo"); previewing = true; message = ""
        Task { defer { previewing = false }; do { let page = try await model.bridge.send(payload, as: MemoryPage.self); if let next = page.entries.first(where: { $0.id == original.id }) { row = next; values = Dictionary(uniqueKeysWithValues: next.fields.map { ($0.id, $0.value) }) } } catch { message = error.localizedDescription } }
    }
    private func save() { let payload = request("memorySet"); Task { await model.command(payload, status: "Memories & care updated — Set to Slot to keep them"); if model.error == nil { dismiss() } } }
}
