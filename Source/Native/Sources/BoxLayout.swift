import SwiftUI

struct BoxLayoutEntry:Codable,Identifiable {let id:Int, name:String, wallpaper:Int, sprite:String}
struct WallpaperEntry:Codable,Identifiable {let id:Int, name:String, sprite:String}
struct BoxLayoutData:Codable {let canName:Bool, canUnlock:Bool, unlocked:Int, flags:[Int], entries:[BoxLayoutEntry], wallpapers:[WallpaperEntry]}
struct SlotTransferRequest:Identifiable {let id=UUID();let payload:String, name:String, box:Int, slot:Int}

struct BoxLayoutView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @Environment(\.gameTheme) private var theme
    @State private var selected=0
    @State private var destination=0
    private var hasDraft:Bool {model.drafts.keys.contains{$0.hasPrefix("boxname|")}}
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {Text("Box Layout").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction).disabled(hasDraft)}
            if let data=model.state.boxLayout,let box=data.entries.first(where:{$0.id==selected}) {
                Picker("Box",selection:$selected){ForEach(data.entries){Text("\($0.id+1) · \($0.name)").tag($0.id)}}.disabled(hasDraft)
                if data.canName {
                    HStack {
                        TextField("Box name",text:Binding(get:{model.drafts["boxname|\(selected)"] ?? box.name},set:{v in if v==box.name{model.drafts.removeValue(forKey:"boxname|\(selected)")}else{model.drafts["boxname|\(selected)"]=v}})).textFieldStyle(.roundedBorder)
                        Button("Save Name"){let id=selected;let name=model.drafts["boxname|\(id)"] ?? box.name;Task{await model.command(["op":"boxLayoutSet","mode":"name","box":id,"name":name],status:"Box name updated");if model.error==nil{model.drafts.removeValue(forKey:"boxname|\(id)")}}}.disabled(!hasDraft)
                        if hasDraft {Button("Discard"){model.drafts=model.drafts.filter{!$0.key.hasPrefix("boxname|")}}}
                    }
                }
                if data.canUnlock || !data.flags.isEmpty {
                    DisclosureGroup("Box availability & advanced flags") {
                        VStack(alignment:.leading,spacing:10) {
                            if data.canUnlock {
                                Picker("Unlocked boxes",selection:Binding(get:{data.unlocked},set:{v in Task{await model.command(["op":"boxLayoutSet","mode":"unlocked","count":v],status:"Unlocked box count updated")}})) {ForEach(0...model.state.boxCount,id:\.self){Text(String($0)).tag($0)}}
                                Text("Controls how many boxes the game makes available. Stored Pokémon are kept when the count is lowered.").font(.caption).foregroundStyle(.secondary)
                            }
                            if !data.flags.isEmpty {
                                DisclosureGroup("Raw box flags") {
                                    Text("Game-specific bytes used by PKHeX’s box settings. Only change a byte when you know its meaning for this game.").font(.caption).foregroundStyle(.secondary)
                                    ForEach(data.flags.indices,id:\.self){i in
                                        HStack {Text("Flag byte \(i+1)");Spacer();Picker("Flag byte \(i+1)",selection:Binding(get:{data.flags[i]},set:{v in Task{await model.command(["op":"boxLayoutSet","mode":"flag","index":i,"value":v],status:"Box flag updated")}})){ForEach(0...255,id:\.self){Text(String(format:"%02X",$0)).tag($0)}}.labelsHidden().frame(width:90)}
                                    }
                                }
                            }
                        }.padding(.top,8)
                    }.disabled(hasDraft)
                }
                GroupBox("Box order") {
                    HStack {
                        Picker("Position",selection:$destination){ForEach(data.entries){Text("\($0.id+1) · \($0.name)").tag($0.id)}}
                        Button("Move Here"){reorder("move")}.disabled(selected==destination)
                        Button("Swap Boxes"){reorder("swap")}.disabled(selected==destination)
                    }.padding(8).disabled(hasDraft || model.state.pending)
                }
                Text("Move Here shifts the boxes between the two positions. Swap Boxes exchanges only the selected pair. Names and wallpapers travel with the Pokémon.").font(.caption).foregroundStyle(.secondary)
                if data.wallpapers.isEmpty {
                    Spacer()
                    WallpaperImage(name:box.sprite).frame(height:170).clipShape(RoundedRectangle(cornerRadius:12))
                    Text("This game has no editable box wallpaper. Its standard backdrop is shown when available.").font(.callout).foregroundStyle(.secondary)
                    Spacer()
                } else {
                    HStack {Text("Wallpaper").font(.headline);Spacer();Text(data.wallpapers.first{$0.id==box.wallpaper}?.name ?? "Stored wallpaper \(box.wallpaper)").foregroundStyle(.secondary)}
                    ScrollView {
                        LazyVGrid(columns:[GridItem(.adaptive(minimum:125))],spacing:14) {
                            ForEach(data.wallpapers){wallpaper in
                                Button {Task{await model.command(["op":"boxLayoutSet","mode":"wallpaper","box":selected,"wallpaper":wallpaper.id],status:"Box wallpaper updated")}} label:{
                                    VStack(spacing:7){WallpaperImage(name:wallpaper.sprite).frame(height:82).clipShape(RoundedRectangle(cornerRadius:8));Text(wallpaper.name).font(.caption).lineLimit(1)}
                                        .padding(7).background(wallpaper.id==box.wallpaper ? theme.accent.opacity(0.15) : .clear,in:RoundedRectangle(cornerRadius:10))
                                        .overlay(RoundedRectangle(cornerRadius:10).stroke(wallpaper.id==box.wallpaper ? theme.accent : .clear,lineWidth:2))
                                }.buttonStyle(.plain).accessibilityLabel("Wallpaper \(wallpaper.id+1): \(wallpaper.name)")
                            }
                        }.padding(3)
                    }.disabled(hasDraft)
                }
                Text("Undo restores layout edits. Export Copy writes them to your save.").font(.caption).foregroundStyle(.secondary)
            }
        }.padding(24).frame(width:680,height:680).disabled(model.busy).interactiveDismissDisabled(hasDraft)
            .task {selected=model.state.box;destination=(selected+1) % max(1,model.state.boxCount)}
    }
    private func reorder(_ mode:String) {
        let source=selected,target=destination
        Task{await model.command(["op":"boxLayoutSet","mode":mode,"box":source,"destination":target],status:"Boxes reordered — export a copy to save");if model.error==nil{selected=target;destination=source}}
    }
}

