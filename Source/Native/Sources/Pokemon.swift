import SwiftUI

struct PokemonWorkspace: View {
    @EnvironmentObject var journals:JournalStore
    @State private var journal:JournalSelection?
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    @State private var tab = "Main"
    @State private var report = false
    @State private var autoLegality=false
    @State private var showImport = false
    @State private var nameBytes = false
    @State private var showdownText = ""
    @State private var teamImport=false
    @State private var qrSheet=false
    @State private var summarySheet=false
    private let tabs = ["Main", "Met", "Stats", "Moves", "Trainer", "Cosmetics", "More"]
    var body: some View {
        HSplitView {
            VStack(spacing:0) {
                HStack(spacing:14) {
                    ZStack(alignment:.topTrailing) {
                        PokemonSprite(name:model.state.entitySprite,portrait:model.state.entityPortrait).frame(width:64,height:64).opacity(model.state.entityName == "Empty slot" ? 0.25 : 1)
                        if model.state.entitySprite.hasSuffix("s") {Image(systemName:"sparkles").foregroundStyle(.yellow).font(.title3).accessibilityLabel("Shiny Pokémon")}
                    }
                    VStack(alignment:.leading, spacing:5) {
                        Text(journals.entries[model.state.entityJournalKey]?.displayName.isEmpty == false ? journals.entries[model.state.entityJournalKey]!.displayName : (model.state.entityNickname.isEmpty || model.state.entityName == "Empty slot" ? model.state.entityName : model.state.entityNickname)).font(.system(size:23, weight:.bold, design:.rounded))
                        HStack(spacing:8) {
                            Text("\(model.state.entityName) · Lv. \(model.state.entityLevel)").font(.caption).foregroundStyle(.secondary)
                            GenderBadge(value:Int(model.state.fields.first{$0.id=="Gender"}?.value ?? "2") ?? 2)
                            if model.state.pending { Pill(text:"EDITING",color:.orange) }
                        }
                    }
                    Spacer()
                    Button {journal=model.state.journalSelection} label:{Image(systemName:"heart.text.square").font(.title2)}.buttonStyle(.borderless).help("Personal journal — kept only in KeepSake").disabled(model.state.entityJournalKey.isEmpty)
                    Button { report = true } label: {
                        Label(model.state.pkHaXMode ? "PKHaX · Unchecked" : model.state.legality == "valid" ? "Legal" : model.state.legality == "invalid" ? "Review" : "Report", systemImage:model.state.legality == "valid" ? "checkmark.shield.fill" : "exclamationmark.shield")
                            .foregroundStyle(model.state.legality == "valid" ? .green : .orange)
                    }.buttonStyle(.borderless).help(model.state.pkHaXMode ? "Automatic legality checks are disabled in Settings → Engine":"View full PKHeX legality report")
                }.padding(22)
                HStack(spacing:3) {
                    ForEach(tabs,id:\.self) { name in
                        Button { tab = name } label: { Text(LocalizedStringKey(name)).font(.system(size:11, weight:tab == name ? .semibold : .regular)).frame(maxWidth:.infinity,minHeight:36).contentShape(Rectangle()).background(tab == name ? theme.accent.opacity(0.12) : .clear, in:RoundedRectangle(cornerRadius:6)).foregroundStyle(tab == name ? theme.accent : .secondary) }.buttonStyle(.plain)
                    }
                }.padding(.horizontal,16).padding(.bottom,10)
                Divider()
                if tab == "More" { MorePokemonView(openTab:{tab=$0}) }
                else if tab == "Cosmetics" { CosmeticsView() }
                else { SimplePokemonEditor(tab:tab).id(tab) }
                Divider()
                actions.padding(16)
            }.frame(minWidth:500, idealWidth:560, maxWidth:.infinity).disabled(model.busy)
            if model.state.hasSave { BoxPanel().frame(minWidth:365, idealWidth:450, maxWidth:600) }
        }.sheet(isPresented:$summarySheet){PokemonSummaryView()}.sheet(isPresented:$qrSheet){PokemonQRView()}.sheet(isPresented:$teamImport){ShowdownTeamSheet()}.sheet(isPresented:$autoLegality){GenerationSheet(team:nil)}.sheet(item:$journal){JournalEditor(selection:$0)}.sheet(isPresented:$showImport) {
            VStack(alignment:.leading,spacing:16) {
                Text("Import a Showdown set").font(.title2.bold())
                Text("Applies a single set to the Pokémon in the editor. Encounter data remains from the existing Pokémon; review legality afterward.").font(.callout).foregroundStyle(.secondary)
                TextEditor(text:$showdownText).font(.system(size:12,design:.monospaced)).border(.quaternary)
                HStack { Button("Cancel") { showImport = false }.keyboardShortcut(.cancelAction); Spacer(); Button("Import a Team…"){showImport=false;teamImport=true}.disabled(!model.state.hasSave || model.state.pending); Button("Apply Set") { showImport = false; Task { await model.command(["op":"showdownImport", "text":showdownText]) } }.buttonStyle(.borderedProminent).disabled(showdownText.isEmpty) }
            }.padding(24).frame(width:550,height:460)
        }.sheet(isPresented:$report) {
            VStack(alignment:.leading, spacing:16) {
                HStack { Text("Legality report").font(.title2.bold()); Spacer(); Button("Done") { report = false }.keyboardShortcut(.cancelAction) }
                Text("Checks from PKHeX \(model.state.engineVersion)").font(.caption).foregroundStyle(.secondary)
                ScrollView { Text(model.state.report.isEmpty ? "Select a non-empty Pokémon to run legality checks." : model.state.report).font(.system(size:12, design:.monospaced)).textSelection(.enabled).frame(maxWidth:.infinity, alignment:.leading) }
            }.padding(24).frame(width:640, height:530)
        }
    }
    private var actions: some View {
        HStack(spacing:10) {
            Menu("Tools") {
                Button("Names & Text Bytes…") { nameBytes = true }
                Divider()
                Button("Toggle Shiny") { action("shiny") }
                Button("Maximize IVs") { action("maxIV") }
                Button("Clear EVs") { action("clearEV") }
                Button("Heal / Recalculate Party Stats") { action("heal") }
                Divider()
                Button("Import Pokémon…") { model.importEntity() }
                Button("Export Pokémon…") { model.exportEntity() }
                Button("Pokémon QR…"){qrSheet=true}
                Button("Summary Image…"){summarySheet=true}
                Button("Import Showdown Set…") { showImport = true }
                Button("Copy Showdown Set") { Task { await model.copyShowdown() } }
                if model.state.hasSave {
                    Divider()
                    Button("Clear Selected Slot",role:.destructive) { Task { await model.command(["op":"delete"]) } }
                }
            }.fixedSize().disabled(model.fieldDrafts).sheet(isPresented:$nameBytes){NameBytesEditor()}
            Button{autoLegality=true}label:{Label("Auto-Legality",systemImage:"wand.and.stars")}.disabled(model.fieldDrafts || model.state.entityJournalKey.isEmpty).help("Preview a legal encounter and review changes before applying")
            Spacer()
            if model.state.hasSave {
                Button { Task { await model.editPokemon(apply:true) } } label: { Label("Set to Slot",systemImage:"checkmark") }.buttonStyle(.borderedProminent).disabled(!model.state.pending && !model.fieldDrafts)
            } else {
                if model.fieldDrafts { Button("Commit Edits") { Task { await model.editPokemon() } } }
                Button("Export Pokémon…") { model.exportEntity() }.buttonStyle(.borderedProminent) }
        }
    }
    private func action(_ action: String) { Task { await model.command(["op":"entityAction", "action":action]) } }
}
struct BoxPanel: View {
    @State private var keyboardFocused=false
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    @State private var showLayout = false
    @State private var showWallpaper = false
    @State private var transfer:SlotTransferRequest?
    @State private var partyCapture:JournalPartyCapture?
    var body: some View {
        ScrollView {
            VStack(alignment:.leading, spacing:18) {
                HStack {
                    Text("BOXES").font(.caption.weight(.semibold)).tracking(1).foregroundStyle(.secondary)
                    Spacer()
                    Text("\(model.state.slots.filter { !$0.empty }.count) / \(model.state.slots.count)").font(.caption.monospaced()).foregroundStyle(.secondary)
                }
                if model.state.boxCount > 0 {
                    HStack {
                        Button { model.chooseBox(max(0,model.state.box-1)) } label: { Image(systemName:"chevron.left") }.disabled(model.state.box == 0)
                        Picker("Box",selection:Binding(get:{model.state.box},set:{model.chooseBox($0)})) {
                            ForEach(0..<model.state.boxCount,id:\.self) { i in Text("\(i+1) · \(model.state.boxNames[i])").tag(i) }
                        }.labelsHidden()
                        Button { model.chooseBox(min(model.state.boxCount-1,model.state.box+1)) } label: { Image(systemName:"chevron.right") }.disabled(model.state.box == model.state.boxCount-1)
                    }.controlSize(.small).disabled(model.busy)
                    HStack {Button("Box Layout…"){showLayout=true}.disabled(model.fieldDrafts || model.state.pending);Button("Wallpaper…"){showWallpaper=true};Spacer();Text(model.selectionTarget != nil ? "Loading Pokémon…":"Drag to move or swap").font(.caption2).foregroundStyle(.secondary)}.disabled(model.busy)
                    LazyVGrid(columns:Array(repeating:GridItem(.flexible(),spacing:6),count:6),spacing:6) {
                        ForEach(model.state.slots) { slot in slotButton(slot, selected:model.isSelected(slot)) }
                    }.padding(10).background {
                        BoxWallpaperBackground(game:model.state.game,box:model.state.box,stored:model.state.boxLayout?.entries.first(where:{$0.id==model.state.box})?.sprite ?? "")
                    }.clipShape(RoundedRectangle(cornerRadius:14))
                        .overlay(RoundedRectangle(cornerRadius:14).stroke(Color.primary.opacity(0.12),lineWidth:1))
                } else { Text("This save has no storage boxes.").foregroundStyle(.secondary) }
                if !model.state.partySlots.isEmpty {
                    HStack {
                        Text("PARTY").font(.caption.weight(.semibold)).tracking(1).foregroundStyle(.secondary)
                        Spacer()
                        Button{partyCapture=model.state.journalParty}label:{Label("Save Party as Team",systemImage:"book.closed.fill")}.font(.caption).disabled(model.state.partySlots.allSatisfy{$0.empty} || model.busy)
                    }.padding(.top,8)
                    LazyVGrid(columns:Array(repeating:GridItem(.flexible(),spacing:6),count:6),spacing:6) {
                        ForEach(model.state.partySlots) { slot in slotButton(slot, selected:model.isSelected(slot)) }
                    }
                }
                Text("Arrow keys: move selection · Option ←/→: change box").font(.caption).foregroundStyle(.tertiary).lineSpacing(3)
            }.padding(18)
        }.background(ThemeBackdrop())
        .background(BoxKeyboardCapture(active:$keyboardFocused) {delta,box,repeating in
            model.navigateSlots(delta:model.state.party && abs(delta)==6 ? delta/6:delta,changeBox:box,repeating:repeating)
        })
        .sheet(isPresented:$showLayout){BoxLayoutView()}
        .sheet(isPresented:$showWallpaper){BoxAppearanceView(game:model.state.game,box:model.state.box,stored:model.state.boxLayout?.entries.first(where:{$0.id==model.state.box})?.sprite ?? "")}
        .sheet(item:$transfer){SlotTransferView(source:$0)}
        .sheet(item:$partyCapture){JournalPartySheet(capture:$0)}
    }
    private func viewButton(_ slot:Slot)->some View {
        Button { model.select(slot) } label: { Label("View",systemImage:"eye") }
            .disabled((model.busy && model.selectionTarget == nil) || slot.empty)
            .help("View this Pokémon in the editor")
    }
    private func setToSlotButton(_ slot:Slot)->some View {
        Button("Set to Slot") { Task { await model.editPokemon(apply:true,destination:slot) } }
            .disabled(model.busy || model.state.fields.first(where:{$0.id == "Species"})?.value == "0" || model.state.fields.isEmpty || model.drafts.keys.contains{!$0.hasPrefix("entity|")})
            .help("Copy the Pokémon in the editor here, replacing the current occupant. Undo restores the previous slot.")
    }
    @ViewBuilder private func slotButton(_ slot:Slot,selected:Bool)->some View {
        let button=Button {keyboardFocused=true;model.select(slot)} label:{
            InteractiveSlotCell(slot:slot,selected:selected)
                .onDrag {model.busy ? NSItemProvider() : NSItemProvider(object:model.boxDragPayload(slot) as NSString)} preview:{PokemonDragPreview(slot:slot)}
        }
            .buttonStyle(PokemonSlotButtonStyle())
            .accessibilityLabel(slot.empty ? "Empty slot \(slot.index+1)" : "\(slot.name), level \(slot.level), slot \(slot.index+1)" + ((slot.heldItem ?? 0)>0 ? ", holding " + (slot.heldItemName ?? "an item") : ""))
        if slot.party {
            button
                .modifier(PokemonSlotDrop(slot:slot))
                .contextMenu {
                    viewButton(slot);setToSlotButton(slot)
                    Button("Move / Swap…"){transfer=SlotTransferRequest(payload:model.boxDragPayload(slot),name:slot.name,box:-1,slot:slot.index)}.disabled(model.busy || slot.empty || model.state.pending || model.fieldDrafts)
                }
                .help("Drag to reorder your party or move to a box. Drop a box Pokémon here to swap, or use the next empty party slot to add it.")
        }
        else {
            button
                .modifier(PokemonSlotDrop(slot:slot))
                .contextMenu {
                    viewButton(slot)
                    setToSlotButton(slot)
                    Divider()
                    Button("Move / Swap…") {transfer=SlotTransferRequest(payload:model.boxDragPayload(slot),name:slot.name,box:model.state.box,slot:slot.index)}
                        .disabled(model.busy || slot.empty || model.state.pending || model.fieldDrafts)
                    Button("Box Layout…"){showLayout=true}.disabled(model.state.pending || model.fieldDrafts)
                }
        }
    }
}
struct SlotCell: View {
    @EnvironmentObject var journals:JournalStore
    @Environment(\.gameTheme) private var theme
    let slot: Slot, selected: Bool
    var body: some View {
        VStack(spacing:5) {
            if slot.empty { Image(systemName:"circle.dashed").font(.system(size:18)).foregroundStyle(.secondary).frame(height:24) }
            else {
                HStack(alignment:.bottom,spacing:2) {
                    ZStack(alignment:.topTrailing) {
                        PokemonSprite(name:slot.sprite,portrait:slot.portrait).frame(width:36,height:28)
                        if journals.entries[slot.journalKey]?.favorite == true {Image(systemName:"star.fill").font(.system(size:9)).foregroundStyle(.yellow).offset(x:-30,y:-3)}
                        if slot.shiny { Image(systemName:"sparkle").font(.system(size:10)).foregroundStyle(.yellow).offset(x:6,y:-3) }
                    }
                    if (slot.heldItem ?? 0)>0,let icon=slot.heldItemIcon,!icon.isEmpty {
                        GameAsset(folder:"Items",name:icon).frame(width:18,height:18)
                            .accessibilityLabel("Holding " + (slot.heldItemName ?? "an item"))
                    }
                }
            }
            Text(slot.empty ? "\(slot.index+1)" : (journals.entries[slot.journalKey]?.displayName.isEmpty == false ? journals.entries[slot.journalKey]!.displayName : slot.displayName)).font(.system(size:9,weight:slot.empty ? .regular : .medium)).lineLimit(1).minimumScaleFactor(0.7).foregroundStyle(slot.empty ? .secondary : .primary)
        }.frame(maxWidth:.infinity).frame(height: sixty).modifier(BoxSlotSurface(selected:selected,empty:slot.empty,party:slot.party))
            .contentShape(Rectangle())
            .accessibilityElement(children:.ignore)
    }
    private var sixty: CGFloat { 60 }
}

