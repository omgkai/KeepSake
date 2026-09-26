import SwiftUI
struct DexRecordData:Codable { let species:Int, name:String, edition:String, fields:[Field] }
struct DexRecordView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let species:Int
    private var groups:[String] { var seen=Set<String>();return (model.dexRecord?.fields ?? []).map(\.group).filter{seen.insert($0).inserted} }
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {VStack(alignment:.leading) {Text(model.dexRecord?.name ?? "Pokédex details").font(.title2.bold());Text(model.dexRecord?.edition ?? "").foregroundStyle(.secondary)};Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            Text("Record which forms and languages you’ve collected, and choose their Pokédex appearance. Each change supports Undo; Export Copy saves your edits.").font(.callout).foregroundStyle(.secondary)
            if let data=model.dexRecord, data.species==species {
                ScrollView {
                    VStack(spacing:16) {
                        ForEach(groups,id:\.self) { group in
                            EditorCard(title:group) {
                                if group != "Overview" && !group.hasPrefix("Display") && !group.localizedCaseInsensitiveContains("unlock") && data.fields.contains(where:{$0.group==group && $0.kind=="bool" && $0.editable}) {
                                    HStack {Spacer();Button("Set All"){changeGroup(group,true)};Button("Clear"){changeGroup(group,false)}}
                                }

                                ForEach(data.fields.filter{$0.group==group}) { field in
                                    if field.kind=="bool" {
                                        Toggle(field.label,isOn:Binding(get:{field.value=="true"},set:{change(field,value:$0 ? "true":"false")})).toggleStyle(.checkbox).foregroundStyle(field.label.lowercased().contains("female") ? Color.pink : field.label.lowercased().contains("male") ? Color.blue : Color.primary)
                                    } else if field.kind=="number" {DexNumericRecord(field:field){change(field,value:$0)}} else {
                                        Picker(field.label,selection:Binding(get:{field.value},set:{change(field,value:$0)})) {
                                            ForEach(field.choices){Text($0.label).foregroundStyle($0.label=="Female" ? Color.pink : $0.label=="Male" ? Color.blue : Color.primary).tag($0.value)}
                                            if !field.choices.contains(where:{$0.value==field.value}) {Text("Stored value: "+field.value).tag(field.value)}
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } else {ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity)}
        }.padding(24).frame(width:610,height:680).disabled(model.busy)
            .task {await model.loadDexRecord(species)}
    }
    private func changeGroup(_ group:String,_ value:Bool){Task{await model.command(["op":"dexRecordGroup","species":species,"group":group,"value":value,"revision":model.state.revision],status:"Pokédex group updated — Undo is available")}}
    private func change(_ field:Field,value:String) { Task {await model.command(["op":"dexRecordSet","species":species,"field":field.id,"value":value],status:"Updated Pokédex record")} }
}
extension EditorModel {
    func loadDexRecord(_ species:Int) async {
        guard !busy else{return};busy=true;defer{busy=false}
        do {dexRecord=try await bridge.send(["op":"dexRecord","species":species],as:DexRecordData.self)}
        catch {self.error=error.localizedDescription}
    }
}

private struct DexNumericRecord:View {
    let field:Field
    let commit:(String)->Void
    @State private var text=""
    var body:some View {HStack{TextField(field.label,text:$text).textFieldStyle(.roundedBorder).onSubmit{commit(text)};Button("Save"){commit(text)}.disabled(text==field.value)}.help(field.help).onAppear{text=field.value}.onChange(of:field.value){_,v in text=v}}
}
