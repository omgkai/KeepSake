import SwiftUI

struct PlusRecordsView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var search=""
    @State private var compatibleOnly=false
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack{Text("Z-A Move-Plus Records").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            Text("Mark moves upgraded to their + version in Legends: Z-A. These are separate from TM records. Use Set to Slot afterward.").foregroundStyle(.secondary)
            HStack{TextField("Find a move or type…",text:$search).textFieldStyle(.roundedBorder);Toggle("Learnable only",isOn:$compatibleOnly).toggleStyle(.checkbox)}
            if let data=model.plusRecords {
                ScrollView{LazyVStack(spacing:0){ForEach(data.entries.filter{(!compatibleOnly || $0.permitted) && (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || $0.type.localizedCaseInsensitiveContains(search))}){entry in
                    HStack{VStack(alignment:.leading,spacing:3){Text(entry.name);Text(entry.type+(entry.permitted ? "" : " · Not in the known learning path")).font(.caption).foregroundStyle(entry.permitted ? Color.secondary : .orange)};Spacer();Toggle("\(entry.name) upgraded",isOn:Binding(get:{entry.learned},set:{v in Task{await model.changePlus(["mode":"one","index":entry.id,"learned":v])}})).labelsHidden()}.padding(.vertical,9)
                    Divider().opacity(0.35)
                }}}
                HStack{Menu("Suggest upgrades") {
                    Button("Naturally available at this level"){change("current")}
                    Button("Include compatible TMs"){change("tm")}
                    Button("Include Seed of Mastery and TMs"){change("seed")}
                };Button("Clear All"){change("clear")};Spacer();Text("Edit → Undo restores changes").font(.caption).foregroundStyle(.secondary)}
            } else {ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity)}
        }.padding(24).frame(width:660,height:620).disabled(model.busy).task{await model.loadPlus()}
    }
    func change(_ mode:String){Task{await model.changePlus(["mode":mode])}}
}
extension EditorModel {
    func loadPlus() async {
        guard !busy else{return};busy=true;defer{busy=false}
        do{plusRecords=try await bridge.send(["op":"plusRecords"],as:MoveRecordData.self)}catch{self.error=error.localizedDescription}
    }
    func changePlus(_ values:[String:Any]) async {
        guard !busy else{return};busy=true;defer{busy=false}
        do{var request=values;request["op"]="plusRecordsSet";state=try await bridge.send(request,as:EditorState.self);plusRecords=try await bridge.send(["op":"plusRecords"],as:MoveRecordData.self);status="Move-plus records updated — Set to Slot when ready"}catch{self.error=error.localizedDescription}
    }
}
