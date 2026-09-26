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
                HStack { Text("Appearance"); Spacer(); Picker("Appearance",selection:$appearance) { ForEach(["System","Light","Dark"],id:\.self) { Text($0) } }.labelsHidden().pickerStyle(.segmented).frame(width:240) }
                Picker("Settings section",selection:$page) { Text("Themes").tag("Themes"); Text("Files & Startup").tag("Files"); Text("Engine").tag("Engine"); Text("Updates & Backup").tag("Services") }.pickerStyle(.segmented)
            }.padding(26)
            if page == "Themes" { ScrollView { ThemePicker().padding(26) } }
            else if page == "Files" { SaveResourcesView() }
            else if page == "Services" { ScrollView { VStack(spacing:20) { UpdateSettingsView(); PersonalBackupView() }.padding(26) } }
            else { FieldList(fields:model.fields,target:"settings",grouped:true) }
        }.frame(maxWidth:850)
    }
}
struct CoverageView: View {
    @Environment(\.gameTheme) private var theme
    private let ready = [
        "Hoenn/Kanto, Sinnoh/Johto and Unova adventure collections: Frontier, Pokétch art, Pokéwalker, Entrée Forest, Funfest, Subway and props",
        "Gen 2–5 and Stadium 2 mailbox: authors, stationery, phrases, portraits, party links, clear and reorder",
        "Game-aware Pokédex bulk actions with shiny/language options; older-game dex availability and Details navigation fixed",
        "Battle Revolution wardrobe and BDSP encounter, destination, fashion and trainer unlocks",
        "Finder file drops with unsaved-change protection, party-to-journal team capture and refined badge details",
        "Pokémon Link rewards, Unova Global Link records, downloaded content and C-Gear PNG customization",
        "Gen 4 battle-video files and Battle Revolution passes, appearance, messages and teams",
        "Auto-Legality encounter generation with change/report preview, guarded apply and Undo",
        "Journal team Pokémon snapshots, game-format conversion/generation, file export and empty-box-slot placement",
        "Personal welcome name, journal emblem, custom cover colors and shared named color presets",
        "Trainer party and mapped gym badges; PID in Stats; separate Scarlet/Violet themes",
        "Game Extras: badges, Hall of Fame, Join Avenue, raids, contacts, Gen 4/8 Underground, Chatter and collections",
        "Catch counts, Ball Stickers and Z-A donuts with artwork, flavor charts and individual files",
        "Personal Pokémon pages, six-companion teams, memories, cover customization and complete JSON backups",
        "Visual wardrobe catalog, individual ownership and category Give All; named Hisui, Galar, Paldea and Sinnoh entries",
        "Species/base-stat guide, typed event variables, Switch block comparison and reviewed folder/ZIP imports",
        "Pokéblocks, Poffins, Poké Puffs and Poké Beans in game-specific treat cases",
        "Customizable trainer card, item and ball icons, nickname-first names and gender colors",
        "Rich Pokémon hover cards, optional click/drag animations, game-aware sidebar and Fashion actions",
        "Pokédex card grid, SV/BDSP/SWSH/ZA basic flags, Give All and Undo",
        "Tera controls and characteristic in Stats, random IVs and EVs",
        "Native SwiftUI window, native file panels, 15 game-inspired themes, custom colors, automatic game palettes, light/dark appearance",
        "Move-shop purchases and mastery, TM/TR records, Arceus research counters and reporting",
        "Storage search, folder Pokémon library, box export, CSV reports, staged bulk box actions",
        "Encounter search by species/origin/type/move, result preparation, and legality review",
        "Z-A move-plus records, upgrade suggestions, box unlock counts and advanced flags",
        "Pixel sprites, HOME portraits or game portraits; Scarlet/Violet and Arceus art with exact-form and shiny fallbacks",
        "Current/relearn suggestions, alternate sets, move type icons, learnability filters and legality explanations",
        "Gen 4–9 Pokédex form/language/display records, DexNav counters, size records and unlocks",
        "Game identity artwork across all sample games and supported side games; separate sequel logos",
        "Arceus form variants, Pokédex appearance, and recorded size ranges",
        "Hoenn Secret Bases, ORAS Super-Secret Bases and files, HGSS Pokéathlon, Festival Plaza and Battle Agency",
        "Ribbon artwork, count controls, equipped titles, legality hints, and shape markings",
        "Super Training medals plus Gen 6 save records, stage unlocks and training bags",
        "Mystery Gift albums: card import/export, received history, deletion and status",
        "Read-only gift expressions and filtered bulk export; bundled and session-folder Mystery Gift libraries; language, origin, generation, species, moves, item, shiny and egg filters; card export and restricted-language preparation",
        "PKHeX.Core 26.08.26: save recognition, binary data, checksums, legality",
        "Box and party selection; Pokémon import, conversion, edit, apply, clear, export",
        "Box-slot move/swap, right-click View and Set to Slot, box names, game/Mac wallpapers, box order and undo",
        "Main, stats, moves, met, trainer, ribbons, and searchable scalar Pokémon fields",
        "Trainer identity, currencies, play time, and exposed scalar save properties",
        "Searchable Add Items, stack top-up, quantities, extra flags; basic Pokédex seen/caught flags",
        "Navigable save structures, Scarlet/Violet raid properties, named event flags and variables",
        "Read-only event comparison between saves or the workspace, with copy/export reports",
        "Z-A keyed event editing across all 15 collections, key-name files and change reports",
        "Pokémon and Hall of Fame name bytes, encoding, special characters, hidden-byte clearing and species-name layers",
        "Multiple remembered library folders; storage property filters, result navigation and configurable CSV reports",
        "Reviewed Showdown team import, four-move/form/advanced encounter search and current-editor criteria",
        "Pokédex group shortcuts, custom Z-A donut ranges/flavor pools and guarded file-to-box-slot import",
        "Batch editing with a changes preview, protected-slot handling, and undo",
        "Full legality report, Showdown set import/export, shiny/IV/EV/heal actions",
        "25-step in-memory undo/redo; export copies; unsaved-change prompts",
        "Persistent legality, conversion, import, slot-write, and save-language settings",
        "Independent encounter generation preferences and remembered/imported original-trainer profiles",
        "Folder batch previews and atomic export of edited copies, with source-change checks",
        "Pokémon QR image export/import, including binary Gen 7 payloads",
        "Apple Silicon and Intel macOS builds; Intel engine protocol checks under Rosetta",
        "Party reorder and box/party transfers, guarded party composition and byte-exact Game Boy rollback",
        "Original-save snapshots, backup export, bounded folder save discovery and optional last-document reopening",
        "Game-data language choices and localized community/Showdown template export",
        "Game-specific trainer details, full timestamp editing, saved SV/Z-A photos and PNG export",
        "Hisui research task panels with reported/unreported milestones and game-derived tasks",
        "Mystery Gift QR import/export, staged album card drops, guided search rules and Pokémon summary images",
        "Trainer name bytes, named trainer records, linked league-card controls and Poffin cooking records",
        "Persistent box-export naming, encounter result limits and report column presets",
        "League-card/title-screen teams, Battle Maison/Tree, named Alola destinations and game-specific trainer unlocks",
        "Stadium registered teams, Gen 4–7 adventure dates, facing directions, trainer progress and compatible edition changes",
        "Batch source/box/slot filters, medal multi-selection, donut clipboard/drop and Hall of Fame summaries",
        "Explicit save-format opening, rival/Secret Base name bytes, nested model collections and configurable report previews"
    ]
    private let waiting = [
        "Cross-window and Finder drag gesture verification",
        "Distribution signing and notarization"
    ]
    var body: some View {
        ScrollView {
            VStack(alignment:.leading,spacing:18) {
                Pill(text:"NATIVE · MAC",color:theme.accent)
                Text("Made for your adventures").font(.system(size:28,weight:.bold,design:.rounded))
                Text("Native editing powered by PKHeX’s core, with a personal journal and a Mac-first interface. Available tools depend on your game and file format.").font(.body).foregroundStyle(.secondary).lineSpacing(4)
                Text("Implemented").font(.headline).padding(.top,8)
                ForEach(ready,id:\.self) { Label($0,systemImage:"checkmark.circle.fill").foregroundStyle(.primary).font(.callout) }
                Text("Remaining verification & release work").font(.headline).padding(.top,12)
                ForEach(waiting,id:\.self) { Label($0,systemImage:"circle.dashed").foregroundStyle(.secondary).font(.callout) }
                Text("RC scope: offline editing with Auto-Legality and an English interface. Ten-language game-data catalogs are included. Other Windows plugins, LiveHeX and interface translations are outside this RC.").font(.callout).foregroundStyle(.secondary).padding(.top,12)
                Divider().padding(.vertical,12)
                Text("PKHeX is by Kaphotics and contributors. This unofficial SwiftUI port is distributed with its source under GPL-3.0-or-later. Pokémon and related names belong to their respective owners.").font(.caption).foregroundStyle(.tertiary)
                Link("Upstream PKHeX source",destination:URL(string:"https://github.com/kwsch/PKHeX")!).font(.caption)
            }.padding(36).frame(maxWidth:850,alignment:.leading)
        }
    }
}
