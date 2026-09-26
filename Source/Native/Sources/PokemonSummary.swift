import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct PokemonSummaryView:View {
    @EnvironmentObject private var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @State private var image:NSImage?
    @State private var privateTrainer=false
    @State private var dark=false
    var body:some View {
        VStack(spacing:18) {
            HStack {Label("A portrait of your companion",systemImage:"photo.fill").font(.title2.bold());Spacer();Button("Done"){dismiss()}}
            if let image {Image(nsImage:image).resizable().scaledToFit().frame(maxHeight:490).clipShape(RoundedRectangle(cornerRadius:18))}
            HStack {Toggle("Include trainer name & ID",isOn:$privateTrainer);Toggle("Night colors",isOn:$dark);Spacer()}.toggleStyle(.checkbox)
            HStack {Text("Image only. Your Pokémon data stays unchanged.").font(.caption).foregroundStyle(.secondary);Spacer();Button("Copy Image",action:copy);Button("Save PNG…",action:save).buttonStyle(.borderedProminent)}
        }.padding(24).frame(width:740,height:640).task{render()}.onChange(of:privateTrainer){_,_ in render()}.onChange(of:dark){_,_ in render()}
    }
    @MainActor private func render() {
        let renderer=ImageRenderer(content:PokemonSummaryCard(state:model.state,catalogs:model.catalogs,includeTrainer:privateTrainer,dark:dark).environment(\.pokemonArtworkGame,model.state.gameVersion))
        renderer.scale=2
        image=renderer.nsImage
    }
    private func copy() {guard let image else{return};NSPasteboard.general.clearContents();NSPasteboard.general.writeObjects([image]);model.status="Copied Pokémon summary image"}
    private func save() {
        guard let image,let tiff=image.tiffRepresentation,let data=NSBitmapImageRep(data:tiff)?.representation(using:.png,properties:[:]) else{return}
        let panel=NSSavePanel();panel.allowedContentTypes=[.png];panel.nameFieldStringValue="Pokemon Summary.png"
        if panel.runModal() == .OK,let url=panel.url {do {try data.write(to:url,options:.atomic)}catch{model.error=error.localizedDescription}}
    }
}
private struct PokemonSummaryCard:View {
    let state:EditorState
    let catalogs:[String:[Choice]]
    let includeTrainer:Bool, dark:Bool
    private func value(_ id:String)->String {state.fields.first{$0.id==id}?.value ?? "—"}
    private func name(_ id:String,_ catalog:String)->String {catalogs[catalog]?.first{$0.value==value(id)}?.label ?? value(id)}
    var body:some View {
        VStack(alignment:.leading,spacing:20) {
            HStack {Label("KEEPSAKE",systemImage:"book.closed.fill").font(.caption.bold()).tracking(3);Spacer();Text(state.originGame).font(.caption)}.foregroundStyle(.secondary)
            HStack(spacing:22) {
                PokemonSprite(name:state.entitySprite,portrait:state.entityPortrait).frame(width:125,height:125)
                VStack(alignment:.leading,spacing:7) {
                    Text(state.entityNickname.isEmpty ? state.entityName : state.entityNickname).font(.system(size:32,weight:.bold,design:.rounded))
                    Text("\(state.entityName) · Level \(state.entityLevel)").font(.headline).foregroundStyle(.secondary)
                    Text(name("Ability","abilities") + " · " + name("Nature","natures")).font(.callout)
                    if value("HeldItem") != "0" {Label(name("HeldItem","items"),systemImage:"bag.fill").font(.caption)}
                }
            }
            HStack(spacing:10) {ForEach(Array(["HP","ATK","DEF","SPE","SPA","SPD"].enumerated()),id:\.offset) { i,label in
                VStack(spacing:5) {Text(label).font(.caption2.bold()).foregroundStyle(.secondary);Text(i < state.stats.count ? String(state.stats[i]) : "—").font(.title3.bold())}.frame(maxWidth:.infinity).padding(12).background(Color.accentColor.opacity(0.09),in:RoundedRectangle(cornerRadius:12))
            }}
            VStack(alignment:.leading,spacing:10) {ForEach(state.moveChecks.filter{!$0.relearn && $0.move != 0},id:\.slot) { move in
                HStack {GameAsset(folder:"MoveTypes",name:String(format:"type_icon_%02d",move.type)).frame(width:26,height:26);Text(catalogs["moves"]?.first{$0.value==String(move.move)}?.label ?? "Move \(move.move)").font(.headline);Spacer();Text(move.typeName).font(.caption).foregroundStyle(.secondary)}
            }}
            if includeTrainer {Divider();Text("Trainer \(value("OriginalTrainerName")) · ID \(value("DisplayTID"))").font(.caption).foregroundStyle(.secondary)}
        }.padding(30).frame(width:650).background(dark ? Color(hex:"122039") : Color(hex:"F8F6F0")).foregroundStyle(dark ? Color.white : Color(hex:"17243B")).environment(\.colorScheme,dark ? .dark : .light)
    }
}
