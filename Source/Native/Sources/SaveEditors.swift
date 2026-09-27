import SwiftUI

struct InventoryView: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    @State private var pouch = 0
    @State private var search = ""
    @State private var showEmpty = false
    @State private var adding: Pouch?
    var selected: Pouch? { model.inventory.first { $0.id == pouch } }
    var body: some View {
        if !model.state.canInventory { ContentUnavailableView("Inventory Unavailable",systemImage:"backpack",description:Text("Open a save with a supported inventory to edit item quantities.")) }
        else {
            VStack(alignment:.leading,spacing:18) {
                HStack { Text("Inventory").font(.title2.bold()); Spacer(); Button {if let selected {Task {await model.command(["op":"inventoryGiveAll","pouch":selected.id],status:"Pouch filled — Undo restores it; Export Copy saves it")}}} label:{Label("Give All",systemImage:"gift.fill")}.disabled(selected==nil || model.fieldDrafts); Button { adding = selected } label: { Label("Add Items…",systemImage:"plus") }.buttonStyle(.borderedProminent).disabled(selected == nil || model.busy || model.fieldDrafts) }
                HStack {
                    Picker("Pouch",selection:$pouch) { ForEach(model.inventory) { Text($0.name).tag($0.id) } }.frame(width:270)
                    Spacer()
                    Toggle("Show zero quantities",isOn:$showEmpty).toggleStyle(.checkbox)
                }
                TextField("Find an item…",text:$search).textFieldStyle(.roundedBorder)
                if let selected {
                    HStack { Text("ITEM"); Spacer(); Text("QUANTITY / MAX \(selected.max)") }.font(.caption.weight(.semibold)).foregroundStyle(.secondary)
                    ScrollView {
                        LazyVStack(spacing:0) {
                            ForEach(selected.items.filter { (showEmpty || $0.count > 0) && (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search)) }) { item in
                                InventoryRow(item:item,pouch:selected).id("\(selected.id)-\(item.id)-\(item.item)-\(item.count)")
                            }
                        }
                    }
                    Text("Changes are stored in the open save. Export a copy when finished.").font(.caption).foregroundStyle(.secondary)
                }
            }.padding(28).frame(maxWidth:850).sheet(item:$adding) { AddInventoryView(pouch:$0) }
        }
    }
}
struct InventoryRow: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    let item: InventoryItem, pouch: Pouch
    @State private var choose = false
    @State private var details = false
    var quantityKey: String { "quantity:\(pouch.id):\(item.id)" }
    var count: String { model.drafts[quantityKey] ?? String(item.count) }
    var body: some View {
        HStack(spacing:12) {
            GameAsset(folder:"Items",name:item.icon).frame(width:32,height:32)
            Text(String(format:"%03d",item.id+1)).font(.caption.monospaced()).foregroundStyle(.tertiary).frame(width:35)
            Button { choose = true } label: { HStack { Text(item.item == 0 ? "Empty slot" : item.name); if !pouch.fixedItems { Image(systemName:"chevron.down").font(.caption2) } } }.buttonStyle(.plain).disabled(pouch.fixedItems)
            Spacer()
            TextField("Count",text:Binding(get:{count},set:{ v in if v == String(item.count) { model.drafts.removeValue(forKey:quantityKey) } else { model.drafts[quantityKey] = v } })).textFieldStyle(.roundedBorder).frame(width:80).onSubmit { commit(item.item) }
            Button("Give Max") { Task { await model.updateInventory(pouch:pouch.id,slot:item.id,item:item.item,count:item.count,giveMax:true) } }.disabled(item.item == 0 || model.busy || model.fieldDrafts).help("Give the maximum quantity allowed for this item in this game")
            Button("Apply") { commit(item.item) }.disabled(Int(count) == nil || Int(count) == item.count)
            Button { details = true } label: { Image(systemName:"ellipsis.circle") }.help("Additional item flags")
        }.padding(.vertical,9).overlay(alignment:.bottom) { Divider().opacity(0.4) }
        .sheet(isPresented:$details) {
            VStack(alignment:.leading,spacing:12) {
                HStack { Text(item.name).font(.title2.bold()); Spacer(); Button("Done") { details = false } }
                if model.inventoryExtra.isEmpty { Text("No extra item fields are exposed for this format.").foregroundStyle(.secondary).padding() }
                else { FieldList(fields:model.inventoryExtra,target:"inventoryField") }
            }.padding(24).frame(width:600,height:430)
            .task { await model.inventoryDetails(pouch:pouch.id,slot:item.id) }
        }
        .sheet(isPresented:$choose) {
            ChoiceSheet(title:"Item",options:pouch.choices,selected:String(item.item)) { selected in
                choose = false
                guard let id = Int(selected.value) else { return }
                Task { await model.updateInventory(pouch:pouch.id,slot:item.id,item:id,count:id == 0 ? 0 : max(1,Int(count) ?? 1)) }
            }
        }
    }
    func commit(_ id: Int) { guard let n = Int(count) else { return }; Task { await model.updateInventory(pouch:pouch.id,slot:item.id,item:id,count:n) } }
}
struct DexRecordSelection:Identifiable {let id:Int}
struct DexView: View {
    @State private var showingActions=false
    @State private var detailsSpecies:DexRecordSelection?
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    @State private var search=""
    @State private var filter="All"
    private var entries:[DexEntry] {model.dex.filter{(search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || String($0.id)==search) && (filter=="All" || filter=="Caught" && $0.caught || filter=="Missing" && !$0.caught || filter=="Seen" && $0.seen)}}
    var body:some View {
        if !model.state.canDex {ContentUnavailableView("Pokédex",systemImage:"book.closed",description:Text("Open a save with a supported Pokédex."))}
        else {VStack(alignment:.leading,spacing:18) {
            HStack {
                VStack(alignment:.leading,spacing:5){Label("Pokédex",systemImage:"book.closed.fill").font(.largeTitle.bold());Text("Every encounter has a place in your journal.").foregroundStyle(.secondary)}
                Spacer()
                Button {showingActions=true} label:{Label("Pokédex Actions…",systemImage:"gift.fill")}.disabled(model.fieldDrafts)
            }
            HStack(spacing:12){dexCount("Caught",model.dex.filter(\.caught).count,"checkmark.seal.fill",.green);dexCount("Seen",model.dex.filter(\.seen).count,"eye.fill",theme.accent);dexCount("To discover",model.dex.filter{!$0.seen}.count,"sparkle",.orange)}
            HStack {TextField("Find a Pokémon or National Dex number…",text:$search).textFieldStyle(.roundedBorder);Picker("Show",selection:$filter){ForEach(["All","Seen","Caught","Missing"],id:\.self){Text($0)}}.frame(width:170)}
            ScrollView {LazyVGrid(columns:[GridItem(.adaptive(minimum:225),spacing:14)],spacing:14) {
                ForEach(entries){entry in
                    VStack(alignment:.leading,spacing:14) {
                        HStack {PokemonSprite(name:"b_\(entry.id)").frame(width:52,height:48).opacity(entry.seen ? 1 : 0.38);VStack(alignment:.leading,spacing:4){Text(entry.name).font(.headline);Text(String(format:"No. %04d",entry.id)).font(.caption.monospaced()).foregroundStyle(.secondary)};Spacer();if entry.caught{Image(systemName:"checkmark.seal.fill").foregroundStyle(.green)}}
                        HStack {Toggle("Seen",isOn:Binding(get:{entry.seen},set:{v in Task{await model.updateDex(entry,seen:v,caught:v && entry.caught)}}));Toggle("Caught",isOn:Binding(get:{entry.caught},set:{v in Task{await model.updateDex(entry,seen:entry.seen || v,caught:v)}}))}.toggleStyle(.checkbox).font(.caption).disabled(model.fieldDrafts)
                        if entry.details {Button("Forms & Details…"){if model.state.canResearch{model.researchSpecies=entry.id;model.dexDetails=nil};detailsSpecies=DexRecordSelection(id:entry.id)}.buttonStyle(.borderless).font(.caption)}
                    }.padding(16).background(theme.accent.opacity(entry.caught ? 0.1 : 0.035),in:RoundedRectangle(cornerRadius:16)).overlay(RoundedRectangle(cornerRadius:16).stroke(theme.accent.opacity(0.14)))
                }
            }.padding(2)}
            Text("\(entries.count) entries · Form, language and appearance records are under Details. Bulk actions include game-specific forms and languages; research task counts stay unchanged.").font(.caption).foregroundStyle(.secondary)
        }.padding(24).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.topLeading).sheet(isPresented:$showingActions){DexBulkView()}.sheet(item:$detailsSpecies){entry in if model.state.canResearch{DexDetailsView()}else{DexRecordView(species:entry.id)}}}
    }
    private func dexCount(_ title:String,_ count:Int,_ icon:String,_ color:Color)->some View {HStack{Image(systemName:icon).font(.title2);VStack(alignment:.leading){Text(String(count)).font(.title2.bold());Text(title).font(.caption)}}.foregroundStyle(color).padding(16).frame(maxWidth:.infinity,alignment:.leading).background(color.opacity(0.08),in:RoundedRectangle(cornerRadius:14))}
}
struct PreferencesView: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    @AppStorage("appearance") private var appearance = "System"
    @State private var page="Themes"
    var body: some View {
        VStack(alignment:.leading,spacing:0) {
            VStack(alignment:.leading,spacing:14) {
                Text("Settings").font(.title2.bold())
                InterfaceLanguageView()
                HStack { Text("Appearance"); Spacer(); Picker("Appearance",selection:$appearance) { ForEach(["System","Light","Dark"],id:\.self) { Text($0) } }.labelsHidden().pickerStyle(.segmented).frame(width:240) }
                Picker("Settings section",selection:$page) { Text("Themes").tag("Themes"); Text("Files & Startup").tag("Files"); Text("Engine").tag("Engine"); Text("Updates & Backup").tag("Services") }.pickerStyle(.segmented)
            }.padding(26)
            if page == "Themes" { ScrollView { ThemePicker().padding(26) } }
            else if page == "Files" { SaveResourcesView() }
            else if page == "Services" { ScrollView { VStack(spacing:20) { UpdateSettingsView(); PersonalBackupView() }.padding(26) } }
            else { VStack(alignment:.leading,spacing:12){
                if let field=model.fields.first(where:{$0.id=="PKHaXMode"}) {
                    VStack(alignment:.leading,spacing:10){Toggle(isOn:Binding(get:{field.value=="true"},set:{enabled in Task{await model.commit(field,target:"settings",value:enabled ? "true":"false")}})){Label("PKHaX mode",systemImage:"lock.open.fill").font(.headline)}.disabled(model.busy || model.fieldDrafts)
                        Text(field.help).font(.caption).foregroundStyle(.secondary)
                        Text(field.value=="true" ? "Unrestricted editing · automatic checks off":"Standard editing · automatic checks on").font(.caption.bold()).foregroundStyle(field.value=="true" ? Color.orange:theme.accent)
                    }.padding(18).background(theme.accent.opacity(0.07),in:RoundedRectangle(cornerRadius:16)).padding(.horizontal,26)
                }
                FieldList(fields:model.fields.filter{$0.id != "PKHaXMode"},target:"settings",grouped:true)
            }}
        }.frame(maxWidth:850)
    }
}
