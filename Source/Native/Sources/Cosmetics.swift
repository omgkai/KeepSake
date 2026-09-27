import SwiftUI

struct CosmeticInfo:Codable {let height,weight,scale:String;let canRecalculate:Bool;let origin,battle:String}
private struct CosmeticSizeChoice:Identifiable {let id,title,value:String}
struct CosmeticsView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    @State private var page="Appearance"
    @State private var autoSize=true
    @State private var training=false
    @State private var sizeChoice:CosmeticSizeChoice?
    private let contest:[(String,String,Color)]=[("ContestCool","Cool",.orange),("ContestBeauty","Beauty",.blue),("ContestCute","Cute",.pink),("ContestSmart","Clever",.green),("ContestTough","Tough",.yellow),("ContestSheen","Sheen",.purple)]
    private func has(_ id:String)->Bool{model.state.fields.contains{$0.id==id && $0.editable}}
    private func value(_ id:String)->String{model.state.fields.first{$0.id==id}?.value ?? "0"}
    private var pages:[String]{["Appearance","Awards"]+(has("ContestCool") ? ["Contests"]:[])+(!history.isEmpty ? ["History"]:[])}
    private var history:[Field]{model.state.fields.filter{$0.editable && ($0.id.contains("Memory") || $0.id.contains("Affection") || $0.id.hasPrefix("Geo") || ["Fullness","Enjoyment","Country","Region","ConsoleRegion"].contains($0.id))}}
    var body:some View {
        VStack(spacing:0){
            HStack(spacing:12){PokemonSprite(name:model.state.entitySprite).frame(width:44,height:44);VStack(alignment:.leading,spacing:3){Text("A little more personality").font(.headline);Text("Cosmetics, keepsakes & accomplishments").font(.caption).foregroundStyle(.secondary)};Spacer()}.padding([.horizontal,.top],18)
            Picker("Cosmetics section",selection:$page){ForEach(pages,id:\.self){Text($0)}}.pickerStyle(.segmented).padding(18)
            if page=="Awards" {DecorationsView()}
            else {ScrollView{VStack(alignment:.leading,spacing:18){
                if page=="Appearance" {appearance}
                else if page=="Contests" {contests}
                else {MemoriesAndCareView();if history.contains(where:{["Country","Region","ConsoleRegion"].contains($0.id)}) {EditorCard(title:"Region of origin"){ForEach(history.filter{["Country","Region","ConsoleRegion"].contains($0.id)}){f in SimpleField(id:f.id,title:f.label)}}}}

                Text("These are Pokémon data fields. Use Set to Slot to keep changes in your save; personal journal styling stays separate.").font(.caption).foregroundStyle(.secondary)
            }.padding(18)}}
        }.sheet(item:$sizeChoice){choice in ChoiceSheet(title:choice.title+" scalar · 0–255",options:(0...255).map{Choice(value:String($0),label:String($0))},selected:choice.value){value in sizeChoice=nil;cosmetic(["mode":"size","field":choice.id,"value":value.value,"automatic":autoSize])}}.sheet(isPresented:$training){SuperTrainingView()}.onChange(of:model.state.entityExtension){_,_ in if !pages.contains(page){page="Appearance"}}
    }
    private var appearance:some View {
        Group {
            EditorCard(title:"Origins & special traits"){
                HStack(spacing:14){if value("IsShiny")=="true"{Label("Shiny",systemImage:"sparkles").foregroundStyle(theme.companion)};if value("IsEgg")=="true"{Label("Egg",systemImage:"oval.fill")};if (Int(value("PKRS_Strain")) ?? 0)>0{Label(value("PKRS_Days")=="0" ? "Pokérus cured":"Pokérus",systemImage:"cross.case.fill").foregroundStyle(.pink)}}.font(.caption)
                HStack{SaveGameLogo(version:model.state.originVersion,name:model.state.originGame);Spacer();Text(model.state.originGame).font(.callout).foregroundStyle(.secondary)}
                ForEach([("IsFavorite","Favorite"),("NSparkle","N’s sparkle"),("PokeStarFame","Pokéstar fame"),("Spirit","Partner spirit"),("Mood","Partner mood"),("WalkingMood","Walking mood"),("BattleVersion","Battle-ready game"),("CanGigantamax","Gigantamax form"),("DynamaxLevel","Dynamax level")],id:\.0){id,title in SimpleField(id:id,title:title)}
                if has("IsAlpha"){QuickFlag(id:"IsAlpha",title:"Alpha",icon:"sparkles")}
                if model.state.superTraining != nil{Button{training=true}label:{Label("Super Training medals…",systemImage:"medal.fill")}.disabled(model.fieldDrafts)}
            }
            if has("HeightScalar"){EditorCard(title:"Size & presence"){
                HStack(spacing:12){sizeSummary("Height",model.state.cosmeticInfo?.height ?? "","ruler");sizeSummary("Weight",model.state.cosmeticInfo?.weight ?? "","scalemass");if has("Scale"){sizeSummary("Scale",model.state.cosmeticInfo?.scale ?? "","arrow.up.left.and.arrow.down.right")}}
                ForEach([("HeightScalar","Height"),("WeightScalar","Weight"),("Scale","Scale")],id:\.0){id,title in if has(id){HStack{Button{sizeChoice=CosmeticSizeChoice(id:id,title:title,value:value(id))}label:{HStack{Text(title);Spacer();Text(value(id)).monospacedDigit();Image(systemName:"chevron.up.chevron.down").font(.caption2)}};Stepper(title,value:Binding(get:{Int(value(id)) ?? 0},set:{number in cosmetic(["mode":"size","field":id,"value":String(number),"automatic":autoSize])}),in:0...255).labelsHidden().fixedSize()}.disabled(model.fieldDrafts)}}
                if model.state.cosmeticInfo?.canRecalculate==true {Toggle("Auto height, weight & CP",isOn:$autoSize).font(.caption).onChange(of:autoSize){_,enabled in if enabled{cosmetic(["mode":"recalculate"])}}.disabled(model.fieldDrafts);Button("Recalculate Now"){cosmetic(["mode":"recalculate"])}.disabled(model.fieldDrafts)}
                ForEach([("HeightAbsolute","Height (meters)","autoHeight"),("WeightAbsolute","Weight (kg)","autoWeight")],id:\.0){id,title,mode in
                    if has(id){HStack{SimpleField(id:id,title:title).disabled(autoSize);Button("Auto"){cosmetic(["mode":mode])}.disabled(model.fieldDrafts || model.busy).help("Calculate "+title+" from the Pokémon’s size scalars using PKHeX")}}
                }
                Text("Auto derives measured size from your scalars; it does not randomize them.").font(.caption).foregroundStyle(.secondary)
                if has("Stat_CP"){SimpleField(id:"Stat_CP",title:"Combat Power").disabled(autoSize)}
            }}
            if has("ShinyLeaf"){EditorCard(title:"Shiny Leaves"){
                let leaves=Int(value("ShinyLeaf")) ?? 0
                HStack(spacing:12){ForEach(0..<6){i in let on=(leaves&(1<<i)) != 0
                    Button{var next=leaves^(1<<i);if next&31 != 31{next &= ~32};cosmetic(["mode":"leaves","value":next])}label:{Image(systemName:i==5 ? "crown.fill":"leaf.fill").font(.title2).foregroundStyle(on ? theme.companion:.secondary.opacity(0.3)).frame(maxWidth:.infinity).padding(.vertical,12).background(theme.companion.opacity(on ? 0.12:0.025),in:RoundedRectangle(cornerRadius:10))}.buttonStyle(.plain).disabled(model.fieldDrafts || (i==5 && leaves&31 != 31)).accessibilityLabel(i==5 ? "Leaf crown":"Shiny Leaf \(i+1)").accessibilityValue(on ? "Obtained":"Missing")
                }}
                HStack{Button("Give All"){cosmetic(["mode":"leaves","value":63])};Button("Clear"){cosmetic(["mode":"leaves","value":0])}}.disabled(model.fieldDrafts)
            }}
        }
    }
    private var contests:some View {
        EditorCard(title:"Contest conditions"){
            HStack{Label {Text("Ready for the spotlight")} icon:{Text("🎀")}.font(.headline);Spacer();Menu("Set Conditions"){Button("Max All"){cosmetic(["mode":"contest","all":true])};Button("Clear All"){cosmetic(["mode":"contest","all":false])}}.disabled(model.fieldDrafts)}
            ForEach(contest,id:\.0){id,title,color in VStack(spacing:6){SimpleField(id:id,title:title);GeometryReader{g in Capsule().fill(color.opacity(0.12)).overlay(alignment:.leading){Capsule().fill(color.gradient).frame(width:g.size.width*min(1,max(0,Double(value(id)) ?? 0)/255))}}.frame(height:5)}}
            Text("Each condition is stored from 0 to 255. Transferred Pokémon may retain contest records in games without contests.").font(.caption).foregroundStyle(.secondary)
        }
    }
    private func sizeSummary(_ title:String,_ text:String,_ symbol:String)->some View{VStack(spacing:5){Image(systemName:symbol).foregroundStyle(theme.accent);Text(text).font(.callout.bold());Text(title).font(.caption2).foregroundStyle(.secondary)}.frame(maxWidth:.infinity).padding(10).background(theme.accent.opacity(0.07),in:RoundedRectangle(cornerRadius:12))}
    private func cosmetic(_ payload:[String:Any]){Task{await model.command(payload.merging(["op":"cosmeticSet"]){_,n in n},status:"Cosmetics updated — Set to Slot to keep them")}}
}
