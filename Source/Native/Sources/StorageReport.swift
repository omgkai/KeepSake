import SwiftUI
import AppKit
struct ReportPreviewData:Decodable {struct Row:Decodable,Identifiable{let id:String,values:[String]};let columns:[Choice],rows:[Row]}
struct StorageReportSheet:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let ids:[String]
    @State private var options:[Choice]=[]
    @State private var columns=["Location","Slot","Species","Nickname","Level","Shiny","Alpha","Nature","Ability","Item","Trainer","Moves"]
    @AppStorage("storageReportColumnPreset") private var savedColumns=""
    @State private var query=""
    @State private var preview:ReportPreviewData?
    @State private var showingPreview=false
    @State private var rowSearch=""
    @State private var visibleOnly=true
    @State private var error=""
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {Label("Collection report",systemImage:"tablecells").font(.title2.bold());Spacer();Button("Done"){dismiss()}}
            Toggle("Only the \(ids.count) Pokémon shown in Storage",isOn:$visibleOnly)
            Text("Choose up to 64 columns. The order on the right becomes the order in your CSV.").font(.callout).foregroundStyle(.secondary)
            TextField("Find a column…",text:$query).textFieldStyle(.roundedBorder)
            HStack {
                List(options.filter{query.isEmpty || $0.label.localizedCaseInsensitiveContains(query)},id:\.value){option in
                    Toggle(option.label,isOn:Binding(get:{columns.contains(option.value)},set:{on in if on{if columns.count<64{columns.append(option.value)}}else{columns.removeAll{$0==option.value}}})).toggleStyle(.checkbox)
                }
                List(Array(columns.enumerated()),id:\.element){index,key in HStack {Text(options.first{$0.value==key}?.label ?? key).lineLimit(2);Spacer();Button{columns.swapAt(index,index-1)}label:{Image(systemName:"arrow.up")}.buttonStyle(.borderless).help("Move column earlier").disabled(index==0);Button{columns.swapAt(index,index+1)}label:{Image(systemName:"arrow.down")}.buttonStyle(.borderless).help("Move column later").disabled(index==columns.count-1)}}
            }
            if !error.isEmpty {Text(error).foregroundStyle(.orange)}
            HStack {Button("Save Column Preset"){savedColumns=columns.joined(separator:"|")};Button("Reset Columns"){columns=["Location","Slot","Species","Nickname","Level","Shiny","Alpha","Nature","Ability","Item","Trainer","Moves"]};Text("\(columns.count) columns").font(.caption);Spacer();Button("Preview Table"){Task{do{var r:[String:Any]=["op":"storageReportPreview","columns":columns];if visibleOnly{r["ids"]=ids};preview=try await model.bridge.send(r,as:ReportPreviewData.self);showingPreview=true}catch{self.error=error.localizedDescription}}}.disabled(columns.isEmpty || model.busy);Button("Export CSV…"){export()}.buttonStyle(.borderedProminent).disabled(columns.isEmpty || model.busy)}
        }.padding(24).frame(width:850,height:650).sheet(isPresented:$showingPreview){VStack(alignment:.leading,spacing:16){HStack{Label("Collection report",systemImage:"tablecells").font(.title2.bold());Spacer();Button("Done"){showingPreview=false}};TextField("Search report rows…",text:$rowSearch).textFieldStyle(.roundedBorder);if let preview {ScrollView([.horizontal,.vertical]){LazyVStack(alignment:.leading,spacing:0){HStack(spacing:0){ForEach(preview.columns){Text($0.label).font(.headline).frame(width:150,alignment:.leading).padding(10)}}.background(.tint.opacity(0.1));ForEach(preview.rows.filter{rowSearch.isEmpty || $0.values.contains{$0.localizedCaseInsensitiveContains(rowSearch)}}){row in HStack(spacing:0){ForEach(Array(row.values.enumerated()),id:\.offset){_,value in Text(value).lineLimit(2).frame(width:150,alignment:.leading).padding(10)}};Divider()}}};Text("\(preview.rows.count) Pokémon · columns match your report preset").font(.caption).foregroundStyle(.secondary)}}.padding(24).frame(width:980,height:650)}.task {do{options=try await model.bridge.send(["op":"reportColumns"],as:[Choice].self);let remembered=savedColumns.split(separator:"|").map(String.init).filter{key in options.contains{$0.value==key}};if !remembered.isEmpty {columns=Array(NSOrderedSet(array:remembered)) as? [String] ?? columns}}catch{self.error=error.localizedDescription}}
    }
    private func export() {
        let panel=NSSavePanel();panel.nameFieldStringValue="KeepSake collection.csv"
        guard panel.runModal() == .OK,let url=panel.url else{return}
        Task {guard !model.busy else{return};model.busy=true;defer{model.busy=false};do {var request:[String:Any]=["op":"storageReport","path":url.path,"columns":columns];if visibleOnly{request["ids"]=ids};let result=try await model.bridge.send(request,as:ExportResult.self);model.status="Exported \(result.count) Pokémon to \(result.path)";dismiss()}catch{self.error=error.localizedDescription}}
    }
}
