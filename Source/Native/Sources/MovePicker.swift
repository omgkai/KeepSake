import SwiftUI

struct MoveOption:Codable,Identifiable {
    let id:Int, name:String, type:Int, typeName:String, pp:Int, status:String, source:String
}
struct MoveChoiceData:Codable {let revision:Int, species:String, entries:[MoveOption]}
struct MovePresentation:Identifiable {let id=UUID();let data:MoveChoiceData;let selected:Int}

struct MoveTypeBadge:View {
    let type:Int, name:String
    var compact=false
    static let colors=["929DA3","CE4069","8FA9DE","AB6AC8","D97845","C6B88C","91C12F","5269AD","5A8EA2","FF9D55","5090D6","63BC5A","C9A919","FA7179","73CEC0","5C69B5","695C73","DC8FBE"]
    private var color:Color {Color(hex:Self.colors.indices.contains(type) ? Self.colors[type] : "929DA3")}
    var body:some View {
        HStack(spacing:4) {
            if let icon=MoveTypeImages.load(type) {Image(nsImage:icon).resizable().interpolation(.high).frame(width:18,height:18)}
            if !compact {Text(name.uppercased()).font(.system(size:9,weight:.bold)).foregroundStyle(Color.primary)}
        }.padding(.horizontal,compact ? 2 : 6).padding(.vertical,3)
            .background(color.opacity(0.23),in:RoundedRectangle(cornerRadius:5))
            .overlay(RoundedRectangle(cornerRadius:5).strokeBorder(color.opacity(0.5)))
            .accessibilityLabel(name+" type")
    }
}
private enum MoveTypeImages {
    static var cache:[Int:NSImage]=[:]
    static func load(_ type:Int)->NSImage? {
        if let image=cache[type] {return image}
        guard let image=NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent(String(format:"Contents/Resources/MoveTypes/type_icon_%02d.png",type))) else{return nil}
        cache[type]=image;return image
    }
}
struct MovePicker:View {
    @EnvironmentObject var model:EditorModel
    let slot:Int
    var relearn=false
    @State private var presentation:MovePresentation?
    private var fieldID:String {(relearn ? "RelearnMove" : "Move")+String(slot)}
    private var check:MoveCheck? {model.state.moveChecks.first{$0.slot==slot && $0.relearn==relearn}}
    private var current:Int {Int(model.state.fields.first{$0.id==fieldID}.map{model.value($0,target:"entity")} ?? "0") ?? 0}
    private var name:String {model.catalogs["moves"]?.first{$0.value==String(current)}?.label ?? "No move"}
    var body:some View {
        Button {
            Task {
                do {let data=try await model.fetchMoveChoices();presentation=MovePresentation(data:data,selected:current)}
                catch {model.error=error.localizedDescription}
            }
        } label: {
            HStack(spacing:8) {
                if current != 0,let check,check.move==current {MoveTypeBadge(type:check.type,name:check.typeName)}
                Text(current==0 ? "No move" : name).lineLimit(1).frame(maxWidth:.infinity,alignment:.leading)
                Image(systemName:"chevron.up.chevron.down").font(.system(size:9,weight:.semibold)).foregroundStyle(.secondary)
            }.padding(.horizontal,8).padding(.vertical,5).background(.quaternary.opacity(0.5),in:RoundedRectangle(cornerRadius:7))
                .overlay(alignment:.leading) {
                    if !model.fieldDrafts,let check,check.status=="illegal" {RoundedRectangle(cornerRadius:2).fill(.red).frame(width:3).padding(.vertical,5)}
                }
        }.buttonStyle(.plain).accessibilityLabel((relearn ? "Relearn move " : "Move ")+String(slot)+": "+name)
        .popover(item:$presentation,arrowEdge:.bottom) { presented in
            MoveChoicePopover(data:presented.data,selected:presented.selected) { option in
                presentation=nil
                guard model.state.revision==presented.data.revision else {model.error="This Pokémon changed while the list was open. Open the move list again to refresh its checks.";return}
                Task {await model.editPokemon(fieldID,value:String(option.id))}
            }
        }
    }
}
struct MoveChoicePopover:View {
    let data:MoveChoiceData, selected:Int, choose:(MoveOption)->Void
    @State private var search=""
    @State private var learnableOnly=false
    @State private var typeFilter = -1
    private var filtered:[MoveOption] {data.entries.filter {
        (!learnableOnly || $0.status=="learnable" || $0.id==0) &&
        (typeFilter<0 || $0.type==typeFilter && $0.id != 0) &&
        (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || String($0.id)==search)
    }}
    var body:some View {
        VStack(alignment:.leading,spacing:10) {
            Text("Moves for "+data.species).font(.headline)
            TextField("Search move name or number",text:$search).textFieldStyle(.roundedBorder)
                .onSubmit {if filtered.count==1 {choose(filtered[0])}}
            HStack {
                Toggle("Learnable only",isOn:$learnableOnly).toggleStyle(.checkbox)
                Spacer()
                Picker("Type",selection:$typeFilter) {
                    Text("All types").tag(-1)
                    ForEach(Array(Set(data.entries.filter{$0.id != 0}.map(\.type))).sorted(),id:\.self) { type in
                        Text(data.entries.first{$0.type==type}?.typeName ?? "Unknown").tag(type)
                    }
                }.frame(width:180)
            }.font(.caption)
            ScrollView {
                LazyVStack(spacing:4) {
                    ForEach(filtered) { move in
                        Button {choose(move)} label: {
                            HStack(spacing:8) {
                                if move.id != 0 {MoveTypeBadge(type:move.type,name:move.typeName,compact:true)}
                                else {Image(systemName:"minus.circle").frame(width:26)}
                                VStack(alignment:.leading,spacing:3) {
                                    Text(move.name).font(.system(size:12,weight:.medium)).foregroundStyle(.primary)
                                    if move.id != 0 {Text("\(move.typeName) · \(move.pp) PP").font(.caption2).foregroundStyle(.secondary)}
                                }
                                Spacer(minLength:4)
                                if move.id==selected {Image(systemName:"checkmark").foregroundStyle(.primary)}
                                Label(statusLabel(move),systemImage:move.status=="learnable" ? "checkmark.circle.fill" : move.status=="unavailable" ? "xmark.circle" : "minus.circle")
                                    .font(.system(size:10,weight:.medium)).foregroundStyle(statusColor(move))
                            }.padding(8).frame(maxWidth:.infinity,alignment:.leading)
                                .background(statusColor(move).opacity(move.id==selected ? 0.20 : 0.08),in:RoundedRectangle(cornerRadius:7))
                                .overlay(RoundedRectangle(cornerRadius:7).strokeBorder(move.id==selected ? Color.accentColor : .clear))
                                .contentShape(Rectangle())
                        }.buttonStyle(.plain).accessibilityLabel("\(move.name), \(move.typeName), \(statusLabel(move))")
                    }
                    if filtered.isEmpty {Text("No matching moves").foregroundStyle(.secondary).padding(25)}
                }
            }
            Text("\(filtered.count) moves · Green marks PKHeX's learnable moves. The complete set is checked after selection.").font(.caption).foregroundStyle(.secondary)
        }.padding(14).frame(width:450,height:460)
    }
    private func statusLabel(_ m:MoveOption)->String {switch m.status {case "learnable":"Learnable";case "unavailable":"Not learnable";case "empty":"Clear";default:"Not evaluated"}}
    private func statusColor(_ m:MoveOption)->Color {m.status=="learnable" ? .green : m.status=="unavailable" ? .red : .secondary}
}
