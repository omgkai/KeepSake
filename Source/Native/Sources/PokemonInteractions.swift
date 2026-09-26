import SwiftUI

struct HoverMove:Codable {let name:String,type:Int,typeName:String,legal:Bool}
struct SlotPreviewData:Codable {
    var portrait:String?=nil
    let name:String,species:String,sprite:String,gender:Int,ball:Int,item:String,itemName:String,shiny:Bool,egg:Bool,alpha:Bool,level:Int,text:String,moves:[HoverMove],legal:Bool,report:String,origin:String,trainer:String,encounter:String
}
private struct HoverCardPresentation:Identifiable {
    let id=UUID()
    let data:SlotPreviewData
}
private struct HoverRequest:Equatable {
    let hovering:Bool,enabled:Bool,busy:Bool,empty:Bool,party:Bool
    let box:Int,slot:Int,revision:Int
}
struct InteractiveSlotCell:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @AppStorage("pokemonMotion") private var motion="Bounce"
    @AppStorage("pokemonHoverCards") private var hoverCards=true
    let slot:Slot,selected:Bool
    @State private var hovering=false
    @State private var preview:HoverCardPresentation?
    private var animated:Bool {!reduceMotion && motion != "Off"}
    private var hoverRequest:HoverRequest {
        HoverRequest(hovering:hovering,enabled:hoverCards,busy:model.busy,empty:slot.empty,party:slot.party,box:model.state.box,slot:slot.index,revision:model.state.revision)
    }
    var body:some View {
        SlotCell(slot:slot,selected:selected)
            .scaleEffect(animated && hovering && motion=="Bounce" ? 1.07 : 1)
            .rotationEffect(.degrees(animated && hovering && motion=="Sway" ? 5 : 0))
            .overlay(alignment:.topLeading){if animated && hovering && motion=="Sparkle" {Image(systemName:"sparkles").foregroundStyle(.orange).font(.title3).allowsHitTesting(false)}}
            .animation(animated ? .spring(response:0.35,dampingFraction:0.55) : nil,value:hovering)
            .animation(animated ? .spring(response:0.35,dampingFraction:0.65) : nil,value:selected)
            .onHover{hovering=$0;if !$0{preview=nil}}
            .task(id:hoverRequest) {
                let request=hoverRequest
                preview=nil
                guard request.hovering,request.enabled,!request.empty,!request.busy else{return}
                do {
                    try await Task.sleep(for:.milliseconds(650));try Task.checkCancellation()
                    let data=try await model.bridge.send(["op":"slotPreview","box":request.box,"slot":request.slot,"party":request.party,"revision":request.revision],as:SlotPreviewData.self)
                    try Task.checkCancellation()
                    guard request==hoverRequest,NSEvent.pressedMouseButtons==0 else{return}
                    preview=HoverCardPresentation(data:data)
                } catch { /* A cancelled or stale request must not present a card. */ }
            }
            .popover(item:$preview,arrowEdge:.leading){PokemonHoverCard(data:$0.data)}
            .onDisappear{preview=nil}
    }
}
struct PokemonHoverCard:View {
    let data:SlotPreviewData
    var body:some View {ScrollView {VStack(alignment:.leading,spacing:12) {
        HStack(spacing:12) {PokemonSprite(name:data.sprite,portrait:data.portrait).frame(width:60,height:60);VStack(alignment:.leading,spacing:5){Text(data.name.isEmpty ? data.species : data.name).font(.title2.bold());Text("\(data.species) · Lv. \(data.level)").font(.caption).foregroundStyle(.secondary);GenderBadge(value:data.gender)};Spacer();GameAsset(folder:"Balls",name:"_ball\(data.ball)").frame(width:26,height:26)}
        HStack {if data.shiny{PokemonEmblem(kind:"IsShiny")};if data.egg{PokemonEmblem(kind:"IsEgg")};if data.alpha{PokemonEmblem(kind:"IsAlpha")};Spacer();Label(data.legal ? "Legal" : "Needs review",systemImage:data.legal ? "checkmark.shield.fill" : "exclamationmark.shield.fill").font(.caption).foregroundStyle(data.legal ? Color.green : .orange)}
        Text(data.text).font(.system(size:11,design:.monospaced)).textSelection(.enabled).fixedSize(horizontal:false,vertical:true)
        Divider()
        ForEach(Array(data.moves.enumerated()),id:\.offset){_,move in if move.name != "—" && move.name != "(None)" {HStack {MoveTypeBadge(type:move.type,name:move.typeName,compact:true);Text(move.name).font(.callout);Spacer();Image(systemName:move.legal ? "checkmark.circle.fill" : "xmark.circle.fill").foregroundStyle(move.legal ? Color.green : .red)}}}
        HStack {GameAsset(folder:"Items",name:data.item).frame(width:26,height:26);Text(data.itemName).font(.caption)}
        Label(data.origin,systemImage:"map.fill").font(.caption).foregroundStyle(.secondary)
        Label("OT · "+data.trainer,systemImage:"person.crop.circle").font(.caption).foregroundStyle(.secondary)
        Text(data.encounter).font(.caption2).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
        if !data.legal {Text(data.report).font(.caption2).foregroundStyle(.orange).fixedSize(horizontal:false,vertical:true)}
    }.padding(18)}.frame(width:360).frame(maxHeight:620)}
}
struct PokemonDragPreview:View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @AppStorage("pokemonMotion") private var motion="Bounce"
    let slot:Slot
    @State private var active=false
    var enabled:Bool {!reduceMotion && motion != "Off"}
    var body:some View {
        VStack(spacing:8) {
            ZStack {PokemonSprite(name:slot.sprite,portrait:slot.portrait).frame(width:66,height:60);if enabled && motion=="Sparkle" {Image(systemName:"sparkles").foregroundStyle(.orange).offset(x:30,y:-24).opacity(active ? 1 : 0.3)}}
                .offset(y:enabled && motion=="Bounce" && active ? -8 : 0)
                .rotationEffect(.degrees(enabled && motion=="Sway" ? (active ? 9 : -9) : 0))
            Text(slot.displayName).font(.caption.bold())
        }.padding(18).background(.regularMaterial,in:RoundedRectangle(cornerRadius:20))
            .onAppear {if enabled {withAnimation(.easeInOut(duration:0.55).repeatForever(autoreverses:true)){active=true}}}
    }
}
struct PokemonSlotButtonStyle:ButtonStyle {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @AppStorage("pokemonMotion") private var motion="Bounce"
    func makeBody(configuration:Configuration)->some View {
        configuration.label.scaleEffect(configuration.isPressed && motion != "Off" && !reduceMotion ? 0.90 : 1)
            .animation(motion != "Off" && !reduceMotion ? .spring(response:0.25,dampingFraction:0.5) : nil,value:configuration.isPressed)
    }
}
