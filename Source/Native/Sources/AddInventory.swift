import SwiftUI

struct AddInventoryView: View {
    @EnvironmentObject var model: EditorModel
    @Environment(\.dismiss) private var dismiss
    let pouch: Pouch
    @State private var search = ""
    @State private var item: String = ""
    @State private var quantity = "1"
    var selected: Choice? { pouch.choices.first { $0.value == item } }
    var owned: Int { pouch.items.first { String($0.item) == item }?.count ?? 0 }
    var maximum: Int { pouch.limits[item] ?? pouch.max }
    var amount: Int { Int(quantity) ?? 0 }
    var valid: Bool { selected != nil && amount > 0 && amount <= maximum - owned }
    var choices: [Choice] { pouch.choices.filter { $0.value != "0" && (search.isEmpty || $0.label.localizedCaseInsensitiveContains(search) || $0.value == search) } }
    var body: some View {
        VStack(alignment:.leading,spacing:16) {
            Text("Add Items").font(.title2.bold())
            Text("\(pouch.name) · Choose an item and how many to add.").foregroundStyle(.secondary)
            TextField("Search items…",text:$search).textFieldStyle(.roundedBorder)
            ScrollView {
                LazyVStack(spacing:2) {
                    ForEach(choices) { choice in
                        Button { item = choice.value; quantity = "1" } label: {
                            HStack { Text(choice.label); Spacer(); if item == choice.value { Image(systemName:"checkmark.circle.fill") } }
                                .padding(10).frame(maxWidth:.infinity,alignment:.leading)
                                .background(item == choice.value ? Color.accentColor.opacity(0.12) : .clear,in:RoundedRectangle(cornerRadius:8))
                                .contentShape(Rectangle())
                        }.buttonStyle(.plain).accessibilityLabel(choice.label)
                    }
                    if choices.isEmpty { Text("No matching items").foregroundStyle(.secondary).padding() }
                }
            }.frame(height:280)
            Divider()
            if let selected {
                Text(selected.label).font(.headline)
                HStack { Text("Quantity to add"); TextField("Quantity to add",text:$quantity).textFieldStyle(.roundedBorder).frame(width:90); Spacer(); Text("Owned: \(owned) · Maximum: \(maximum)").font(.caption).foregroundStyle(.secondary) }
                Text(valid ? "You’ll have \(owned + amount). Existing stacks are topped up automatically." : "Enter 1–\(max(0,maximum-owned)) more. This item cannot exceed \(maximum).")
                    .font(.caption).foregroundStyle(valid ? Color.secondary : Color.orange)
            } else { Text("Select an item above.").foregroundStyle(.secondary).frame(height:75) }
            HStack { Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction); Spacer(); Button("Add Items") { Task { if await model.addInventory(pouch:pouch.id,item:Int(item) ?? 0,count:amount) { dismiss() } } }.buttonStyle(.borderedProminent).disabled(!valid).keyboardShortcut(.defaultAction) }
        }.padding(24).frame(width:560).disabled(model.busy).interactiveDismissDisabled(model.busy)
    }
}
