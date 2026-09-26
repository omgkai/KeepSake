import SwiftUI
import AppKit

struct GiftFolderResult:Codable {let added:Int,skipped:Int,duplicates:Int,truncated:Bool,entries:[GiftEntry]}
struct GiftDatabaseView:View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    @State private var filters=GiftFilters()
    @State private var advanced=false
    @State private var showGiftQR=false
    @State private var selection:Int?
    @State private var moveCatalog:[Choice]=[]
    @State private var folderMessage=""
    @State private var instructions=""
    @State private var buildRule=false
    @State private var expressionEditor=false
    @State private var matched:Set<Int>?
    @State private var appliedInstructions=""
    private var filtered:[GiftEntry]{model.gifts.filter{filters.matches($0) && (matched == nil || matched!.contains($0.id))}}
    private var chosen:GiftEntry?{filtered.first{$0.id==selection}}
    private var origins:[String]{Array(Set(model.gifts.map(\.origin))).sorted()}
    private var items:[Choice]{[Choice(value:"",label:"Any held item")]+Array(Set(model.gifts.filter(\.entity).map(\.heldItem))).sorted().map{Choice(value:$0,label:$0)}}
    private var species:[Choice]{[Choice(value:"",label:"Any species")]+Dictionary(grouping:model.gifts.filter(\.entity),by:\.species).sorted{$0.key<$1.key}.map{Choice(value:String($0.key),label:$0.value[0].name)}}
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack(spacing:18) {
                Image(systemName:"gift.fill").font(.system(size:32)).foregroundStyle(theme.accent).frame(width:68,height:68).background(theme.accent.opacity(0.12),in:RoundedRectangle(cornerRadius:20))
                VStack(alignment:.leading,spacing:6){Text("A little mystery. A special memory.").font(.system(size:26,weight:.semibold,design:.rounded));Text("Discover event Pokémon and deliveries from every adventure.").font(.callout).foregroundStyle(.secondary)}
                Spacer()
                Menu {Button("Mystery Gift QR…"){showGiftQR=true};Button("Add Gift Folder…"){loadFolder()};Button("Export Filtered Cards…"){exportFiltered()}.disabled(filtered.filter(\.exportable).isEmpty);Divider();Button("Clear Folder Cards"){clearFolders()}.disabled(!model.gifts.contains{$0.source=="Folder"})} label:{Label("Library",systemImage:"folder.badge.plus")}.disabled(model.busy)
            }.padding(20).background(theme.accent.opacity(0.06),in:RoundedRectangle(cornerRadius:22))
            filterPanel
            if !folderMessage.isEmpty {Text(folderMessage).font(.caption).foregroundStyle(.secondary)}
            if model.busy && model.gifts.isEmpty {ProgressView("Opening the gift library…").frame(maxWidth:.infinity,maxHeight:.infinity)}
            else if filtered.isEmpty {WorkspaceEmptyState(title:model.gifts.isEmpty ? "The library is waiting":"No gifts match these filters",icon:"gift",message:model.gifts.isEmpty ? "Load the bundled event collection to begin.":"Try another language, game or event detail."){Button(model.gifts.isEmpty ? "Load Gifts":"Clear Filters"){if model.gifts.isEmpty{Task{await model.loadGifts()}}else{filters=GiftFilters()}}}}
            else {ScrollView{LazyVGrid(columns:[GridItem(.adaptive(minimum:270,maximum:420))],spacing:16){ForEach(filtered){gift in Button{selection=gift.id}label:{GiftLibraryCard(gift:gift,selected:selection==gift.id)}.buttonStyle(.plain).accessibilityLabel("\(gift.name), \(gift.title), Card \(gift.card), \(gift.languageText)").accessibilityAddTraits(selection==gift.id ? .isSelected:[])}}.padding(2)}}
            selectionPanel
        }.padding(24).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.topLeading)
            .task {if model.gifts.isEmpty{await model.loadGifts()};do{moveCatalog=[Choice(value:"",label:"Any move")]+(try await model.bridge.send(["op":"lookup","kind":"giftMoves"],as:[Choice].self)).filter{$0.value != "0"}}catch{model.error=error.localizedDescription}}
            .onChange(of:filters){_,_ in selection=nil}
            .sheet(isPresented:$showGiftQR) { PokemonQRView(giftMode:true,giftID:chosen?.exportable == true ? chosen?.id : nil) }
            .sheet(isPresented:$expressionEditor) {
                VStack(alignment:.leading,spacing:16) {
                    HStack {Label("Advanced gift filters",systemImage:"line.3.horizontal.decrease.circle").font(.title2.bold());Spacer();Button("Cancel"){expressionEditor=false}}
                    Text("Read-only PKHeX expressions, one per line. Example: =Species=25 or =CardID=100. Missing properties do not match. Editing instructions are rejected.").font(.callout).foregroundStyle(.secondary)
                    TextEditor(text:$instructions).font(.body.monospaced()).border(.quaternary)
                    HStack {Button("Add Rule…"){buildRule=true}.sheet(isPresented:$buildRule){SearchRuleBuilder(instructions:$instructions,separator:"\n",gift:true)};Button("Clear"){instructions=""};Spacer();Button("Apply Expressions"){applyExpressions()}.buttonStyle(.borderedProminent).disabled(model.busy)}
                }.padding(24).frame(width:610,height:360)
            }
    }
    private var filterPanel:some View {
        VStack(alignment:.leading,spacing:12) {
            HStack {Image(systemName:"magnifyingglass").foregroundStyle(.secondary);TextField("Pokémon, event, card ID, trainer or filename…",text:$filters.search).textFieldStyle(.roundedBorder);Button {advanced.toggle()}label:{Label("More filters",systemImage:"line.3.horizontal.decrease.circle")};Button("Reset"){filters=GiftFilters();instructions="";appliedInstructions="";matched=nil}.disabled(!filters.active && matched==nil)}
            HStack(spacing:16) {
                Picker("Language",selection:$filters.language){Text("Any language").tag(0);ForEach(GiftEntry.languageNames.keys.sorted(),id:\.self){id in Text(GiftEntry.languageNames[id]!).tag(id)};Divider();Text("Multiple languages").tag(-2);Text("Unrecorded").tag(-1)}
                Picker("Origin",selection:$filters.origin){Text("Any origin game").tag("");ForEach(origins,id:\.self){Text($0).tag($0)}}
                Picker("Gift",selection:$filters.kind){Text("All gifts").tag("");Text("Pokémon").tag("pokemon");Text("Items").tag("items")}
            }.font(.callout)
            if advanced {
                Divider()
                HStack(spacing:12) {
                    Picker("Generation",selection:$filters.generation){Text("Any").tag(0);ForEach(1...9,id:\.self){Text("Gen \($0)").tag($0)}}
                    Picker("Range",selection:$filters.comparison){Text("Exactly").tag("exact");Text("Or earlier").tag("before");Text("Or later").tag("after")}.disabled(filters.generation==0)
                    Picker("Shiny",selection:$filters.shiny){Text("Any rule").tag("");Text("Always shiny").tag("Always");Text("Never shiny").tag("Never");Text("Can vary").tag("Random")}
                    Picker("Egg",selection:$filters.egg){Text("Either").tag("");Text("Egg gifts").tag("egg");Text("Not eggs").tag("notEgg")}
                }.font(.caption)
                HStack {CatalogChoiceButton(title:"Species",options:species,value:$filters.species);CatalogChoiceButton(title:"Held item",options:items,value:$filters.heldItem);Picker("Source",selection:$filters.source){Text("All sources").tag("");Text("Bundled").tag("Bundled");Text("Folders").tag("Folder")}.frame(width:190)}
                LazyVGrid(columns:[GridItem(.flexible()),GridItem(.flexible())],spacing:8){ForEach(0..<4,id:\.self){i in CatalogChoiceButton(title:"Move \(i+1)",options:moveCatalog,value:$filters.moves[i])}}
            }
            if advanced {HStack {Button("Advanced Windows Filters…"){expressionEditor=true};if matched != nil {Text("Property expressions applied").font(.caption).foregroundStyle(.secondary)}}}
            Text("Language follows the Pokémon’s card rules, including multilingual gifts. It does not identify the distribution country or region. Origin game is not a guarantee of save compatibility.").font(.caption2).foregroundStyle(.secondary)
        }.padding(16).background(.quaternary.opacity(0.22),in:RoundedRectangle(cornerRadius:16))
    }
    private var selectionPanel:some View {
        VStack(alignment:.leading,spacing:10) {
            if let gift=chosen {
                HStack {Image(systemName:"giftcard.fill").foregroundStyle(theme.accent);VStack(alignment:.leading,spacing:4){Text(gift.title).font(.headline);Text("Card \(gift.card) · \(gift.origin) · \(gift.languageText)").font(.caption).foregroundStyle(.secondary).help(gift.languageDetails)};Spacer();if !gift.trainer.isEmpty{Text("OT · "+gift.trainer).font(.caption)}}
            }
            HStack {Text("\(filtered.count.formatted()) of \(model.gifts.count.formatted()) gifts").font(.caption).foregroundStyle(.secondary);Spacer();Button("Add to Album…"){if let chosen{model.pendingAlbumGift=chosen;model.section="Gift Album"}}.disabled(chosen?.exportable != true || !model.state.canGiftAlbum || model.busy);Button("Export Card…"){if let chosen{model.exportGift(chosen)}}.disabled(chosen?.exportable != true || model.busy);Button("Prepare Pokémon"){if let chosen{model.prepareGift(chosen)}}.buttonStyle(.borderedProminent).disabled(chosen?.entity != true || !model.state.hasSave || model.busy)}
            Text("Prepare with your save’s trainer, review legality, then Set to Slot. Folder libraries remain available for this session; original files stay unchanged.").font(.caption).foregroundStyle(.secondary)
        }.padding(16).background(.quaternary.opacity(0.22),in:RoundedRectangle(cornerRadius:16))
    }
    private func applyExpressions() {
        Task {guard !model.busy else{return};model.busy=true;defer{model.busy=false};do {let ids=try await model.bridge.send(["op":"giftsFilter","filters":instructions],as:[Int].self);matched=Set(ids);appliedInstructions=instructions;selection=nil;expressionEditor=false}catch{model.error=error.localizedDescription}}
    }
    private func exportFiltered() {
        let ids=filtered.filter(\.exportable).map(\.id)
        let panel=NSOpenPanel();panel.canChooseFiles=false;panel.canChooseDirectories=true;panel.canCreateDirectories=true;panel.message="Export \(ids.count) filtered cards into a new folder. Existing files remain unchanged."
        guard panel.runModal() == .OK,let url=panel.url else{return}
        Task {guard !model.busy else{return};model.busy=true;defer{model.busy=false};do {let result=try await model.bridge.send(["op":"giftsExportSelection","ids":ids,"path":url.path],as:ExportResult.self);folderMessage="Exported \(result.count) cards to \(result.path)"}catch{model.error=error.localizedDescription}}
    }
    private func loadFolder() {
        let panel=NSOpenPanel();panel.title="Add Mystery Gift Folder";panel.canChooseFiles=false;panel.canChooseDirectories=true
        guard panel.runModal() == .OK,let url=panel.url else{return}
        Task {guard !model.busy else{return};model.busy=true;defer{model.busy=false};do {let result=try await model.bridge.send(["op":"giftsLoadFolder","path":url.path,"recursive":true],as:GiftFolderResult.self);model.gifts=result.entries;selection=nil;matched=nil;appliedInstructions="";folderMessage="\(result.added) cards added · \(result.duplicates) duplicates · \(result.skipped) unreadable"+(result.truncated ? " · Folder limit reached":"")}catch{model.error=error.localizedDescription}}
    }
    private func clearFolders() {Task{guard !model.busy else{return};model.busy=true;defer{model.busy=false};do{model.gifts=try await model.bridge.send(["op":"giftsClearFolders"],as:[GiftEntry].self);selection=nil;matched=nil;appliedInstructions="";model.pendingAlbumGift=nil;folderMessage="Folder cards removed from this session. Your files are unchanged."}catch{model.error=error.localizedDescription}}}
}
struct GiftLibraryCard:View {
    @Environment(\.gameTheme) private var theme
    let gift:GiftEntry,selected:Bool
    var body:some View {
        VStack(alignment:.leading,spacing:12) {
            HStack {Text("GEN \(gift.generation)").font(.caption2.bold());Spacer();Text(gift.origin).font(.caption.bold()).lineLimit(1)}.foregroundStyle(theme.accent)
            HStack(spacing:14) {Group{if gift.entity{PokemonSprite(name:gift.sprite,portrait:gift.portrait)}else{Image(systemName:"gift.fill").font(.largeTitle).foregroundStyle(theme.accent)}}.frame(width:58,height:58).padding(8).background(theme.accent.opacity(0.08),in:RoundedRectangle(cornerRadius:18));VStack(alignment:.leading,spacing:5){Text(gift.name).font(.title3.bold()).lineLimit(1);Text(gift.entity ? (gift.egg ? "Egg gift":"Level \(gift.level)"):"Special delivery").font(.caption).foregroundStyle(.secondary);if gift.shinyRule=="Always"{Label("Shiny",systemImage:"sparkles").font(.caption2).foregroundStyle(.orange)}}}
            Text(gift.title).font(.callout).lineLimit(2).frame(height:36,alignment:.topLeading).frame(maxWidth:.infinity,alignment:.leading)
            HStack {Label(gift.languageText,systemImage:"globe").font(.caption2).foregroundStyle(.secondary).help(gift.languageDetails);Spacer();if gift.source=="Folder"{Image(systemName:"folder").foregroundStyle(.secondary).help(gift.file)}}
            HStack {Text("Card \(gift.card)").font(.caption).foregroundStyle(.secondary);Spacer();Image(systemName:selected ? "checkmark.circle.fill":"circle").foregroundStyle(selected ? theme.accent:Color.secondary.opacity(0.4))}
        }.padding(18).background(selected ? theme.accent.opacity(0.1):Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:20)).overlay(RoundedRectangle(cornerRadius:20).stroke(selected ? theme.accent:theme.accent.opacity(0.18),lineWidth:selected ? 2:1))
    }
}
