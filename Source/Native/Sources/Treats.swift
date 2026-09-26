import SwiftUI

struct TreatEntry:Codable,Identifiable {let id:Int,name:String,fields:[Field]}
struct TreatData:Codable {let supported:Bool,revision:Int,kind:String,collections:[Choice],entries:[TreatEntry];let cookingCount:Int?}
struct TreatsView:View {
    @EnvironmentObject var model:EditorModel
    @State private var data:TreatData?
    @State private var kind=""
    @State private var search=""
    @State private var selected:TreatEntry?
    @State private var cookingCount=""
    var body:some View {VStack(alignment:.leading,spacing:18) {
        HStack {Label("Treat case",systemImage:"birthday.cake.fill").font(.largeTitle.bold());Spacer();if let data {
            Button {act("give",data)} label:{Label("Give All",systemImage:"sparkles")}.buttonStyle(.borderedProminent)
            Menu {if data.kind=="puffs" {Button("Fill with Best Puffs"){act("best",data)};Button("Sort Puffs"){act("sort",data)}};Button("Empty Case"){act("clear",data)}}label:{Label("Case actions",systemImage:"ellipsis.circle").labelStyle(.iconOnly)}
        }}
        Text("Your game's Pokéblocks, Poffins, Poké Puffs or Poké Beans. Open a treat to adjust its values; all changes support Undo.").foregroundStyle(.secondary)
        if let data {
            if !data.supported {ContentUnavailableView("No treat case in this game",systemImage:"birthday.cake")}
            else {
                HStack {if data.collections.count>1 {Picker("Case",selection:$kind){ForEach(data.collections){Text($0.label).tag($0.value)}}.frame(width:240)};TextField("Search treats…",text:$search).textFieldStyle(.roundedBorder);Text("\(data.entries.count) slots").font(.caption).foregroundStyle(.secondary)}
                if let count=data.cookingCount {
                    HStack(spacing:14) {
                        Image(systemName:"flame.fill").font(.title2).foregroundStyle(.orange)
                        VStack(alignment:.leading,spacing:3) { Text("Poffins cooked").font(.headline);Text("Your total cooking record").font(.caption).foregroundStyle(.secondary) }
                        Spacer()
                        TextField("Count",text:$cookingCount).textFieldStyle(.roundedBorder).frame(width:120)
                        Button("Save") { Task { await model.command(["op":"treatsSet","kind":data.kind,"mode":"cooking","revision":data.revision,"edits":[["field":"CookingCount","value":cookingCount]]]) } }.disabled(Int(cookingCount)==nil || Int(cookingCount)==count)
                    }.padding(16).background(.orange.opacity(0.08),in:RoundedRectangle(cornerRadius:16))
                }
                ScrollView {LazyVGrid(columns:[GridItem(.adaptive(minimum:190,maximum:280))],spacing:12){ForEach(data.entries.filter{search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || String($0.id+1)==search}){entry in
                    Button{selected=entry}label:{HStack(spacing:12){Image(systemName:data.kind=="beans" ? "leaf.fill" : "birthday.cake.fill").font(.title2).foregroundStyle(.tint).frame(width:40,height:45).background(.tint.opacity(0.1),in:RoundedRectangle(cornerRadius:12));VStack(alignment:.leading,spacing:5){Text(entry.name).font(.headline).lineLimit(2);Text(entry.fields.first(where:{$0.id=="Count"}).map{"\($0.value) owned"} ?? "Slot \(entry.id+1)").font(.caption).foregroundStyle(.secondary)};Spacer(minLength:0)}.frame(maxWidth:.infinity,minHeight:60,alignment:.leading).padding(12).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:16))}.buttonStyle(.plain)
                }}}
            }
        }else{ProgressView()}
    }.padding(24).disabled(model.busy || model.fieldDrafts).task(id:"\(model.state.revision):\(kind)"){do{data=try await model.bridge.send(["op":"treats","kind":kind],as:TreatData.self);cookingCount=data?.cookingCount.map(String.init) ?? "";if let current=data?.kind,kind != current{kind=current}}catch{model.error=error.localizedDescription}}.sheet(item:$selected){entry in if let data{TreatEditor(entry:entry,kind:data.kind,revision:data.revision)}}}
    private func act(_ mode:String,_ data:TreatData){Task{await model.command(["op":"treatsSet","kind":data.kind,"mode":mode,"revision":data.revision])}}
}
private struct TreatEditor:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let entry:TreatEntry,kind:String,revision:Int
    @State private var values:[String:String]=[:]
    var body:some View {VStack(alignment:.leading,spacing:18){HStack{Label("Treat \(entry.id+1)",systemImage:"birthday.cake.fill").font(.title2.bold());Spacer();Button("Cancel"){dismiss()};Button("Save"){Task{await model.command(["op":"treatsSet","kind":kind,"revision":revision,"id":entry.id,"edits":entry.fields.map{["field":$0.id,"value":values[$0.id] ?? $0.value]}]);if model.error==nil{dismiss()}}}.buttonStyle(.borderedProminent)}
        Form{ForEach(entry.fields.sorted{rank($0)<rank($1)}){field in if field.kind=="bool" {Toggle(field.label,isOn:Binding(get:{(values[field.id] ?? field.value).lowercased()=="true"},set:{values[field.id]=$0 ? "true" : "false"}))}else if !field.choices.isEmpty {Picker(field.label,selection:binding(field)){ForEach(field.choices){Text($0.label).tag($0.value)}}}else{TextField(field.label,text:binding(field)).textFieldStyle(.roundedBorder)}}}.formStyle(.grouped)
    }.padding(24).frame(width:530,height:520).disabled(model.busy).onAppear{values=Dictionary(uniqueKeysWithValues:entry.fields.map{($0.id,$0.value)})}}
    private func rank(_ field:Field)->String {(["Type","MstID","Color"].contains(field.id) ? "0" : field.id=="Level" ? "1" : field.kind=="bool" ? "3" : "2")+field.id}
    private func binding(_ field:Field)->Binding<String>{Binding(get:{values[field.id] ?? field.value},set:{values[field.id]=$0})}
}