struct SlotTransferView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let source:SlotTransferRequest
    @State private var destinationBox=0
    @State private var destinationSlot=0
    @State private var destinationParty=false
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            Text("Move or Swap Pokémon").font(.title2.bold())
            Text(source.box<0 ? "\(source.name) · Party, slot \(source.slot+1)":"\(source.name) · Box \(source.box+1), slot \(source.slot+1)").foregroundStyle(.secondary)
            Toggle("Move to party",isOn:$destinationParty).toggleStyle(.checkbox).disabled(model.state.partySlots.isEmpty)
            if !destinationParty {Picker("Destination box",selection:$destinationBox){ForEach(0..<model.state.boxCount,id:\.self){Text("\($0+1) · \(model.state.boxNames[$0])").tag($0)}}}
            Picker("Destination slot",selection:$destinationSlot){ForEach(0..<(destinationParty ? model.state.partySlots.count:model.state.slots.count),id:\.self){Text("Slot \($0+1)").tag($0)}}
            Text("An empty destination moves the Pokémon. An occupied destination swaps both Pokémon. Undo restores the original slots.").font(.callout).foregroundStyle(.secondary)
            HStack {Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction);Spacer();Button("Move / Swap"){if model.dropBoxSlot(source.payload,box:destinationBox,slot:destinationSlot,party:destinationParty){dismiss()}}.buttonStyle(.borderedProminent).disabled((destinationParty ? source.box<0:destinationBox==source.box) && destinationSlot==source.slot)}
        }.padding(24).frame(width:470).disabled(model.busy).task{destinationBox=max(0,source.box);destinationSlot=source.box<0 ? 0:source.slot}.onChange(of:destinationParty){_,_ in destinationSlot=0}
    }
}

struct WallpaperImage:View {
    let name:String
    var body:some View {
        if !name.isEmpty,let image=NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/Wallpapers/\(name).png")) {
            Image(nsImage:image).resizable().interpolation(.none).scaledToFit()
        } else {Rectangle().fill(Color(nsColor:.controlBackgroundColor))}
    }
}

extension EditorModel {
    func dropPokemonFile(_ url:URL,slot:Int)->Bool {
        guard !busy else{return false}
        guard !state.pending,!fieldDrafts else{error="Set or discard your Pokémon edits before dropping a file into a slot.";return true}
        do {let file=try FileDropSelection.file(from:[url]);let request:[String:Any]=["op":"slotFileImport","path":file.path,"box":state.box,"slot":slot,"revision":state.revision,"session":state.dragSession];Task{await command(request,status:"Pokémon file placed — review legality; Undo restores the previous slot")};return true}catch{self.error=error.localizedDescription;return true}
    }
    func boxDragPayload(_ slot:Slot)->String {
        guard !slot.empty,!state.pending,!fieldDrafts,!busy else{return ""}
        return "pkhex-slot:\(state.dragSession):\(state.revision):\(slot.party ? -1 : state.box):\(slot.index)"
    }
    func dropBoxSlot(_ payload:String,box:Int,slot:Int,party:Bool=false)->Bool {
        let parts=payload.split(separator:":",omittingEmptySubsequences:false)
        guard !busy,!state.pending,!fieldDrafts,parts.count==5,parts[0]=="pkhex-slot",parts[1]==state.dragSession,let revision=Int(parts[2]),let sourceBox=Int(parts[3]),let sourceSlot=Int(parts[4]) else{return false}
        Task{await command(["op":"slotSwap","session":String(parts[1]),"revision":revision,"fromBox":max(0,sourceBox),"fromSlot":sourceSlot,"fromParty":sourceBox == -1,"toBox":box,"toSlot":slot,"toParty":party],status:"Pokémon moved / swapped — export a copy to save")}
        return true
    }
}
