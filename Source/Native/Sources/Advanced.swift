import SwiftUI

struct AdvancedSaveView: View {
    @EnvironmentObject var model: EditorModel
    @State private var search = ""
    var body: some View {
        if !model.state.hasSave { ContentUnavailableView("Open a Save File",systemImage:"doc") }
        else {
            VStack(alignment:.leading,spacing:0) {
                VStack(alignment:.leading,spacing:10) {
                    Text("Advanced save editor").font(.title2.bold())
                    Text("Browse the structures exposed by this game, including configuration, trainer records, appearance, and supported raid data. These raw fields bypass the specialized Windows editors' guidance.").font(.callout).foregroundStyle(.secondary)
                    HStack {
                        Button { Task { await model.navigateObject("") } } label: { Image(systemName:"house") }.help("Save root")
                        Button { let parts = (model.saveObject?.path ?? "").split(separator:".").dropLast(); Task { await model.navigateObject(parts.joined(separator:".")) } } label: { Image(systemName:"chevron.left") }.disabled(model.saveObject?.path.isEmpty != false)
                        Text(model.saveObject?.path.isEmpty == false ? model.saveObject!.path : "Save overview").font(.callout.monospaced()).lineLimit(1).textSelection(.enabled)
                        Spacer()
                        Text(model.saveObject?.type ?? "").font(.caption).foregroundStyle(.tertiary)
                    }
                }.padding(24)
                Divider()
                if let data = model.saveObject {
                    HSplitView {
                        VStack(alignment:.leading,spacing:12) {
                            Text("STRUCTURES").font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                            TextField("Find a structure…",text:$search).textFieldStyle(.roundedBorder)
                            List(data.nodes.filter { search.isEmpty || $0.label.localizedCaseInsensitiveContains(search) || $0.type.localizedCaseInsensitiveContains(search) }) { node in
                                Button { search = ""; Task { await model.navigateObject(node.id) } } label: {
                                    VStack(alignment:.leading,spacing:4) { Text(node.label).font(.system(size:12,weight:.medium)); Text(node.type).font(.caption2).foregroundStyle(.secondary) }.frame(maxWidth:.infinity,alignment:.leading).padding(.vertical,4).contentShape(Rectangle())
                                }.buttonStyle(.plain)
                            }.listStyle(.plain)
                            if data.nodes.isEmpty { Text("No child structures").font(.caption).foregroundStyle(.tertiary) }
                        }.padding(16).frame(minWidth:210,idealWidth:230,maxWidth:290)
                        FieldList(fields:data.fields,target:"object").frame(minWidth:440)
                    }
                } else { ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity) }
            }
        }
    }
}
struct BatchEditorView: View {
    @EnvironmentObject var model: EditorModel
    @State private var text = "=Species=25\n.CurrentLevel=50"
    @State private var scope = "box"
    var body: some View {
        if !model.state.hasSave { ContentUnavailableView("Open a Save File",systemImage:"square.stack.3d.up") }
        else {
            VStack(alignment:.leading,spacing:16) {
                Text("Batch editor").font(.title2.bold())
                Text("Use PKHeX's batch syntax to filter and modify Pokémon. Preview the changes before applying them to the open save.").foregroundStyle(.secondary).font(.callout)
                Picker("Edit",selection:$scope) {
                    Text("Current box").tag("box"); Text("All boxes").tag("boxes"); Text("Party").tag("party")
                }.pickerStyle(.segmented).onChange(of:scope) { _, _ in model.batch = nil }
                TextEditor(text:$text).font(.system(size:13,design:.monospaced)).frame(minHeight:100,maxHeight:180).padding(8).background(.quaternary.opacity(0.3),in:RoundedRectangle(cornerRadius:8)).onChange(of:text) { _, _ in model.batch = nil }
                HStack {
                    Text("= filters • . edits • ; separates instruction sets").font(.caption).foregroundStyle(.secondary)
                    Spacer()
                    Button("Preview Changes") { Task { await model.previewBatch(text:text,scope:scope) } }.buttonStyle(.borderedProminent)
                }
                Divider()
                if let preview = model.batch {
                    HStack {
                        Text("\(preview.count) Pokémon would change").font(.headline)
                        Spacer()
                        Button("Apply Batch") { Task { await model.command(["op":"batchApply", "token":preview.token],status:"Batch applied — export a copy to keep the changes"); model.batch = nil } }.disabled(preview.count == 0 || !preview.errors.isEmpty)
                    }
                    ScrollView {
                        LazyVStack(alignment:.leading,spacing:14) {
                            ForEach(preview.errors,id:\.self) { Text($0).foregroundStyle(.red).font(.callout) }
                            ForEach(Array(preview.changes.enumerated()),id:\.offset) { _, change in
                                VStack(alignment:.leading,spacing:5) { Text("\(change.location) · \(change.name)").font(.callout.bold()); Text(change.detail).font(.caption.monospaced()).foregroundStyle(.secondary).textSelection(.enabled) }.frame(maxWidth:.infinity,alignment:.leading).padding(12).background(.quaternary.opacity(0.3),in:RoundedRectangle(cornerRadius:8))
                            }
                        }
                    }
                } else { ContentUnavailableView("Preview your edits",systemImage:"list.bullet.rectangle",description:Text("Empty slots are skipped. Batch changes stay in memory until you export the save.")) }
            }.padding(28).frame(maxWidth:960)
        }
    }
}
