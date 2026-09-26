import SwiftUI

struct FieldList: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    let fields: [Field], target: String
    var grouped = false
    @State private var search = ""
    @State private var showReadOnly = false
    init(fields:[Field],target:String,grouped:Bool=false,initialSearch:String="") {
        self.fields=fields; self.target=target; self.grouped=grouped
        _search=State(initialValue:initialSearch)
    }
    var visible: [Field] { fields.filter { (showReadOnly || $0.editable) && (search.isEmpty || $0.label.localizedCaseInsensitiveContains(search) || $0.id.localizedCaseInsensitiveContains(search)) } }
    var groups: [String] { Array(Set(visible.map(\.group))).sorted() }
    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Image(systemName:"magnifyingglass").foregroundStyle(.secondary)
                TextField("Find a field…", text:$search).textFieldStyle(.plain)
                Toggle("Read-only", isOn:$showReadOnly).toggleStyle(.checkbox).font(.caption).fixedSize()
            }.padding(10).background(.quaternary.opacity(0.4), in: RoundedRectangle(cornerRadius:9)).padding(.horizontal, 20).padding(.vertical, 12)
            ScrollView {
                LazyVStack(spacing: 0) {
                    if visible.isEmpty { Text("No matching fields").foregroundStyle(.secondary).padding(32) }
                    if grouped {
                        ForEach(groups, id:\.self) { group in
                            Text(group.uppercased()).font(.caption.weight(.semibold)).tracking(1).foregroundStyle(.secondary).frame(maxWidth:.infinity, alignment:.leading).padding(.top, 24).padding(.bottom, 10)
                            ForEach(visible.filter { $0.group == group }) { FieldRow(field:$0, target:target) }
                        }
                    } else { ForEach(visible) { FieldRow(field:$0, target:target) } }
                }.padding(.horizontal, 22).padding(.bottom, 24)
            }
            HStack {
                Text("\(visible.count) fields")
                Spacer()
                Text("Return or ✓ commits a typed field")
            }.font(.caption2).foregroundStyle(.tertiary).padding(.horizontal,22).padding(.vertical,9)
        }
    }
}
struct FieldRow: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    let field: Field, target: String
    @State private var choicePresentation: ChoicePresentation?
    var value: String { model.value(field, target:target) }
    var changed: Bool { model.drafts["\(target)|\(field.id)"] != nil }
    var body: some View {
        HStack(spacing: 12) {
            VStack(alignment:.leading, spacing:3) {
                Text(field.label).font(.system(size:12, weight:.medium)).foregroundStyle(field.editable ? .primary : .secondary)
                if let lookup = field.lookup, let name = model.catalogs[lookup]?.first(where: { $0.value == value })?.label { Text(name).font(.system(size:10)).foregroundStyle(.secondary).lineLimit(1) }
                if !field.help.isEmpty { Text(field.help).font(.system(size:11)).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true) }
            }.frame(maxWidth:.infinity, alignment:.leading)
            if field.id.contains("Gender") && field.editable {
                Picker(field.label,selection:Binding(get:{value},set:{v in Task{await model.commit(field,target:target,value:v)}})){Text("♂ Male").foregroundStyle(.blue).tag("0");Text("♀ Female").foregroundStyle(.pink).tag("1");if target=="entity"{Text("Genderless").tag("2")}}.labelsHidden().frame(width:230).foregroundStyle(value=="0" ? Color.blue : value=="1" ? Color.pink : Color.secondary)
            } else if !field.editable {
                Text(field.value.isEmpty ? "—" : field.value).font(.system(size:12, design:.monospaced)).foregroundStyle(.secondary).textSelection(.enabled).lineLimit(2).frame(maxWidth:220, alignment:.trailing)
            } else if field.kind == "bool" {
                Toggle(field.label, isOn:Binding(get:{ value == "true" }, set:{ v in Task { await model.commit(field, target:target, value:v ? "true" : "false") } })).labelsHidden().toggleStyle(.switch).controlSize(.mini)
            } else {
                HStack(spacing:5) {
                    TextField(field.kind == "datetime" ? "yyyy-mm-dd HH:mm:ss" : field.kind == "date" ? "yyyy-mm-dd" : field.label, text:Binding(get:{value}, set:{ model.draft($0, field:field, target:target) }))
                        .textFieldStyle(.roundedBorder).font(.system(size:12, design:field.kind == "number" ? .monospaced : .default))
                        .onSubmit { Task { await model.commit(field, target:target) } }
                    if field.lookup != nil || !field.choices.isEmpty {
                        Button { Task {
                            do {
                                let options: [Choice]
                                if let kind = field.lookup { options = try await model.fetchChoices(kind) }
                                else { options = field.choices }
                                choicePresentation = ChoicePresentation(title:field.label, options:options, selected:value)
                            } catch { model.error = error.localizedDescription }
                        } } label: { Image(systemName:"list.bullet").frame(width:16) }.help("Choose by name")
                    }
                    if changed { Button { Task { await model.commit(field, target:target) } } label: { Image(systemName:"checkmark").foregroundStyle(theme.accent) }.help("Commit this field") }
                }.frame(width:230)
            }
        }.padding(.vertical,9)
        .overlay(alignment:.bottom) { Divider().opacity(0.45) }
        .help(field.id + (field.help.isEmpty ? "" : "\n" + field.help))
        .sheet(item:$choicePresentation) { presentation in
            ChoiceSheet(title:presentation.title, options:presentation.options, selected:presentation.selected) { choice in
                choicePresentation = nil
                Task { await model.commit(field, target:target, value:choice.value) }
            }
        }
    }
}
// Present the options and the UI as one state change. Separate Boolean/array state can
// open a sheet with a stale empty array on its first presentation.
struct ChoicePresentation: Identifiable {
    let id = UUID()
    let title: String
    let options: [Choice]
    let selected: String
}
struct ChoiceSheet: View {
    @Environment(\.gameTheme) private var theme
    let title: String, options: [Choice], selected: String
    let choose: (Choice) -> Void
    @Environment(\.dismiss) var dismiss
    @State private var search = ""
    var filtered: [Choice] { options.filter { search.isEmpty || $0.label.localizedCaseInsensitiveContains(search) || $0.value.localizedCaseInsensitiveContains(search) } }
    var body: some View {
        VStack(alignment:.leading, spacing:14) {
            HStack { Text("Choose \(title)").font(.title2.bold()); Spacer(); Button("Cancel") { dismiss() }.keyboardShortcut(.cancelAction) }
            TextField("Search by name or ID", text:$search).textFieldStyle(.roundedBorder)
                .onSubmit { if filtered.count == 1 { choose(filtered[0]) } }
            ChoiceRows(options:filtered, selected:selected, showIDs:true, choose:choose)
                .overlay {
                    if filtered.isEmpty {
                        Text(options.isEmpty ? "No choices are available for this field." : "No matches. Try another name or ID.")
                            .foregroundStyle(.secondary).padding()
                    }
                }
            Text("\(filtered.count) choices").font(.caption).foregroundStyle(.secondary)
        }.padding(24).frame(width:490, height:540)
    }
}
struct ChoiceRows: View {
    @Environment(\.gameTheme) private var theme
    let options: [Choice]
    let selected: String
    var showIDs = false
    let choose: (Choice) -> Void
    var body: some View {
        ScrollView {
            LazyVStack(spacing:0) {
                ForEach(options) { choice in
                    Button { choose(choice) } label: {
                        HStack {
                            Text(choice.label).foregroundStyle(choice.label.hasPrefix("Female") ? Color.pink : choice.label.hasPrefix("Male") ? Color.blue : Color.primary)
                            Spacer()
                            if showIDs { Text(choice.value).font(.caption.monospaced()).foregroundStyle(.secondary) }
                            if choice.value == selected { Image(systemName:"checkmark").foregroundStyle(theme.accent) }
                        }.padding(.horizontal,10).padding(.vertical,8).frame(maxWidth:.infinity,alignment:.leading).contentShape(Rectangle())
                    }.buttonStyle(.plain).accessibilityLabel(choice.label)
                        .accessibilityValue(choice.value == selected ? "Selected" : "")
                    Divider().opacity(0.4)
                }
            }
        }
    }
}
struct SaveFieldsView: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    let advanced: Bool
    var body: some View {
        if !model.state.hasSave { ContentUnavailableView("Open a Save File", systemImage:"doc", description:Text("Trainer and save fields belong to a game save.")) }
        else {
            VStack(alignment:.leading, spacing:0) {
                VStack(alignment:.leading, spacing:8) {
                    Text(advanced ? "Advanced save fields" : "Trainer & progress").font(.title2.bold())
                    Text(advanced ? "Scalar fields exposed by this game's save format. Specialized game editors are listed under Port Coverage." : "Edit the trainer identity, play time, and currencies supported by this game.").foregroundStyle(.secondary).font(.callout)
                }.padding(24)
                FieldList(fields:model.state.saveFields.filter { advanced ? $0.group != "Trainer" : $0.group == "Trainer" }, target:"save")
            }.frame(maxWidth:800)
        }
    }
}
