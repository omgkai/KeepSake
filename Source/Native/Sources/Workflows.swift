import SwiftUI

struct MoveRecord:Codable,Identifiable { let id:Int, move:Int; let name:String, type:String; let permitted:Bool, learned:Bool, mastered:Bool }
struct MoveRecordData:Codable { let shop:Bool; let entries:[MoveRecord] }
struct ResearchTask:Codable,Identifiable { let id:Int, name:String, count:Int, active:Bool, thresholds:[Int], required:Bool, reported:Int, points:Int, bonus:Bool, editable:Bool }
struct ResearchData:Codable { let species:Int, name:String, tasks:[ResearchTask], choices:[Choice], unreportedPoints:Int, points:Int, perfect:Bool, solitude:Bool }
struct StorageEntry:Codable,Identifiable { let id:String, box:Int, slot:Int, party:Bool, name:String, nickname:String, species:Int, level:Int, shiny:Bool, alpha:Bool, nature:String, ability:String, item:String, trainer:String, moves:String, sprite:String
    var location:String { party ? "Party · \(slot+1)" : "Box \(box+1) · \(slot+1)" }
}
struct ExportResult:Codable { let count:Int, path:String }

struct MoveRecordsView:View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var search=""
    @State private var permittedOnly=false
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack { Text(model.moveRecords?.shop == true ? "Move Shop & Mastery" : "TM / TR Records").font(.title2.bold()); Spacer(); Button("Done") {dismiss()}.keyboardShortcut(.cancelAction) }
            Text(model.moveRecords?.shop == true ? "Purchased tracks move-shop lessons. Mastered tracks access to Agile and Strong Style. Changes apply to the Pokémon in the editor; use Set to Slot afterward." : "These flags record which machines or records the Pokémon has learned. Changes apply to the Pokémon in the editor; use Set to Slot afterward.").font(.callout).foregroundStyle(.secondary)
            HStack { TextField("Find a move or type…",text:$search).textFieldStyle(.roundedBorder); Toggle("Compatible only",isOn:$permittedOnly).toggleStyle(.checkbox) }
            if let data=model.moveRecords {
                HStack { Text("MOVE"); Spacer(); Text(data.shop ? "PURCHASED" : "LEARNED").frame(width:85); if data.shop {Text("MASTERED").frame(width:85)} }.font(.caption).foregroundStyle(.secondary)
                ScrollView {
                    LazyVStack(spacing:0) {
                        ForEach(data.entries.filter { (!permittedOnly || $0.permitted) && (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || $0.type.localizedCaseInsensitiveContains(search)) }) { entry in
                            HStack {
                                VStack(alignment:.leading,spacing:3) { Text(entry.name); Text(entry.type + (entry.permitted ? "" : " · Not compatible with current species/form")).font(.caption).foregroundStyle(entry.permitted ? Color.secondary : Color.orange) }
                                Spacer()
                                Toggle("\(entry.name) learned",isOn:Binding(get:{entry.learned},set:{v in Task{await model.changeMoveRecord(entry,learned:v,mastered:entry.mastered)}})).labelsHidden().frame(width:85)
                                if data.shop { Toggle("\(entry.name) mastered",isOn:Binding(get:{entry.mastered},set:{v in Task{await model.changeMoveRecord(entry,learned:entry.learned,mastered:v)}})).labelsHidden().frame(width:85) }
                            }.padding(.vertical,9)
                            Divider().opacity(0.35)
                        }
                    }
                }
                HStack {
                    Menu("Set records") {
                        Button("Suggest for current moves") {change("current")}
                        Button {change("all")} label:{Label(data.shop ? "Give All Compatible Lessons" : "Give All Legal Records",systemImage:"gift.fill")}
                        if !data.shop { Button("All flags, including incompatible") {change("force")} }
                    }
                    Button("Clear All") {change("clear")}
                    Spacer()
                    Text("Undo is available in the Edit menu").font(.caption).foregroundStyle(.secondary)
                }
            } else { ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity) }
        }.padding(24).frame(width:680,height:620).disabled(model.busy)
            .task {await model.loadMoveRecords()}
    }
    private func change(_ mode:String) {Task{await model.moveRecordAction(mode)}}
}
struct ResearchView:View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    @State private var showUnused=false
    @State private var showDexDetails=false
    var body:some View {
        if !model.state.canResearch { ContentUnavailableView("Arceus Research",systemImage:"list.clipboard",description:Text("Open a Legends: Arceus save to edit research task counts.")) }
        else if let data=model.research {
            VStack(alignment:.leading,spacing:18) {
                HStack {
                    VStack(alignment:.leading,spacing:5) {Text("Hisui research notebook").font(.title2.bold()); Text("Edit task counts, then report progress when you're ready.").foregroundStyle(.secondary)}
                    Spacer()
                    PokemonSprite(name:"b_\(data.species)").frame(width:72,height:58)
                }
                HStack {
                    Picker("Pokémon",selection:Binding(get:{model.researchSpecies},set:{id in model.researchSpecies=id;Task{await model.loadResearch()}})) {ForEach(data.choices){Text($0.label).tag(Int($0.value)!)}}.frame(width:300).disabled(model.fieldDrafts)
                    Spacer()
                    Button("Forms & Appearance…"){model.dexDetails=nil;showDexDetails=true}.disabled(model.fieldDrafts)
                    Toggle("Show unused counters",isOn:$showUnused).toggleStyle(.checkbox)
                }
                HStack {
                    Pill(text:"\(data.points) RESEARCH POINTS",color:theme.accent)
                    if data.unreportedPoints > 0 { Pill(text:"+\(data.unreportedPoints) TO REPORT",color:.orange) }
                    if data.perfect {Pill(text:"PERFECT",color:.green)}
                    Spacer()
                    Toggle("Path of Solitude complete",isOn:Binding(get:{data.solitude},set:{v in Task{await model.changeResearch(["mode":"solitude","value":v])}})).toggleStyle(.checkbox).disabled(model.fieldDrafts)
                }
                HStack {Text("TASK");Spacer();Text("COUNT").frame(width:90)}.font(.caption).foregroundStyle(.secondary)
                ScrollView {
                    LazyVStack(spacing:0) {
                        ForEach(data.tasks.filter{showUnused || $0.active}) {task in
                            HStack {
                                VStack(alignment:.leading,spacing:4) {
                                    HStack { Text(task.name).font(.callout.weight(.medium)); if task.bonus { Image(systemName:"chevron.up.2").foregroundStyle(.orange).help("Bonus research points") } }
                                    if !task.thresholds.isEmpty {
                                        HStack(spacing:6) { ForEach(Array(task.thresholds.enumerated()),id:\.offset) { index, threshold in
                                            let reached = (Int(model.drafts["research|\(task.id)"] ?? "") ?? task.count) >= threshold
                                            let color:Color = index < task.reported ? (reached ? .green : .red) : (reached ? .orange : .secondary)
                                            Text(String(threshold)).font(.caption.monospaced().bold()).padding(.horizontal,8).padding(.vertical,4).background(color.opacity(0.14),in:Capsule()).foregroundStyle(color).help(index < task.reported ? (reached ? "Reported" : "Reported milestone exceeds current count") : (reached ? "Ready to report" : "Not yet reached"))
                                        }; Text("\(task.points) points each").font(.caption).foregroundStyle(.secondary) }
                                    }
                                    if !task.editable { Text("Derived from Pokédex forms or game progress").font(.caption).foregroundStyle(.secondary) }
                                    if !task.active {Text("Not a listed task for this species").font(.caption).foregroundStyle(.tertiary)}
                                }
                                Spacer()
                                TextField("Count",text:Binding(get:{model.drafts["research|\(task.id)"] ?? String(task.count)},set:{v in if v==String(task.count){model.drafts.removeValue(forKey:"research|\(task.id)")}else{model.drafts["research|\(task.id)"]=v}})).textFieldStyle(.roundedBorder).frame(width:90).disabled(!task.editable).accessibilityLabel(task.name+" count").onSubmit{Task{await model.saveResearchCounts()}}
                            }.padding(.vertical,11)
                            Divider().opacity(0.4)
                        }
                    }
                }
                HStack {
                    Text("Counts are saved in memory. Export Copy writes your save.").font(.caption).foregroundStyle(.secondary)
                    Spacer()
                    Button("Report Research") {Task{await model.changeResearch(["mode":"report"])}}.disabled(model.fieldDrafts)
                    Button("Save Counts") {Task{await model.saveResearchCounts()}}.buttonStyle(.borderedProminent).disabled(!model.drafts.keys.contains{$0.hasPrefix("research|")})
                }
            }.padding(28).frame(maxWidth:1000).sheet(isPresented:$showDexDetails){DexDetailsView()}
        } else {ProgressView().task{await model.loadResearch()}}
    }
}
struct StorageView:View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    @State private var search=""
    @State private var shinyOnly=false
    @State private var alphaOnly=false
    @State private var selection:String?
    @State private var showBoxTools=false
    @State private var showReport=false
    @State private var showTeam=false
    @State private var expressions=""
    @State private var buildRule=false
    @State private var matchedIDs:Set<String>?
    private var filtered:[StorageEntry] {model.storage.filter{p in (matchedIDs == nil || matchedIDs!.contains(p.id)) && (!shinyOnly || p.shiny) && (!alphaOnly || p.alpha) && (search.isEmpty || [p.name,p.nickname,p.location,p.trainer,p.moves,p.ability,p.nature,p.item,String(p.species)].joined(separator:" ").localizedCaseInsensitiveContains(search))}}
    var body:some View {
        if !model.state.hasSave {ContentUnavailableView("Open a Save File",systemImage:"tray.full")}
        else {
            VStack(alignment:.leading,spacing:16) {
                HStack {
                    VStack(alignment:.leading,spacing:5){Text("Your Pokémon collection").font(.title2.bold());Text("Search every box and your party. Select a row to open it in the editor.").font(.callout).foregroundStyle(.secondary)}
                    Spacer()
                    Button("Import Team…"){showTeam=true}.disabled(model.state.pending || model.fieldDrafts)
                    Button("Organize Boxes…"){showBoxTools=true}.disabled(model.state.pending || model.fieldDrafts)
                    Menu("Export") {
                        Button("Current Box as Pokémon Files…"){model.exportBoxes(all:false)}
                        Button("All Boxes as Pokémon Files…"){model.exportBoxes(all:true)}
                        Button("All Boxes, Including Empty Slots…"){model.exportBoxes(all:true,empty:true)}
                        Divider()
                        Button("Complete Storage Report (CSV)…"){model.exportStorageReport()}
                        Button("Custom Report…"){showReport=true}
                    }.disabled(model.state.pending || model.fieldDrafts)
                }
                HStack {TextField("Search species, nickname, move, item, nature, or trainer…",text:$search).textFieldStyle(.roundedBorder); Toggle("Shiny",isOn:$shinyOnly).toggleStyle(.checkbox);Toggle("Alpha",isOn:$alphaOnly).toggleStyle(.checkbox)}
                DisclosureGroup("Advanced Windows search") {
                    HStack {TextField("=Species=25 | >CurrentLevel=49",text:$expressions).textFieldStyle(.roundedBorder);Button("Add Rule…"){buildRule=true}.sheet(isPresented:$buildRule){SearchRuleBuilder(instructions:$expressions)};Button("Search"){searchExpressions()};Button("Reset"){expressions="";matchedIDs=nil;selection=nil}}
                    Text("Read-only PKHeX property filters. Separate expressions with |. These filters never edit Pokémon.").font(.caption).foregroundStyle(.secondary)
                }
                Table(filtered,selection:$selection) {
                    TableColumn("Pokémon") {p in HStack{PokemonSprite(name:p.sprite).frame(width:36,height:28);VStack(alignment:.leading){Text(p.nickname.isEmpty ? p.name : p.nickname).fontWeight(.medium);if p.nickname != p.name {Text(p.name).font(.caption).foregroundStyle(.secondary)}}}}.width(min:150,ideal:190)
                    TableColumn("Location",value:\.location).width(min:90,ideal:100)
                    TableColumn("Level"){Text(String($0.level))}.width(45)
                    TableColumn("Traits"){p in HStack{if p.shiny {Image(systemName:"sparkles").foregroundStyle(.yellow)};if p.alpha {Text("α").fontWeight(.bold).foregroundStyle(theme.accent)}}}.width(50)
                    TableColumn("Nature",value:\.nature).width(min:70,ideal:80)
                    TableColumn("Ability",value:\.ability)
                    TableColumn("Item",value:\.item)
                }
                HStack {Text("\(filtered.count) shown · \(model.storage.count) total").font(.caption).foregroundStyle(.secondary);Button("Previous"){seek(-1)}.disabled(filtered.isEmpty);Button("Next"){seek(1)}.disabled(filtered.isEmpty);Spacer(); Button("Open in Editor"){if let p=filtered.first(where:{$0.id==selection}){model.selectStored(p)}}.buttonStyle(.borderedProminent).disabled(selection==nil)}
            }.padding(26).sheet(isPresented:$showBoxTools){BoxToolsView()}.onChange(of:model.state.revision){_,_ in matchedIDs=nil}.sheet(isPresented:$showReport){StorageReportSheet(ids:filtered.map(\.id))}.sheet(isPresented:$showTeam){ShowdownTeamSheet()}
        }
    }
    private func seek(_ direction:Int){guard !filtered.isEmpty else{return};let current=filtered.firstIndex{$0.id==selection} ?? (direction>0 ? -1:0);selection=filtered[(current+direction+filtered.count)%filtered.count].id}
    private func searchExpressions(){Task{guard !model.busy else{return};model.busy=true;defer{model.busy=false};do{let rows=try await model.bridge.send(["op":"storageSearch","filters":expressions.replacingOccurrences(of:"|",with:"\n")],as:[StorageEntry].self);matchedIDs=Set(rows.map(\.id));selection=nil}catch{model.error=error.localizedDescription}}}

}