// Shared by real storage slots and the Appearance preview. Only the fill fades.
struct BoxSlotSurface:ViewModifier {
    @Environment(\.gameTheme) private var theme
    @AppStorage("boxSlotOpacity") private var opacity=0.82
    @AppStorage("boxSlotBorders") private var borders=true
    let selected:Bool, empty:Bool, party:Bool
    func body(content:Content)->some View {
        let fill = party ? (selected ? 0.72 : 1) : min(1,max(0,opacity))
        content.background((selected ? theme.accent : Color(nsColor:.controlBackgroundColor)).opacity(fill),in:RoundedRectangle(cornerRadius:9))
            .overlay(RoundedRectangle(cornerRadius:9).stroke(selected ? theme.accent : Color.primary.opacity(borders ? 0.12 : 0),lineWidth:selected ? 1.5 : 1))
    }
}
struct PokemonSprite: View {
    @AppStorage("pokemonArtworkStyle") private var artworkStyle="Sprites"
    @Environment(\.gameTheme) private var theme
    let name: String
    var portrait:String?=nil
    @Environment(\.pokemonArtworkGame) private var artworkGame
    private var sprite: NSImage? { SpriteImages.load(name) }
    var body: some View {
        if artworkStyle == "Game Portraits",let image=GamePortraitImages.load(sprite:name,identity:portrait,game:artworkGame) {Image(nsImage:image).resizable().interpolation(.high).scaledToFit()}
        else if artworkStyle != "Sprites",let image=PortraitImages.load(sprite:name,identity:portrait) {Image(nsImage:image).resizable().interpolation(.high).scaledToFit()}
        else if let sprite { Image(nsImage:sprite).resizable().interpolation(.none).scaledToFit() }
        else { PokeballView() }
    }
}

