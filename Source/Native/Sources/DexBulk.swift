import SwiftUI

struct DexBulkOptions: Decodable { let revision: Int, actions: [Choice], shiny: Bool, allLanguages: Bool, entries: Bool }
struct DexBulkView: View {
    @EnvironmentObject var model: EditorModel
    @Environment(\.dismiss) private var dismiss
    @Environment(\.gameTheme) private var theme
    @State private var options: DexBulkOptions?
    @State private var action = "seen"
    @State private var shiny = false
    @State private var languages = false
    @State private var species = "0"
    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack { Image(systemName: "book.closed.fill").font(.largeTitle).foregroundStyle(theme.accent); VStack(alignment: .leading) { Text("Shape your Pokédex").font(.title2.bold()); Text("Forms, discoveries and languages").foregroundStyle(.secondary) }; Spacer() }
            if let options {
                Form {
                    Section("Collection") {
                        Picker("Action", selection: $action) { ForEach(options.actions) { Text($0.label).tag($0.value) } }
                        if options.entries && ["complete","unseen"].contains(action) {
                            CatalogChoiceButton(title: "Apply to", options: [Choice(value:"0",label:"Entire Pokédex")] + model.dex.map { Choice(value:String($0.id),label:$0.name) }, value:$species)
                        }
                    }
                    if options.shiny && !["unseen","uncaught","clearForms"].contains(action) || options.allLanguages && ["caught","complete"].contains(action) {
                        Section("Include") {
                            if options.shiny && !["unseen","uncaught","clearForms"].contains(action) { Toggle("Shiny appearances", isOn:$shiny) }
                            if options.allLanguages && ["caught","complete"].contains(action) { Toggle("Every supported language", isOn:$languages) }
                        }
                    }
                }.formStyle(.grouped)
                Label(explanation, systemImage: action.hasPrefix("un") || action=="clearForms" ? "eraser.fill" : "sparkles").font(.callout).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
                HStack { Text("Undo can restore this change.").font(.caption).foregroundStyle(.secondary); Spacer(); Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction); Button("Apply to \(species == "0" ? "Pokédex" : "Entry")") { apply(options) }.buttonStyle(.borderedProminent).keyboardShortcut(.defaultAction) }
            } else { ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity) }
        }.padding(26).frame(width:590,height:sheetHeight).disabled(model.busy)
            .onChange(of:action){_,next in if !["complete","unseen"].contains(next){species="0"}}
            .task { do { options = try await model.bridge.send(["op":"dexBulkOptions"],as:DexBulkOptions.self) } catch { model.error=error.localizedDescription; dismiss() } }
    }
    private var sheetHeight: CGFloat {
        guard let options else { return 300 }
        let scope = options.entries && ["complete","unseen"].contains(action)
        let include = (options.shiny && !["unseen","uncaught","clearForms"].contains(action)) || (options.allLanguages && ["caught","complete"].contains(action))
        return 300 + (scope ? 65 : 0) + (include ? 95 : 0)
    }
    private var explanation: String {
        switch action {
        case "complete": return "Uses this game’s PKHeX completion rules for seen, caught, forms and languages. Research tasks and size records remain separate."
        case "unseen": return "Clears seen records using this game’s Pokédex rules. Some formats also clear caught status."
        case "uncaught": return "Removes caught records while preserving discoveries where the game supports them."
        case "forms","firstForms","clearForms": return "Updates the form records using the same operation as the Windows editor."
        default: return "Updates the selected records for every supported species in the loaded game."
        }
    }
    private func apply(_ options: DexBulkOptions) { Task { await model.command(["op":"dexBulk","revision":options.revision,"action":action,"shiny":shiny,"allLanguages":languages,"species":Int(species) ?? 0],status:"Pokédex updated — Undo restores the previous records"); if model.error == nil { dismiss() } } }
}
