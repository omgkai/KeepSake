import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct PathResult:Codable {let path:String}
struct GuideEntry:Codable,Identifiable {let id:String,species:Int,form:Int,name:String,formName:String,sprite:String,native:Bool,total:Int,catchRate:Int,type1:Int,type2:Int,type1Name:String,type2Name:String,stats:[Int],abilities:[String],alphaMove:String}
struct GuideData:Codable {let game:String,generation:Int,entries:[GuideEntry]}
struct EventVariable:Codable,Identifiable {let id:Int,value:String,kind:String}
struct EventVariableData:Codable {let supported:Bool,revision:Int,entries:[EventVariable]}
struct SaveBlockRow:Codable,Identifiable {let id:String,name:String,kind:String,size:Int,value:String}
struct SaveBlockList:Codable {let supported:Bool,revision:Int,total:Int,entries:[SaveBlockRow]}
struct SaveBlockDetail:Codable,Identifiable {let id:String,revision:Int,kind:String,size:Int,value:String,editable:Bool,hex:String}
struct SaveToolsView:View {
    @EnvironmentObject var model:EditorModel
    @State private var tab="Species Guide"
    var body:some View {VStack(alignment:.leading,spacing:20) {
        Label("Save tools",systemImage:"wrench.and.screwdriver.fill").font(.largeTitle.bold())
        Picker("Tool",selection:$tab){ForEach(["Species Guide","Event Variables","Save Blocks"],id:\.self){Text($0)}}.pickerStyle(.segmented)
        if !model.state.hasSave {ContentUnavailableView("Open a game save",systemImage:"doc",description:Text("These tools follow the format of your loaded game."))}
        else if tab=="Species Guide" {SpeciesGuideView()}
        else if tab=="Event Variables" {EventVariablesView()}
        else {SaveBlocksView()}
    }.padding(24).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.topLeading)}
}
struct SpeciesGuideView:View {
    @EnvironmentObject var model:EditorModel
    @State private var data:GuideData?
    @State private var search=""
    @State private var sort="Dex order"
    @State private var nativeOnly=false
    private var rows:[GuideEntry] {
        let rows=(data?.entries ?? []).filter{(!nativeOnly || $0.native) && (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || $0.formName.localizedCaseInsensitiveContains(search) || String($0.species)==search || $0.abilities.contains{$0.localizedCaseInsensitiveContains(search)})}
        return sort=="Highest total" ? rows.sorted{$0.total==$1.total ? $0.id<$1.id : $0.total>$1.total} : rows.sorted{$0.id<$1.id}
    }
    var body:some View {VStack(alignment:.leading,spacing:14) {
        Text("Species, forms and base stats from this game's personal table. Read-only, like PKHeX's KChart.").font(.callout).foregroundStyle(.secondary)
        HStack{TextField("Search species, forms or abilities…",text:$search).textFieldStyle(.roundedBorder);Toggle("Native",isOn:$nativeOnly);Picker("Sort",selection:$sort){Text("Dex order").tag("Dex order");Text("Highest total").tag("Highest total")}.frame(width:210)}
        HStack {Text("\(rows.count) forms").font(.caption).foregroundStyle(.secondary);Spacer();Text("HP  ATK  DEF  SpA  SpD  SPE").font(.system(.caption,design:.monospaced)).frame(width:232);Text("BST").font(.caption.bold()).frame(width:45)}
        ScrollView {LazyVStack(spacing:8){ForEach(rows){row in guideRow(row)}}}
    }.task(id:model.state.gameVersion){do{data=try await model.bridge.send(["op":"speciesGuide"],as:GuideData.self)}catch{model.error=error.localizedDescription}}}
    private func guideRow(_ row:GuideEntry)->some View {HStack(spacing:14) {
        PokemonSprite(name:row.sprite).frame(width:45,height:40)
        VStack(alignment:.leading,spacing:4){Text(row.name+(row.formName.isEmpty ? "" : " · "+row.formName)).font(.callout.bold());Text(row.abilities.joined(separator:" · ")).font(.caption).foregroundStyle(.secondary);if !row.alphaMove.isEmpty {Text("Alpha encounter move · "+row.alphaMove).font(.caption2).foregroundStyle(.secondary)}}.frame(minWidth:180,maxWidth:.infinity,alignment:.leading)
        VStack(alignment:.leading,spacing:4){MoveTypeBadge(type:row.type1,name:row.type1Name,compact:true);if row.type1 != row.type2{MoveTypeBadge(type:row.type2,name:row.type2Name,compact:true)}}.frame(width:60)
        Text("Catch \(row.catchRate)").font(.caption2).foregroundStyle(.secondary).frame(width:56)
        HStack(spacing:2){ForEach(Array(row.stats.enumerated()),id:\.offset){_,value in Text("\(value)").font(.system(.caption,design:.monospaced).weight(.semibold)).frame(width:36,height:28).background(Color.green.opacity(Double(value)/600),in:RoundedRectangle(cornerRadius:6))}}.frame(width:232)
        Text("\(row.total)").font(.callout.bold()).frame(width:45)
    }.padding(12).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:14))}
}
struct EventVariablesView:View {
    @EnvironmentObject var model:EditorModel
    @State private var data:EventVariableData?
    @State private var search=""
    @State private var changedOnly=false
    var body:some View {VStack(alignment:.leading,spacing:14) {
        Text("Advanced story and event counters. Values have game-specific meanings; changing a counter does not automatically update related event flags.").font(.callout).foregroundStyle(.secondary)
        HStack{TextField("Variable index…",text:$search).textFieldStyle(.roundedBorder);Toggle("Nonzero only",isOn:$changedOnly)}
        if let data {
            if !data.supported {ContentUnavailableView("No indexed variables",systemImage:"slider.horizontal.3",description:Text("This format has no supported indexed event-variable interface. Switch saves may expose them under Save Blocks."))}
            else {ScrollView{LazyVStack(spacing:8){ForEach(data.entries.filter{(search.isEmpty || String($0.id).contains(search)) && (!changedOnly || $0.value != "0")}){row in EventVariableRow(entry:row,revision:data.revision)}}}}
        } else {ProgressView()}
    }.task(id:model.state.revision){do{data=try await model.bridge.send(["op":"eventWork"],as:EventVariableData.self)}catch{model.error=error.localizedDescription}}}
}
private struct EventVariableRow:View {
    @EnvironmentObject var model:EditorModel
    let entry:EventVariable,revision:Int
    @State private var value=""
    var body:some View {HStack{Label("Variable \(entry.id)",systemImage:"number").frame(width:160,alignment:.leading);Text(entry.kind).font(.caption).foregroundStyle(.secondary);Spacer();TextField("Value",text:$value).textFieldStyle(.roundedBorder).frame(width:160);Button("Apply"){Task{await model.command(["op":"eventWorkSet","id":entry.id,"value":value,"revision":revision])}}.disabled(value==entry.value || model.busy || model.fieldDrafts)}.padding(10).background(.quaternary.opacity(0.3),in:RoundedRectangle(cornerRadius:10)).onAppear{value=entry.value}.onChange(of:entry.value){_,v in value=v}}
}
struct SaveBlocksView:View {
    @EnvironmentObject var model:EditorModel
    @State private var data:SaveBlockList?
    @State private var search=""
    @State private var page=0
    @State private var detail:SaveBlockDetail?
    @State private var review:BlockReviewData?
    @State private var reading=false
    @State private var rawExport=false
    var body:some View {VStack(alignment:.leading,spacing:14) {
        Text("Explore your save’s blocks, compare another save, or review a group of changes before importing.").font(.callout).foregroundStyle(.secondary)
        HStack{TextField("Search block name or hexadecimal key…",text:$search).textFieldStyle(.roundedBorder);Menu("Import & compare") {Button("Compare Another Save…"){read("compare")};Divider();Button("Import Block Folder…"){read("folder")};Button("Import KeepSake Archive…"){read("archive")}}.disabled(data?.supported != true || reading || model.busy || model.fieldDrafts)
            Menu("Export") {Button("All Blocks as ZIP…"){exportAll()};Button("Raw Binary…"){rawExport=true}}.disabled(data?.supported != true || model.busy)}
        if reading {ProgressView("Preparing block review…").controlSize(.small)}
        if let data {
            if !data.supported {ContentUnavailableView("Not a block-based save",systemImage:"square.stack.3d.up",description:Text("Use Event Variables or Advanced Save for this game."))}
            else {HStack{Text("\(data.total) blocks").font(.caption).foregroundStyle(.secondary);Spacer();Button("Previous"){page=max(0,page-1)}.disabled(page==0);Text("Page \(page+1)").font(.caption);Button("Next"){page+=1}.disabled((page+1)*250>=data.total)}
                ScrollView{LazyVStack(spacing:6){ForEach(data.entries){row in Button{Task{do{detail=try await model.bridge.send(["op":"saveBlock","key":row.id],as:SaveBlockDetail.self)}catch{model.error=error.localizedDescription}}}label:{HStack{Text(row.id).font(.system(.caption,design:.monospaced)).frame(width:85);VStack(alignment:.leading){Text(row.name).lineLimit(1);Text("\(row.kind) · \(row.size) bytes").font(.caption).foregroundStyle(.secondary)};Spacer();Text(row.value).font(.caption.monospaced()).lineLimit(1)}}.buttonStyle(.plain).padding(10).background(.quaternary.opacity(0.3),in:RoundedRectangle(cornerRadius:10))}}}
            }
        }else{ProgressView()}
    }.onChange(of:search){_,_ in page=0}.task(id:"\(model.state.revision):\(search):\(page)"){do{try await Task.sleep(for:.milliseconds(200));let result=try await model.bridge.send(["op":"saveBlocks","search":search,"offset":page*250],as:SaveBlockList.self);try Task.checkCancellation();data=result}catch is CancellationError{}catch{model.error=error.localizedDescription}}.sheet(item:$detail){SaveBlockEditor(data:$0)}.sheet(item:$review){BlockReviewSheet(data:$0)}.sheet(isPresented:$rawExport){RawBlockExportSheet()}}
    private func read(_ mode:String) {
        let panel=NSOpenPanel();panel.canChooseDirectories=mode == "folder";panel.canChooseFiles=mode != "folder";panel.allowsMultipleSelection=false
        panel.title=mode == "compare" ? "Compare another save" : mode == "folder" ? "Choose a block folder" : "Choose a KeepSake block archive"
        if mode == "archive" {panel.allowedContentTypes=[.zip]}
        guard panel.runModal() == .OK,let url=panel.url else{return}
        reading=true
        Task {defer{reading=false};do{review=try await model.bridge.send(["op":"saveBlocksReview","mode":mode,"path":url.path],as:BlockReviewData.self)}catch{model.error=error.localizedDescription}}
    }
    private func exportAll(){let panel=NSSavePanel();panel.nameFieldStringValue="KeepSake Save Blocks.zip";panel.allowedContentTypes=[.zip];guard panel.runModal() == .OK,let url=panel.url else{return};Task{do{_ = try await model.bridge.send(["op":"saveBlocksExport","path":url.path],as:PathResult.self);model.status="Exported save blocks"}catch{model.error=error.localizedDescription}}}
}
private struct SaveBlockEditor:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let data:SaveBlockDetail
    @State private var value=""
    var body:some View {VStack(alignment:.leading,spacing:18) {
        HStack{Label("Block "+data.id,systemImage:"square.stack.3d.up").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
        Text("\(data.kind) · \(data.size) bytes").foregroundStyle(.secondary)
        if data.editable {TextField("Stored value",text:$value).textFieldStyle(.roundedBorder);Button("Apply Value"){apply(["value":value])}.buttonStyle(.borderedProminent).disabled(value==data.value)}
        else {Text("First \(min(256,data.size)) bytes").font(.caption);ScrollView{Text(data.hex).font(.system(.caption,design:.monospaced)).textSelection(.enabled).fixedSize(horizontal:false,vertical:true)}}
        HStack{Button("Export Block…"){exportBlock()};Button("Import Block…"){importBlock()}.disabled(data.size==0);Spacer()}
        Text("Import preserves the block's size and stored type. Related blocks are not changed automatically.").font(.caption).foregroundStyle(.secondary)
    }.padding(24).frame(width:600,height:390).disabled(model.busy || model.fieldDrafts).onAppear{value=data.value}}
    private func apply(_ fields:[String:Any]){var request=fields;request["op"]="saveBlockSet";request["key"]=data.id;request["revision"]=data.revision;Task{await model.command(request);if model.error==nil{dismiss()}}}
    private func importBlock(){let p=NSOpenPanel();guard p.runModal() == .OK,let url=p.url else{return};apply(["mode":"import","path":url.path])}
    private func exportBlock(){let p=NSSavePanel();p.nameFieldStringValue=data.id+".bin";guard p.runModal() == .OK,let url=p.url else{return};Task{do{_ = try await model.bridge.send(["op":"saveBlockExport","key":data.id,"path":url.path],as:PathResult.self);model.status="Exported block "+data.id}catch{model.error=error.localizedDescription}}}
}
