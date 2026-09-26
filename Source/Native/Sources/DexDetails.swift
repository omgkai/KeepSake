import SwiftUI

struct DexDetailData:Codable {
    let species:Int, name:String, form:Int, forms:[Choice], seen:Int, obtained:Int, caught:Int
    let displayForm:Int, female:Bool, shiny:Bool, alpha:Bool, multipleGenders:Bool
    let hasMax:Bool, minHeight:Double, maxHeight:Double, minWeight:Double, maxWeight:Double
}

struct DexDetailsView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @Environment(\.gameTheme) private var theme
    private let variants=["Male", "Female", "Alpha male", "Alpha female", "Shiny male", "Shiny female", "Shiny Alpha male", "Shiny Alpha female"]
    private var sizeDrafts:Bool {model.drafts.keys.contains{$0.hasPrefix("dexsize|")}}
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {
                VStack(alignment:.leading,spacing:4) {Text("Forms & Appearance").font(.title2.bold());Text(model.dexDetails?.name ?? "Hisui Pokédex").foregroundStyle(theme.accent)}
                Spacer()
                Button("Done"){dismiss()}.keyboardShortcut(.cancelAction).disabled(sizeDrafts)
            }
            if let data=model.dexDetails {
                ScrollView {
                    VStack(alignment:.leading,spacing:20) {
                        GroupBox("Form records") {
                            VStack(alignment:.leading,spacing:12) {
                                Picker("Form",selection:Binding(get:{data.form},set:{v in Task{await model.loadDexDetails(form:v)}})) {ForEach(data.forms){Text($0.label).tag(Int($0.value)!)}}.disabled(sizeDrafts)
                                HStack {Text("VARIANT");Spacer();Text("SEEN").frame(width:65);Text("OBTAINED").frame(width:85);Text("CAUGHT").frame(width:65)}.font(.caption).foregroundStyle(.secondary)
                                ForEach(0..<8,id:\.self) {bit in
                                    HStack {
                                        Text(variants[bit]).foregroundStyle(bit % 2 == 0 ? Color.blue : .pink);Spacer()
                                        flag(data,kind:"seen",value:data.seen,bit:bit).frame(width:65)
                                        flag(data,kind:"obtained",value:data.obtained,bit:bit).frame(width:85)
                                        flag(data,kind:"caught",value:data.caught,bit:bit).frame(width:65)
                                    }
                                }
                                Text("Seen and caught refer to wild encounters. Obtained also includes other ways of receiving a Pokémon. These are Pokédex records; your Pokémon stay as they are.").font(.caption).foregroundStyle(.secondary)
                            }.padding(8)
                        }
                        GroupBox("Displayed in the Pokédex") {
                            HStack {
                                Picker("Form",selection:Binding(get:{data.displayForm},set:{v in display(data,["displayForm":v])})) {ForEach(data.forms){Text($0.label).tag(Int($0.value)!)}
                                    if !data.forms.contains(where:{$0.value==String(data.displayForm)}) {Text("Stored form \(data.displayForm)").tag(data.displayForm)}
                                }
                                if data.multipleGenders {Toggle("Female",isOn:Binding(get:{data.female},set:{display(data,["female":$0])})).toggleStyle(.checkbox).foregroundStyle(.pink)}
                                Toggle("Shiny",isOn:Binding(get:{data.shiny},set:{display(data,["shiny":$0])})).toggleStyle(.checkbox)
                                Toggle("Alpha",isOn:Binding(get:{data.alpha},set:{display(data,["alpha":$0])})).toggleStyle(.checkbox)
                            }.padding(8)
                        }
                        GroupBox("Recorded size range") {
                            VStack(alignment:.leading,spacing:12) {
                                Toggle("Separate maximum records",isOn:Binding(get:{model.drafts["dexsize|hasMax"].map{$0=="true"} ?? data.hasMax},set:{draft("hasMax",String($0),original:String(data.hasMax))})).toggleStyle(.checkbox)
                                HStack {
                                    size("Minimum height (m)",key:"minHeight",value:data.minHeight)
                                    size("Maximum height (m)",key:"maxHeight",value:data.maxHeight)
                                }
                                HStack {
                                    size("Minimum weight (kg)",key:"minWeight",value:data.minWeight)
                                    size("Maximum weight (kg)",key:"maxWeight",value:data.maxWeight)
                                }
                                Text("Without separate maximum records, the maximum is stored as the minimum.").font(.caption).foregroundStyle(.secondary)
                                HStack {
                                    Spacer()
                                    Button("Discard Size Edits"){model.drafts=model.drafts.filter{!$0.key.hasPrefix("dexsize|")}}.disabled(!sizeDrafts)
                                    Button("Save Sizes"){Task{await model.saveDexSizes(data)}}.buttonStyle(.borderedProminent).disabled(!sizeDrafts)
                                }
                            }.padding(8)
                        }
                    }
                }
                Text("Changes have Undo support. Export Copy writes them to a new save file.").font(.caption).foregroundStyle(.secondary)
            } else {ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity)}
        }.padding(24).frame(width:690,height:700).disabled(model.busy)
            .interactiveDismissDisabled(sizeDrafts)
            .task{await model.loadDexDetails()}
    }
    private func flag(_ data:DexDetailData,kind:String,value:Int,bit:Int)->some View {
        Toggle("\(variants[bit]) \(kind)",isOn:Binding(get:{value & (1 << bit) != 0},set:{v in
            let next=v ? value | (1 << bit) : value & ~(1 << bit)
            var request:[String:Any]=["mode":"flags","seen":data.seen,"obtained":data.obtained,"caught":data.caught];request[kind]=next
            Task{await model.changeDexDetails(request)}
        })).labelsHidden().toggleStyle(.checkbox).disabled(sizeDrafts)
    }
    private func display(_ data:DexDetailData,_ values:[String:Any]) {
        let request:[String:Any]=["mode":"display","displayForm":data.displayForm,"female":data.female,"shiny":data.shiny,"alpha":data.alpha]
        Task{await model.changeDexDetails(request.merging(values){_,new in new})}
    }
    private func draft(_ key:String,_ value:String,original:String) {
        if value==original {model.drafts.removeValue(forKey:"dexsize|"+key)} else {model.drafts["dexsize|"+key]=value}
    }
    private func size(_ title:String,key:String,value:Double)->some View {
        VStack(alignment:.leading,spacing:4) {
            Text(title).font(.caption).foregroundStyle(.secondary)
            TextField(title,text:Binding(get:{model.drafts["dexsize|"+key] ?? String(value)},set:{draft(key,$0,original:String(value))})).textFieldStyle(.roundedBorder)
        }
    }
}