// Trim transparent canvas space at render time, keeping the upstream image files unchanged.
private enum SpriteImages {
    static var cache:[String:NSImage]=[:]
    static func load(_ name:String)->NSImage? {
        if let image=cache[name] {return image}
        let resources=Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/Sprites")
        for candidate in SpriteResourceResolver.candidates(for:name) {
            let url=resources.appendingPathComponent(candidate+".png")
            guard let data=try? Data(contentsOf:url),let image=trimmed(data) else{continue}
            cache[name]=image;return image
        }
        return nil
    }
    static func trimmed(_ data:Data)->NSImage? {
        guard let bitmap=NSBitmapImageRep(data:data),let original=NSImage(data:data) else{return nil}
        var left=bitmap.pixelsWide,top=bitmap.pixelsHigh,right = -1,bottom = -1
        for y in 0..<bitmap.pixelsHigh {for x in 0..<bitmap.pixelsWide {
            if (bitmap.colorAt(x:x,y:y)?.alphaComponent ?? 0)>0.02 {left=min(left,x);right=max(right,x);top=min(top,y);bottom=max(bottom,y)}
        }}
        guard right>=left,bottom>=top,let cropped=bitmap.cgImage?.cropping(to:CGRect(x:left,y:top,width:right-left+1,height:bottom-top+1)) else{return original}
        return NSImage(cgImage:cropped,size:NSSize(width:cropped.width,height:cropped.height))
    }

}

