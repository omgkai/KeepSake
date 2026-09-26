import SwiftUI

struct NameBytesPage:Codable {
    let revision:Int
    let entityKey:String
    let fields:[Choice]
    let field,text,hex:String
    let capacity,maxLength:Int
    let characters:[Choice]
}
struct NameBytesEditor:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    var fameID:String?=nil
    var trainer=false
    var baseID:String?=nil
    private func operation(_ op:String)->String { baseID != nil ? op.replacingOccurrences(of:"nameBytes",with:"baseName") : trainer ? op.replacingOccurrences(of:"nameBytes",with:"trainerName") : fameID==nil ? op : op.replacingOccurrences(of:"nameBytes",with:"fameNameBytes") }
    @State private var page:NameBytesPage?
    @State private var originalHex=""
    @State private var text=""
    @State private var hex=""
    @State private var field="Nickname"
    @State private var error=""
    @State private var loading=false
    @State private var species="25"
    @State private var language="2"
    @State private var generation=9
    @State private var speciesChoices:[Choice]=[]
    private var changed:Bool { compactHex != originalHex || text != page?.text }
    private var compactHex:String {hex.filter{!$0.isWhitespace}.uppercased()}
    private var previewed:Bool {compactHex==page?.hex && text==page?.text}
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {Label("Names & text bytes",systemImage:"character.cursor.ibeam").font(.title2.bold());Spacer();Button("Cancel"){dismiss()};Button("Save Changes"){save()}.buttonStyle(.borderedProminent).disabled(!changed || !previewed || loading || model.busy)}
            Text("Edit a name and inspect its exact stored bytes. Hidden bytes after the text terminator are kept unless you clear or replace them.").font(.callout).foregroundStyle(.secondary)
            if let page {
                Picker("Name field",selection:$field){ForEach(page.fields){Text($0.label).tag($0.value)}}.pickerStyle(.segmented).disabled(changed || loading).onChange(of:field){_,_ in Task{await load()}}
                ScrollView {
                    VStack(alignment:.leading,spacing:16) {
                        VStack(alignment:.leading,spacing:10) {
                            Text("NAME").font(.caption.bold()).foregroundStyle(.secondary)
                            HStack {TextField("Stored name",text:$text).textFieldStyle(.roundedBorder);Button("Encode Text"){preview("text")}}
                            Text("Up to \(page.maxLength) characters · Preview: \(page.text.isEmpty ? "Empty name":page.text)").font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
                            if !page.characters.isEmpty {Menu("Insert a special character"){ForEach(page.characters){c in Button(c.label){text += c.value}}}}
                        }.padding(16).background(.tint.opacity(0.06),in:RoundedRectangle(cornerRadius:16))
                        VStack(alignment:.leading,spacing:10) {
                            HStack {Text("STORED BYTES").font(.caption.bold());Spacer();Text("\(page.capacity) bytes").font(.caption).foregroundStyle(.secondary)}
                            TextEditor(text:$hex).font(.system(.body,design:.monospaced)).frame(height:90).padding(6).background(Color(nsColor:.textBackgroundColor),in:RoundedRectangle(cornerRadius:10))
                            HStack {Button("Decode Bytes"){preview("hex")};Button("Clear Hidden Bytes"){preview("clear")}.disabled(!previewed);Spacer();Button("Reset Draft"){Task{await load()}}}
                        }
                        DisclosureGroup("Apply a species-name layer") {
                            VStack(alignment:.leading,spacing:12) {
                                Text("Recreate leftover species-name bytes without changing the visible name. The selected generation and language choose the name; this Pokémon’s format determines its encoding.").font(.caption).foregroundStyle(.secondary)
                                CatalogChoiceButton(title:"Species",options:speciesChoices,value:$species)
                                Picker("Language",selection:$language){Text("Japanese").tag("1");Text("English").tag("2");Text("French").tag("3");Text("Italian").tag("4");Text("German").tag("5");Text("Spanish").tag("7");Text("Korean").tag("8");Text("Chinese (Simplified)").tag("9");Text("Chinese (Traditional)").tag("10")}
                                Stepper("Name generation · \(generation)",value:$generation,in:1...9)
                                Button("Preview Layer"){preview("layer")}.disabled(!previewed)
                            }.padding(.top,10)
                        }
                    }.padding(2)
                }.disabled(loading || model.busy)
                if !error.isEmpty {Text(error).font(.callout).foregroundStyle(.red)}
                HStack {if loading {ProgressView().controlSize(.small)};Text(previewed ? (trainer || baseID != nil ? "Save updates the stored record. Export Copy keeps the changes; Undo remains available." : fameID==nil ? "Save, then Set to Slot to keep this in your game save. The nickname flag is unchanged.":"Save updates this Hall of Fame record. Export Copy keeps the changes; Undo remains available.") : "Encode Text or Decode Bytes to review your draft before saving.").font(.caption).foregroundStyle(.secondary)}
            } else if !error.isEmpty {Text(error).foregroundStyle(.red);Button("Try Again"){Task{await load()}};Spacer()}
            else {ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity)}
        }.padding(24).frame(width:740,height:650).task {if trainer {field="OT"};if baseID != nil{field="OriginalTrainerName"};await load();speciesChoices=(try? await model.bridge.send(["op":"lookup","kind":"journalSpecies"],as:[Choice].self)) ?? []}
    }
    private func adopt(_ value:NameBytesPage) {page=value;text=value.text;hex=stride(from:0,to:value.hex.count,by:2).map{ i in let a=value.hex.index(value.hex.startIndex,offsetBy:i);return String(value.hex[a..<value.hex.index(a,offsetBy:2)])}.joined(separator:" ")}
    private func load() async {loading=true;defer{loading=false};error="";do{let value=try await model.bridge.send(["op":operation("nameBytesInfo"),"id":baseID ?? fameID ?? "","field":field],as:NameBytesPage.self);adopt(value);originalHex=value.hex}catch{self.error=error.localizedDescription}}
    private func request(_ op:String)->[String:Any] { ["op":operation(op),"id":baseID ?? fameID ?? "","revision":page?.revision ?? -1,"entityKey":page?.entityKey ?? "","field":field,"hex":hex] }
    private func preview(_ mode:String) {var r=request("nameBytesInfo");r["mode"]=mode;r["text"]=text;r["species"]=Int(species) ?? 0;r["language"]=Int(language) ?? 2;r["generation"]=generation;loading=true;error="";Task{defer{loading=false};do{adopt(try await model.bridge.send(r,as:NameBytesPage.self))}catch{self.error=error.localizedDescription}}}
    private func save(){let r=request("nameBytesSet");Task{await model.command(r,status:trainer || baseID != nil ? "Stored name bytes updated" : fameID==nil ? "Name bytes updated — Set to Slot to keep them":"Hall of Fame name bytes updated — export a copy to keep them");if model.error==nil{dismiss()}}}
}