extension EditorModel {
    private var modelSectionNeedsDex:Bool {section=="Pokédex"}
    func loadDexDetails(form:Int?=nil) async {
        guard !busy,state.canResearch else{return};busy=true;defer{busy=false}
        do {
            var request:[String:Any]=["op":"dexDetails","species":researchSpecies]
            if let form{request["form"]=form}
            dexDetails=try await bridge.send(request,as:DexDetailData.self)
        }catch{self.error=error.localizedDescription}
    }
    func changeDexDetails(_ request:[String:Any]) async {
        guard !busy,let data=dexDetails else{return};busy=true;defer{busy=false}
        do {
            state=try await bridge.send(request.merging(["op":"dexDetailsSet","species":data.species,"form":data.form]){_,new in new},as:EditorState.self)
            dexDetails=try await bridge.send(["op":"dexDetails","species":data.species,"form":data.form],as:DexDetailData.self)
            research=try await bridge.send(["op":"research","species":researchSpecies],as:ResearchData.self)
            if modelSectionNeedsDex {dex=try await bridge.send(["op":"dex"],as:DexData.self).entries}
            if request["mode"] as? String == "size" {drafts=drafts.filter{!$0.key.hasPrefix("dexsize|")}}
            status="Pokédex details updated — export a copy to save"
        }catch{self.error=error.localizedDescription}
    }
    func saveDexSizes(_ data:DexDetailData) async {
        var request:[String:Any]=["mode":"size","hasMax":drafts["dexsize|hasMax"].map{$0=="true"} ?? data.hasMax]
        for (key,original) in [("minHeight",data.minHeight),("maxHeight",data.maxHeight),("minWeight",data.minWeight),("maxWeight",data.maxWeight)] {
            guard let value=Double(drafts["dexsize|"+key] ?? String(original)),value.isFinite,value>=0 else{error="Size records must be finite, nonnegative numbers.";return}
            request[key]=value
        }
        await changeDexDetails(request)
    }
}
