import SwiftUI
import AppKit

struct GameAsset:View {
    let folder:String, name:String
    var fallback="shippingbox"
    var body:some View {
        if let image=AssetCache.image(folder,name) {GameArtwork(image:image,whiteOutline:AssetCache.needsOutline(folder,name))}
        else {Image(systemName:fallback).resizable().scaledToFit().foregroundStyle(.secondary).padding(5)}
    }
}
private enum AssetCache {
    static var images:[String:NSImage]=[:]
    static let hisuiBalls:[String:String]=["_ball27":"STRANGEBALL","_ball28":"POKEBALL","_ball29":"GREATBALL","_ball30":"ULTRABALL","_ball31":"FEATHERBALL","_ball34":"HEAVYBALL","_ball37":"ORIGINBALL"]
    static func needsOutline(_ folder:String,_ name:String)->Bool {
        (folder=="Items" && (name.hasPrefix("hisui_") || name.hasPrefix("paldea_"))) || (folder=="Balls" && hisuiBalls[name] != nil)
    }
    static func image(_ folder:String,_ name:String)->NSImage? {
        let key=folder+"/"+name
        if let image=images[key]{return image}
        let hisuiName=folder=="Items" && name.hasPrefix("hisui_") ? String(name.dropFirst(6)) : folder=="Balls" ? hisuiBalls[name] : nil
        if let hisuiName,let image=NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/HisuiItems/\(hisuiName).png")){images[key]=image;return image}
        if folder=="Items",name.hasPrefix("paldea_"),let image=NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/PaldeaItems/\(String(name.dropFirst(7))).png")){images[key]=image;return image}
        let names=folder=="Items" ? [name,name.replacingOccurrences(of:"bitem_",with:"aitem_"),"bitem_unk"] : [name]
        for candidate in names {if let image=NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/\(folder)/\(candidate).png")){images[key]=image;return image}}
        return nil
    }
}
struct GenderBadge:View {
    let value:Int
    var body:some View {Text(value==0 ? "♂ Male" : value==1 ? "♀ Female" : "Genderless").font(.caption.weight(.semibold)).foregroundStyle(value==0 ? Color.blue : value==1 ? Color.pink : Color.secondary)}
}
struct PokemonEmblem:View {
    let kind:String
    var color:Color {kind=="IsShiny" ? .orange : kind=="IsAlpha" ? .red : .mint}
    var body:some View {
        ZStack {
            RoundedRectangle(cornerRadius:8).fill(color.opacity(0.15))
            if kind=="IsEgg" {
                ZStack {Ellipse().fill(Color(.controlBackgroundColor)).overlay(Ellipse().stroke(color,lineWidth:1.5));Ellipse().fill(color).frame(width:5,height:7).offset(x:-3,y:2);Circle().fill(color).frame(width:4).offset(x:3,y:-4)}.frame(width:15,height:20)
            } else {Image(systemName:kind=="IsAlpha" ? "eye.fill" : "sparkles").font(.system(size:17,weight:.semibold)).foregroundStyle(color)}
        }.frame(width:30,height:30)
    }
}
struct TrainerCardView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    @AppStorage("trainerCardStyle") private var style="Midnight"
    @AppStorage("trainerCardTitle") private var title="A new adventure"
    @AppStorage("trainerCardPartner") private var partner="25"
    @AppStorage("trainerCardAccent") private var accent="D9B665"
    @State private var customize=false
    @State private var adventureDetails=false
    private func value(_ id:String)->String {model.state.saveFields.first{$0.id==id}?.value ?? "—"}
    private var ink:Color {Color(hex:accent)}
    var body:some View {
        if !model.state.hasSave {ContentUnavailableView("Your trainer card",systemImage:"person.crop.rectangle",description:Text("Open a save to see your trainer's adventure."))}
        else {ScrollView {
            VStack(alignment:.leading,spacing:24) {
                HStack {VStack(alignment:.leading){Text("Trainer card").font(.largeTitle.bold());Text("Your adventure, your signature.").foregroundStyle(.secondary)};Spacer();Button {customize.toggle()} label:{Label("Customize Card",systemImage:"paintbrush.pointed.fill")}}
                VStack(alignment:.leading,spacing:24) {
                    HStack {Label("KEEPSAKE · TRAINER PASSPORT",systemImage:"book.closed.fill").font(.caption.weight(.bold)).tracking(2);Spacer();SaveGameLogo(version:model.state.gameLogoVersion,name:model.state.game)}
                    HStack(alignment:.center,spacing:24) {
                        PokemonSprite(name:"b_"+partner).frame(width:110,height:110).padding(14).background(.white.opacity(0.08),in:RoundedRectangle(cornerRadius:28))
                        VStack(alignment:.leading,spacing:8) {Text(value("OT")).font(.system(size:38,weight:.bold,design:.rounded));GenderBadge(value:Int(value("Gender")) ?? 2);Text(title).font(.title3).foregroundStyle(.white.opacity(0.8))}
                        Spacer()
                    }
                    TrainerTeamDisplay(ink:ink)
                    Divider().overlay(ink.opacity(0.4))
                    HStack(spacing:36) {cardFact("TRAINER ID",value("DisplayTID"));cardFact("ADVENTURE",model.state.game);cardFact("PLAY TIME",value("PlayedHours")+"h "+value("PlayedMinutes")+"m");Spacer();Image(systemName:"seal.fill").font(.largeTitle).foregroundStyle(ink)}
                }.padding(30).foregroundStyle(.white).background(LinearGradient(colors:style=="Midnight" ? [Color(hex:"0C1830"),Color(hex:"223D60")] : style=="Game Colors" ? [theme.accent.opacity(0.95),Color(hex:"13223A")] : [Color(hex:"4A2456"),Color(hex:"1A2C4C")],startPoint:.topLeading,endPoint:.bottomTrailing),in:RoundedRectangle(cornerRadius:24))
                    .overlay(RoundedRectangle(cornerRadius:24).stroke(ink.opacity(0.65),lineWidth:1)).shadow(color:.black.opacity(0.12),radius:14,y:7)
                if customize {EditorCard(title:"Make it yours") {
                    Picker("Card style",selection:$style){ForEach(["Midnight","Game Colors","Twilight"],id:\.self){Text($0)}}.pickerStyle(.segmented)
                    TextField("Card motto",text:$title).textFieldStyle(.roundedBorder)
                    CatalogChoiceButton(title:"Partner",options:(model.catalogs["species"] ?? []).filter{$0.value != "0"},value:$partner)
                    ColorPicker("Foil color",selection:Binding(get:{ink},set:{accent=NSColor($0).usingColorSpace(.deviceRGB).map{String(format:"%02X%02X%02X",Int(max(0,min(1,$0.redComponent))*255),Int(max(0,min(1,$0.greenComponent))*255),Int(max(0,min(1,$0.blueComponent))*255))} ?? "D9B665"}),supportsOpacity:false)
                    Text("Card styling is saved on this Mac. It does not change your game's trainer appearance.").font(.caption).foregroundStyle(.secondary)
                }}
                Button { adventureDetails=true } label: { Label("Adventure Details…",systemImage:"map.fill") }.disabled(model.fieldDrafts)
                EditorCard(title:"Trainer & progress") {ForEach(model.state.saveFields.filter{$0.group=="Trainer" && $0.editable}){FieldRow(field:$0,target:"save")}}
            }.padding(28).frame(maxWidth:960,alignment:.leading).frame(maxWidth:.infinity,alignment:.top)
                .sheet(isPresented:$adventureDetails) { TrainerDetailsView() }
        }}
    }
    private func cardFact(_ name:String,_ value:String)->some View {VStack(alignment:.leading,spacing:5){Text(name).font(.system(size:9,weight:.bold)).tracking(1.3).foregroundStyle(ink);Text(value).font(.system(.callout,design:.rounded).weight(.semibold))}}
}
extension EditorModel {
    func loadFashion() async {guard !busy,state.canFashion else{return};do{fashion=try await bridge.send(["op":"fashion"],as:FashionData.self)}catch{self.error=error.localizedDescription}}
}
struct MotionPreferences:View {
    @AppStorage("pokemonMotion") private var motion="Bounce"
    @AppStorage("pokemonHoverCards") private var hoverCards=true
    var body:some View {VStack(alignment:.leading,spacing:12) {
        Label("Pokémon interactions",systemImage:"hand.draw.fill").font(.headline)
        Picker("Animation",selection:$motion){ForEach(["Off","Bounce","Sway","Sparkle"],id:\.self){Text($0)}}.pickerStyle(.segmented)
        Toggle("Show Pokémon details on hover",isOn:$hoverCards).toggleStyle(.switch)
        Text("Controls slot selection, hover and drag previews. Reduce Motion is respected.").font(.caption).foregroundStyle(.secondary)
    }}
}