extension EditorModel {
    func loadMoveRecords() async {
        guard !busy else{return};busy=true;defer{busy=false}
        do{moveRecords=try await bridge.send(["op":"moveRecords"],as:MoveRecordData.self)}catch{self.error=error.localizedDescription}
    }
    func changeMoveRecord(_ entry:MoveRecord,learned:Bool,mastered:Bool) async {
        await editMoveRecords(["mode":"one","index":entry.id,"learned":learned,"mastered":mastered])
    }
    func moveRecordAction(_ mode:String) async {await editMoveRecords(["mode":mode])}
    private func editMoveRecords(_ request:[String:Any]) async {
        guard !busy else{return};busy=true;defer{busy=false}
        do {
            state=try await bridge.send(request.merging(["op":"moveRecordsSet"]){_,new in new},as:EditorState.self)
            moveRecords=try await bridge.send(["op":"moveRecords"],as:MoveRecordData.self)
            status="Move records updated — Set to Slot to keep them"
        }catch{self.error=error.localizedDescription}
    }
    func loadResearch() async {
        guard !busy,state.canResearch else{return};busy=true;defer{busy=false}
        do{research=try await bridge.send(["op":"research","species":researchSpecies],as:ResearchData.self)}catch{self.error=error.localizedDescription}
    }
    func changeResearch(_ request:[String:Any]) async {
        guard !busy else{return};busy=true;defer{busy=false}
        do {
            state=try await bridge.send(request.merging(["op":"researchSet","species":researchSpecies]){_,new in new},as:EditorState.self)
            research=try await bridge.send(["op":"research","species":researchSpecies],as:ResearchData.self)
            if request["mode"] as? String == "counts" {drafts=drafts.filter{!$0.key.hasPrefix("research|")}}
            status="Research updated — export a copy to save"
        }catch{self.error=error.localizedDescription}
    }
    func saveResearchCounts() async {
        var edits:[[String:Int]]=[]
        for (key,value) in drafts where key.hasPrefix("research|") {
            guard let id=Int(key.dropFirst(9)),let count=Int(value) else{error="Research counts must be whole numbers.";return}
            edits.append(["id":id,"count":count])
        }
        await changeResearch(["mode":"counts","edits":edits])
    }
    func selectStored(_ row:StorageEntry) {
        guard !busy,confirmDiscard(all:false) else{return}
        Task {await command(["op":"select","box":row.box,"slot":row.slot,"party":row.party]);if error==nil{section="Pokémon"}}
    }
    func exportBoxes(all:Bool,empty:Bool=false) {
        guard !busy,!fieldDrafts,!state.pending else{return}
        let panel=NSOpenPanel();panel.title="Choose a folder for Pokémon files";panel.canChooseFiles=false;panel.canChooseDirectories=true;panel.canCreateDirectories=true
        guard panel.runModal()==NSApplication.ModalResponse.OK,let url=panel.url else{return}
        Task{await exportCollection(["op":"boxExport","path":url.path,"all":all,"empty":empty])}
    }
    func exportStorageReport() {
        guard !busy,!fieldDrafts,!state.pending else{return}
        let panel=NSSavePanel();panel.title="Export complete storage report";panel.nameFieldStringValue="Pokémon collection.csv"
        guard panel.runModal()==NSApplication.ModalResponse.OK,let url=panel.url else{return}
        Task{await exportCollection(["op":"storageReport","path":url.path])}
    }
    private func exportCollection(_ request:[String:Any]) async {
        guard !busy else{return};busy=true;defer{busy=false}
        do{let result=try await bridge.send(request,as:ExportResult.self);status="Exported \(result.count) Pokémon to \(result.path)"}catch{self.error=error.localizedDescription}
    }
}

