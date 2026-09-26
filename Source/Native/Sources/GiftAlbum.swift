import SwiftUI

struct AlbumEntry:Codable,Identifiable {
    let id:Int, card:Int, title:String, type:String, empty:Bool, used:Bool, canUse:Bool, `extension`:String, name:String, special:Bool
    var location:String {special ? "Lock Capsule" : "Slot \(id+1)"}
}
struct AlbumData:Codable {let entries:[AlbumEntry], received:[Int], flagMax:Int}

struct GiftAlbumView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    @State private var selection:Int?=0
    @State private var receivedSelection:Int?
    @State private var cardID=""
    @State private var dropTarget=false
    @State private var droppedCard:URL?
    @State private var dropRevision=0
    private var chosen:AlbumEntry? {model.giftAlbum?.entries.first{$0.id==selection}}
    var body:some View {
        if !model.state.canGiftAlbum {
            ContentUnavailableView("Gift Album Unavailable",systemImage:"gift",description:Text(model.state.hasSave ? "This game does not store an editable Mystery Gift album. The Mystery Gifts library still offers event cards and Pokémon preparation." : "Open a supported generation 4–7 save to edit its stored gift cards."))
        } else if let data=model.giftAlbum {
            VStack(alignment:.leading,spacing:16) {
                HStack {
                    VStack(alignment:.leading,spacing:5) {Text("Mystery Gift album").font(.title2.bold());Text("Drop a card here or choose a slot to import and edit its status.").foregroundStyle(.secondary)}
                    Spacer()
                    Image(systemName:"gift.fill").font(.largeTitle).foregroundStyle(theme.accent)
                }
                if let url=droppedCard {
                    HStack(spacing:12) {
                        Image(systemName:"doc.badge.plus").font(.title2).foregroundStyle(theme.accent)
                        VStack(alignment:.leading,spacing:3) { Text(url.lastPathComponent).font(.headline).lineLimit(1);Text("Choose an album slot, then import the card.").font(.caption).foregroundStyle(.secondary) }
                        Spacer()
                        Button("Cancel") { droppedCard=nil }
                        Button("Import to Slot") { if let chosen { Task {
                            guard model.state.revision==dropRevision else { model.error="The workspace changed. Drop the card again.";droppedCard=nil;return }
                            await model.editGiftAlbum(["mode":"import","index":chosen.id,"path":url.path,"revision":dropRevision])
                            if model.error==nil { droppedCard=nil }
                        } } }.buttonStyle(.borderedProminent).disabled(chosen==nil || model.busy || model.fieldDrafts)
                    }.padding(14).background(theme.accent.opacity(0.08),in:RoundedRectangle(cornerRadius:14))
                }
                if let gift=model.pendingAlbumGift {
                    HStack {
                        VStack(alignment:.leading,spacing:3){Text("From the event library").font(.caption).foregroundStyle(.secondary);Text(gift.title).lineLimit(1)}
                        Spacer()
                        Button("Cancel"){model.pendingAlbumGift=nil}
                        Button("Set Card to Slot"){if let chosen{Task{await model.editGiftAlbum(["mode":"library","id":gift.id,"index":chosen.id])}}}.buttonStyle(.borderedProminent).disabled(chosen==nil)
                    }.padding(12).background(theme.accent.opacity(0.08),in:RoundedRectangle(cornerRadius:12))
                }
                HStack(alignment:.top,spacing:20) {
                    VStack(alignment:.leading,spacing:12) {
                        Table(data.entries,selection:$selection) {
                            TableColumn("Slot",value:\.location).width(min:70,ideal:90)
                            TableColumn("Card"){Text($0.empty ? "—" : String($0.card))}.width(45)
                            TableColumn("Title",value:\.title)
                            TableColumn("Format",value:\.type).width(50)
                            TableColumn("Received"){g in Text(g.empty || !g.canUse ? "—" : g.used ? "Yes" : "No")}.width(65)
                        }
                        HStack {
                            Button("Import Card…"){if let chosen{model.importAlbumGift(index:chosen.id)}}.disabled(chosen==nil)
                            Button("Export Card…"){if let chosen{model.exportAlbumGift(chosen)}}.disabled(chosen?.empty != false)
                            Spacer()
                            Button("Delete Card"){if let chosen{Task{await model.editGiftAlbum(["mode":"delete","index":chosen.id])}}}.disabled(chosen?.empty != false)
                        }
                        HStack {
                            if let chosen, !chosen.empty, chosen.canUse {
                                Toggle("Gift already received",isOn:Binding(get:{chosen.used},set:{v in Task{await model.editGiftAlbum(["mode":"used","index":chosen.id,"value":v])}})).toggleStyle(.checkbox)
                            }
                            Spacer()
                            Menu("Mark All") {
                                Button("Received"){markAll(true)}
                                Button("Not Received"){markAll(false)}
                            }.disabled(!data.entries.contains{!$0.empty && $0.canUse})
                        }
                    }
                    if data.flagMax>0 {
                        VStack(alignment:.leading,spacing:10) {
                            Text("Received card IDs").font(.headline)
                            Text("This history is separate from the cards in your album.").font(.caption).foregroundStyle(.secondary)
                            List(data.received,id:\.self,selection:$receivedSelection){id in Text(String(format:"%04d",id)).tag(id)}
                            HStack {
                                TextField("Card ID",text:$cardID).textFieldStyle(.roundedBorder).accessibilityLabel("Received card ID")
                                Button("Add"){if let id=Int(cardID){Task{await model.editGiftAlbum(["mode":"flag","card":id,"value":true])}}}.disabled(Int(cardID)==nil)
                            }
                            Button("Remove Selected ID"){if let id=receivedSelection{Task{await model.editGiftAlbum(["mode":"flag","card":id,"value":false])}}}.disabled(receivedSelection==nil)
                            Text("IDs 1–\(data.flagMax-1)").font(.caption).foregroundStyle(.tertiary)
                        }.frame(width:185)
                    }
                }
                Text("Cards fill earlier empty slots of the same format. Deletion packs that group and keeps received-card history. Undo restores edits; Export Copy writes your save.").font(.caption).foregroundStyle(.secondary)
            }.padding(28)
                .overlay(RoundedRectangle(cornerRadius:18).stroke(theme.accent.opacity(dropTarget ? 0.7 : 0),lineWidth:2).padding(8).allowsHitTesting(false))
                .dropDestination(for:FileDropItem.self) { items,_ in
                    guard !model.busy,!model.fieldDrafts else {return false}
                    do { droppedCard=try FileDropSelection.file(from:items.map(\.url));dropRevision=model.state.revision;return true }
                    catch {model.error=error.localizedDescription;return false}
                } isTargeted: { dropTarget=$0 }
        } else {ProgressView().task{await model.loadGiftAlbum()}}
    }
    private func markAll(_ value:Bool){Task{await model.editGiftAlbum(["mode":"usedAll","value":value])}}
}

