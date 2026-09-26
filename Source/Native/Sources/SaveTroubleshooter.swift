import SwiftUI
import AppKit
private struct SaveOpenOptions:Decodable {
    struct Version:Decodable,Identifiable {var id:String{value};let value,label,type:String}
    let handlers,types,languages:[Choice]
    let versions:[Version]
}
struct SaveTroubleshooterView:View {
    @EnvironmentObject private var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var options:SaveOpenOptions?
    @State private var handler="0"
    @State private var type=""
    @State private var version="Any"
    @State private var language="None"
    @State private var file:URL?
    @State private var message=""
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            HStack{Label("Open with format options",systemImage:"doc.badge.gearshape").font(.title2.bold());Spacer();Button("Cancel"){dismiss()}}
            Text("For saves that automatic recognition cannot open. Choose the original game and file container; this does not convert a save to another game.").foregroundStyle(.secondary)
            HStack{Text(file?.lastPathComponent ?? "Drop a save here or choose a file").lineLimit(2);Spacer();Button("Choose File…"){let p=NSOpenPanel();p.canChooseDirectories=false;if p.runModal() == .OK {file=p.url}}}
            if let options {Form {
                Picker("File handler",selection:$handler){ForEach(options.handlers){Text($0.label).tag($0.value)}}
                Picker("Save format",selection:$type){ForEach(options.types){Text($0.label).tag($0.value)}}
                Picker("Game edition",selection:$version){Text("Automatic").tag("Any");ForEach(options.versions.filter{$0.type==type}){Text($0.label).tag($0.value)}}
                Picker("Language",selection:$language){ForEach(options.languages){Text($0.label).tag($0.value)}}
            }.formStyle(.grouped)}else{ProgressView()}
            if !message.isEmpty {Text(message).foregroundStyle(.orange)}
            HStack{Spacer();Button("Open Save"){open()}.buttonStyle(.borderedProminent).disabled(file==nil || type.isEmpty || model.busy)}
        }.padding(24).frame(width:660,height:500).onChange(of:type){_,_ in version="Any"}.dropDestination(for:FileDropItem.self){items,_ in do{file=try FileDropSelection.file(from:items.map(\.url));return true}catch{message=error.localizedDescription;return false}}.task{do{let value=try await model.bridge.send(["op":"saveOpenOptions"],as:SaveOpenOptions.self);options=value;type=value.types.first?.value ?? ""}catch{message=error.localizedDescription}}
    }
    private func open(){guard let file,!model.busy,model.confirmDiscard(clearDrafts:false) else{return};Task{await model.command(["op":"open","path":file.path,"handler":Int(handler) ?? 0,"saveType":type,"saveVersion":version,"saveLanguage":language],status:"Opened \(file.lastPathComponent)");if model.error==nil{dismiss()}else{message=model.error ?? "Could not open this save."}}}
}
