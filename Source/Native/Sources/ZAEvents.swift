import SwiftUI
import AppKit
struct ZAEventPage:Codable {let revision:Int;let group:String;let groups:[Choice];let count,total,offset:Int;let entries:[ExtraRow]}
private struct ZAEventSelection:Identifiable {let id=UUID();let row:ExtraRow;let group:String;let revision:Int}
struct ZAEventsView:View {
    @EnvironmentObject var model:EditorModel
    @State private var group="Flags"
    @State private var search=""
    @State private var showEmpty=false
    @State private var offset=0
    @State private var page:ZAEventPage?
    @State private var error=""
    @State private var selection:ZAEventSelection?
    @State private var namesVersion=0
    @State private var comparing=false
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            Text("Inspect flags, quest progress and world records. Keys can be hexadecimal hashes or names; custom names use the game’s FNV-1a hash.").font(.callout).foregroundStyle(.secondary)
            HStack {Picker("Collection",selection:$group){ForEach(page?.groups ?? [Choice(value:"Flags",label:"System flags")]){Text($0.label).tag($0.value)}};Spacer();Button("Compare Saves…"){comparing=true};Button("Load Key Names…"){loadNames()}.help("Load a PKHeX key-name text file: hexadecimal key, tab, name.")}
            HStack {TextField("Search keys, names, values or entry number…",text:$search).textFieldStyle(.roundedBorder);Toggle("Show empty entries",isOn:$showEmpty).toggleStyle(.checkbox)}
            if !error.isEmpty {Text(error).foregroundStyle(.red);Button("Try Again"){Task{await load()}}}
            if let page {
                ScrollView {LazyVStack(spacing:8){ForEach(page.entries){row in Button{selection=ZAEventSelection(row:row,group:page.group,revision:page.revision)}label:{HStack {Image(systemName:"key.horizontal").foregroundStyle(.tint);VStack(alignment:.leading,spacing:5){Text(row.name.isEmpty ? "Unnamed entry":row.name).font(.system(.callout,design:.monospaced)).lineLimit(2);Text(row.detail).font(.caption).foregroundStyle(.secondary)};Spacer();Image(systemName:"chevron.right").font(.caption)}.padding(14).frame(maxWidth:.infinity,alignment:.leading).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:14))}.buttonStyle(.plain)}}}.overlay{if page.entries.isEmpty{ContentUnavailableView("No matching records",systemImage:"list.bullet.rectangle",description:Text("Try another search or show empty entries to add a record."))}}
                HStack {Text("\(page.total) matches · \(page.count) stored slots").font(.caption).foregroundStyle(.secondary);Spacer();Button("Previous"){offset=max(0,offset-100)}.disabled(offset==0);Text("\(page.total==0 ? 0:offset+1)–\(min(offset+100,page.total))").font(.caption.monospacedDigit());Button("Next"){offset+=100}.disabled(offset+100>=page.total)}
            } else if error.isEmpty {ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity)}
            Text("Changes support Undo. Removing an entry compacts the collection, just as in PKHeX.").font(.caption).foregroundStyle(.secondary)
        }.onChange(of:group){_,_ in offset=0}.onChange(of:search){_,_ in offset=0}.onChange(of:showEmpty){_,_ in offset=0}
        .task(id:"\(model.state.revision):\(group):\(search):\(showEmpty):\(offset):\(namesVersion)"){await load()}
        .sheet(item:$selection){ZAEventEditor(selection:$0)}.sheet(isPresented:$comparing){EventCompareView(operation:"zaEventCompare")}
    }
    private func load() async {do {try await Task.sleep(for:.milliseconds(150));let p=try await model.bridge.send(["op":"zaEvents","group":group,"search":search,"showEmpty":showEmpty,"offset":offset],as:ZAEventPage.self);try Task.checkCancellation();page=p;error=""}catch is CancellationError{}catch{self.error=error.localizedDescription;page=nil}}
    private func loadNames(){let panel=NSOpenPanel();panel.canChooseDirectories=false;panel.allowsMultipleSelection=false;guard panel.runModal() == .OK,let url=panel.url else{return};Task{do{struct Result:Decodable{let count:Int};let r=try await model.bridge.send(["op":"zaEventNames","path":url.path],as:Result.self);namesVersion+=1;model.status="Loaded \(r.count) event key names"}catch{self.error=error.localizedDescription}}}
}
private struct ZAEventEditor:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let selection:ZAEventSelection
    @State private var values:[String:String]=[:]
    private var edits:[[String:String]] {selection.row.fields.compactMap{f in guard let v=values[f.id],v != f.value else{return nil};return["field":f.id,"value":v]}}
    var body:some View {VStack(alignment:.leading,spacing:18){HStack{Text("Event record · \(selection.row.id)").font(.title2.bold());Spacer();Button("Cancel"){dismiss()};Button("Save Changes"){save("edit")}.buttonStyle(.borderedProminent).disabled(edits.isEmpty || model.busy)};Text("Blank key names mark empty keys. Values use unsigned whole numbers; the full 64-bit range is preserved.").font(.callout).foregroundStyle(.secondary);Form{ForEach(selection.row.fields){f in if f.kind=="bool"{Toggle(f.label,isOn:Binding(get:{(values[f.id] ?? f.value)=="true"},set:{values[f.id]=$0 ? "true":"false"}))}else{TextField(f.label,text:Binding(get:{values[f.id] ?? f.value},set:{values[f.id]=$0})).textFieldStyle(.roundedBorder)}}}.formStyle(.grouped);Button("Remove Entry",role:.destructive){save("clear")}.disabled(model.busy);Text("Save changes in memory, then Export Copy to write a new save file.").font(.caption).foregroundStyle(.secondary)}.padding(24).frame(width:700,height:450).onAppear{values=Dictionary(uniqueKeysWithValues:selection.row.fields.map{($0.id,$0.value)})}}
    private func save(_ mode:String){let payload:[String:Any]=["op":"zaEventSet","group":selection.group,"index":Int(selection.row.id) ?? -1,"revision":selection.revision,"mode":mode,"edits":edits];Task{await model.command(payload,status:"Z-A event record updated");if model.error==nil{dismiss()}}}
}