extension EditorModel {
    func loadGifts() async {
        guard !busy else{return};busy=true;defer{busy=false}
        do{gifts=try await bridge.send(["op":"gifts"],as:[GiftEntry].self)}catch{self.error=error.localizedDescription}
    }
    func prepareGift(_ gift:GiftEntry) {
        guard !busy,confirmDiscard(all:false) else{return}
        Task{await command(["op":"giftPrepare","id":gift.id],status:"Event Pokémon prepared — review legality before setting to a slot");if error==nil{section="Pokémon"}}
    }
    func exportGift(_ gift:GiftEntry) {
        guard !busy else{return}
        let panel=NSSavePanel();panel.title="Export event card";panel.nameFieldStringValue="Card-\(gift.card).\(gift.extension)"
        guard panel.runModal()==NSApplication.ModalResponse.OK,let url=panel.url else{return}
        Task {
            busy=true;defer{busy=false}
            do{let _:ExportResult=try await bridge.send(["op":"giftExport","id":gift.id,"path":url.path],as:ExportResult.self);status="Exported event card"}catch{self.error=error.localizedDescription}
        }
    }
}

struct BoxToolsView:View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var action="SortSpecies"
    @State private var all=false
    @State private var actions:[Choice]=[]
    @State private var preview:BatchPreview?
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack{Text("Organize boxes").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            Text("Sort, modify, or clear Pokémon with PKHeX's box tools. Preview the exact affected slots, then apply. Undo restores the complete operation.").font(.callout).foregroundStyle(.secondary)
            HStack {
                Picker("Action",selection:$action){ForEach(actions){Text($0.label).tag($0.value)}}
                Picker("Scope",selection:$all){Text("Current box").tag(false);Text("All boxes").tag(true)}.frame(width:200)
            }.onChange(of:action){_,_ in preview=nil}.onChange(of:all){_,_ in preview=nil}
            Button("Preview Changes") {Task{preview=await model.previewBoxes(action:action,all:all)}}.buttonStyle(.borderedProminent)
            Divider()
            if let preview {
                HStack {Text("\(preview.count) slots would change").font(.headline);Spacer();Button(action.hasPrefix("Delete") ? "Apply Deletions" : "Apply Changes") {Task{await model.command(["op":"batchApply","token":preview.token],status:"Box changes applied — export a copy to save");self.preview=nil}}.disabled(preview.count==0)}
                ScrollView {LazyVStack(alignment:.leading,spacing:12){ForEach(Array(preview.changes.enumerated()),id:\.offset){_,change in VStack(alignment:.leading,spacing:4){Text(change.location+" · "+change.name).font(.callout.bold());Text(change.detail).font(.caption).foregroundStyle(.secondary)}.frame(maxWidth:.infinity,alignment:.leading).padding(10).background(.quaternary.opacity(0.25),in:RoundedRectangle(cornerRadius:8))}}}
            } else {ContentUnavailableView("Preview before applying",systemImage:"square.grid.3x3",description:Text("The original file stays unchanged until you export a copy."))}
        }.padding(24).frame(width:720,height:600).disabled(model.busy)
            .task{do{actions=try await model.bridge.send(["op":"boxActions"],as:[Choice].self)}catch{model.error=error.localizedDescription}}
    }
}
extension EditorModel {
    func previewBoxes(action:String,all:Bool) async -> BatchPreview? {
        guard !busy else{return nil};busy=true;defer{busy=false}
        do{return try await bridge.send(["op":"boxPreview","action":action,"all":all],as:BatchPreview.self)}catch{self.error=error.localizedDescription;return nil}
    }
}