extension EditorModel {
    func loadGiftAlbum() async {
        guard !busy,state.canGiftAlbum else{return};busy=true;defer{busy=false}
        do{giftAlbum=try await bridge.send(["op":"giftAlbum"],as:AlbumData.self)}catch{self.error=error.localizedDescription}
    }
    func editGiftAlbum(_ request:[String:Any]) async {
        guard !busy else{return};busy=true;defer{busy=false}
        do {
            state=try await bridge.send(request.merging(["op":"giftAlbumSet"]){_,new in new},as:EditorState.self)
            giftAlbum=try await bridge.send(["op":"giftAlbum"],as:AlbumData.self)
            if request["mode"] as? String == "library" {pendingAlbumGift=nil}
            status="Gift album updated — export a copy to save"
        }catch{self.error=error.localizedDescription}
    }
    func importAlbumGift(index:Int) {
        guard !busy else{return}
        let panel=NSOpenPanel();panel.title="Import Mystery Gift card";panel.canChooseDirectories=false
        guard panel.runModal()==NSApplication.ModalResponse.OK,let url=panel.url else{return}
        Task{await editGiftAlbum(["mode":"import","index":index,"path":url.path])}
    }
    func exportAlbumGift(_ gift:AlbumEntry) {
        guard !busy else{return}
        let panel=NSSavePanel();panel.title="Export album card";panel.nameFieldStringValue="Album-\(gift.id+1)-Card-\(gift.card).\(gift.extension)"
        guard panel.runModal()==NSApplication.ModalResponse.OK,let url=panel.url else{return}
        Task {
            busy=true;defer{busy=false}
            do{let _:ExportResult=try await bridge.send(["op":"giftAlbumExport","index":gift.id,"path":url.path],as:ExportResult.self);status="Exported album card"}catch{self.error=error.localizedDescription}
        }
    }
}
