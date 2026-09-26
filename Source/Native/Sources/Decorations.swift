import SwiftUI

struct RibbonEntry:Codable,Identifiable {
    let id:String, name:String, mark:Bool, count:Bool, value:Int, max:Int, sprite:String, status:String
}
struct ShapeMarking:Codable,Identifiable {let id:String, name:String, value:Int}
struct DecorationData:Codable {
    let entries:[RibbonEntry], markings:[ShapeMarking], colored:Bool, canSuggestMarkings:Bool
    let canAffix:Bool, affixed:Int, affixedChoices:[Choice], analysisNote:String, canSuggest:Bool
}

struct DecorationsView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    @State private var search=""
    @State private var filter="All"
    @State private var ownedOnly=false
    @State private var chooseTitle=false
    private func edit(_ request:[String:Any]) {
        Task{await model.command(request.merging(["op":"decorationsSet"]){_,new in new},status:"Ribbons & markings updated — Set to Slot to keep them")}
    }
    var body:some View {
        if let data=model.state.decorations {
            ScrollView {
                VStack(alignment:.leading,spacing:18) {
                    if !data.markings.isEmpty {markings(data)}
                    if data.entries.isEmpty {
                        ContentUnavailableView("No Ribbons in This Format",systemImage:"rosette",description:Text("Shape markings are shown above when supported."))
                    } else {
                        HStack {
                            Text("Ribbons & marks").font(.headline)
                            Spacer()
                            Text("\(data.entries.filter{$0.value>0}.count) owned").font(.caption).foregroundStyle(.secondary)
                            Menu("Set Ribbons") {
                                Button("Suggest Available Ribbons"){edit(["mode":"suggest"])}.disabled(!data.canSuggest)
                                Button("Keep Required Ribbons Only"){edit(["mode":"required"])}.disabled(!data.canSuggest)
                                Divider()
                                Button {edit(["mode":"all"])} label:{Label("Give All, Including Unobtainable",systemImage:"gift.fill")}
                                Button("Clear All & Equipped Title"){edit(["mode":"clear"])}
                            }.fixedSize()
                        }
                        if data.canAffix {
                            VStack(alignment:.leading,spacing:5) {
                                Text("Equipped ribbon / mark").font(.caption).foregroundStyle(.secondary)
                                Button{chooseTitle=true} label:{HStack{Image(systemName:"rosette");Text(data.affixedChoices.first{$0.value==String(data.affixed)}?.label ?? "Stored title \(data.affixed)").lineLimit(1);Spacer();Image(systemName:"chevron.up.chevron.down").font(.caption2)}}
                                Text("Choosing a title does not award its ribbon or mark.").font(.caption2).foregroundStyle(.secondary)
                            }
                        }
                        TextField("Find a ribbon or mark…",text:$search).textFieldStyle(.roundedBorder)
                        HStack {
                            Picker("Type",selection:$filter){ForEach(["All","Ribbons","Marks"],id:\.self){Text($0)}}.pickerStyle(.segmented)
                            Toggle("Owned only",isOn:$ownedOnly).toggleStyle(.checkbox).fixedSize()
                        }
                        if !data.analysisNote.isEmpty {Text(data.analysisNote).font(.caption).foregroundStyle(.orange)}
                        let entries=data.entries.filter{(!ownedOnly || $0.value>0) && (filter=="All" || (filter=="Marks" ? $0.mark : !$0.mark)) && (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || $0.status.localizedCaseInsensitiveContains(search))}
                        if entries.isEmpty {Text("No matching ribbons or marks.").foregroundStyle(.secondary).padding(.vertical)}
                        LazyVStack(spacing:8) {
                            ForEach(entries){ribbon in ribbonRow(ribbon)}
                        }
                        Text("Available, Missing, and Invalid are hints from PKHeX for this Pokémon. Suggestions may also update linked contest or Super Training records. Review the full legality report after editing.").font(.caption).foregroundStyle(.secondary)
                    }
                }.padding(20).disabled(model.fieldDrafts)
            }.sheet(isPresented:$chooseTitle) {
                ChoiceSheet(title:"Equipped Ribbon or Mark",options:data.affixedChoices,selected:String(data.affixed)){choice in
                    chooseTitle=false
                    if let value=Int(choice.value){edit(["mode":"affix","value":value])}
                }
            }
        } else {ContentUnavailableView("Select a Pokémon",systemImage:"rosette")}
    }
    private func markings(_ data:DecorationData)->some View {
        VStack(alignment:.leading,spacing:12) {
            HStack {
                Text("Shape markings").font(.headline)
                Spacer()
                Menu("Set Markings") {
                    Button("Suggest from IVs"){edit(["mode":"markingsSuggest"])}.disabled(!data.canSuggestMarkings)
                    Button("Clear Shapes"){edit(["mode":"markingsClear"])}
                }.fixedSize()
            }
            HStack(spacing:8) {
                ForEach(data.markings){mark in
                    Button{edit(["mode":"marking","id":mark.id,"value":(mark.value+1) % (data.colored ? 3 : 2)])} label:{
                        VStack(spacing:6) {
                            Image(systemName:mark.name.lowercased()+(mark.value == 0 ? "" : ".fill")).font(.system(size:19)).foregroundStyle(mark.value==0 ? Color.secondary : data.colored ? (mark.value==1 ? Color.blue : Color.pink) : theme.accent)
                            Text(mark.name).font(.system(size:9)).foregroundStyle(.secondary)
                        }.frame(maxWidth:.infinity).padding(.vertical,10).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:9))
                    }.buttonStyle(.plain).accessibilityLabel("\(mark.name) marking: \(mark.value==0 ? "None" : data.colored ? (mark.value==1 ? "Blue" : mark.value==2 ? "Pink" : "Invalid") : "Marked")")
                }
            }
            Text(data.colored ? "Click a shape to cycle None → Blue → Pink. IV suggestions use blue for 31 or 1, pink for 30 or 0." : "Click a shape to toggle it. For six-shape formats, IV suggestions mark stats with 31 IVs.").font(.caption).foregroundStyle(.secondary)
            Divider()
        }
    }
    private func ribbonRow(_ ribbon:RibbonEntry)->some View {
        HStack(spacing:12) {
            RibbonIcon(name:ribbon.sprite).frame(width:38,height:38).opacity(ribbon.value==0 ? 0.4 : 1)
            VStack(alignment:.leading,spacing:3) {
                Text(ribbon.name).font(.callout.weight(.medium))
                if !ribbon.status.isEmpty {Text(ribbon.status).font(.caption2).foregroundStyle(ribbon.status=="Invalid" ? Color.orange : ribbon.status=="Missing" ? Color.orange : Color.secondary)}
            }
            Spacer(minLength:8)
            if ribbon.count {
                Text("\(ribbon.value) / \(ribbon.max)").monospacedDigit().font(.caption)
                Stepper(ribbon.name,value:Binding(get:{ribbon.value},set:{edit(["mode":"ribbon","id":ribbon.id,"value":$0])}),in:0...ribbon.max).labelsHidden().fixedSize()
            } else {
                Toggle(ribbon.name,isOn:Binding(get:{ribbon.value>0},set:{edit(["mode":"ribbon","id":ribbon.id,"value":$0 ? 1 : 0])})).labelsHidden().toggleStyle(.checkbox)
            }
        }.padding(14).background(ribbon.value>0 ? theme.accent.opacity(0.09) : Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:15)).overlay(RoundedRectangle(cornerRadius:15).stroke(theme.accent.opacity(ribbon.value>0 ? 0.22:0.06),lineWidth:1))
    }
}

struct RibbonIcon:View {
    let name:String
    var body:some View {
        if let image=NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/Ribbons/\(name).png")) {
            Image(nsImage:image).resizable().interpolation(.none).scaledToFit()
        } else {Image(systemName:"rosette").resizable().scaledToFit().foregroundStyle(.secondary)}
    }
}