private enum PortraitImages {
    static let cache:NSCache<NSString,NSImage> = {let cache=NSCache<NSString,NSImage>();cache.countLimit=128;cache.totalCostLimit=96*1024*1024;return cache}()
    static let directory=Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/Portraits")
    static let manifest:PortraitManifest? = {guard let data=try? Data(contentsOf:directory.appendingPathComponent("manifest.json")) else{return nil};return try? JSONDecoder().decode(PortraitManifest.self,from:data)}()
    static func load(sprite:String,identity:String?)->NSImage? {
        guard let manifest,let url=PortraitResourceResolver.resolve(sprite:sprite,identity:identity,manifest:manifest,in:directory) else{return nil}
        if let image=cache.object(forKey:url.path as NSString){return image}
        guard let image=NSImage(contentsOf:url) else{return nil}
        cache.setObject(image,forKey:url.path as NSString,cost:Int(image.size.width*image.size.height*4));return image
    }
}

private struct PokemonArtworkGameKey:EnvironmentKey {static let defaultValue=""}
extension EnvironmentValues {var pokemonArtworkGame:String {get{self[PokemonArtworkGameKey.self]}set{self[PokemonArtworkGameKey.self]=newValue}}}
private enum GamePortraitImages {
    static let cache:NSCache<NSString,NSImage> = {let c=NSCache<NSString,NSImage>();c.countLimit=128;return c}()
    static let directory=Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/GamePortraits")
    static let home=Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/Portraits")
    static let manifest:PortraitManifest? = {guard let data=try? Data(contentsOf:home.appendingPathComponent("manifest.json")) else{return nil};return try? JSONDecoder().decode(PortraitManifest.self,from:data)}()
    static func load(sprite:String,identity:String?,game:String)->NSImage? {
        guard let manifest,let url=GamePortraitResourceResolver.resolve(sprite:sprite,identity:identity,game:game,manifest:manifest,homeDirectory:home,in:directory) else{return nil}
        if let cached=cache.object(forKey:url.path as NSString){return cached}
        guard let data=try? Data(contentsOf:url),let image=SpriteImages.trimmed(data) else{return nil};cache.setObject(image,forKey:url.path as NSString);return image
    }
}
