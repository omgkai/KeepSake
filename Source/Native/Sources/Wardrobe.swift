import SwiftUI

struct WardrobeItem:Codable,Identifiable {let id:String,category:String,name:String,owned:Bool,editable:Bool}
struct FashionData:Codable {let supported:Bool,canUnlock:Bool,description:String,fields:[Field],items:[WardrobeItem],revision:Int}
struct FashionView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    @State private var search=""
    @State private var category="All"
    @State private var ownership="All"
    private var items:[WardrobeItem] {(model.fashion?.items ?? []).filter{(category=="All" || $0.category==category) && (ownership=="All" || (ownership=="Owned" ? $0.owned : !$0.owned)) && (search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || $0.id.localizedCaseInsensitiveContains(search))}}
    private var categories:[String] {Array(Set(model.fashion?.items.map(\.category) ?? [])).sorted()}
    var body:some View {ScrollView {VStack(alignment:.leading,spacing:22) {
        HStack(alignment:.top) {
            VStack(alignment:.leading,spacing:6){Label("Your wardrobe",systemImage:"tshirt.fill").font(.largeTitle.bold());Text("Find your next favorite look.").font(.title3).foregroundStyle(.secondary)}
            Spacer();SaveGameLogo(version:model.state.gameLogoVersion,name:model.state.game)
        }
        if let data=model.fashion {
            HStack(spacing:22) {wardrobeCount("Owned",data.items.filter(\.owned).count);wardrobeCount("In catalog",data.items.count);Spacer()
                if data.canUnlock {Button {giveAll("")} label:{Label("Give All Clothing",systemImage:"gift.fill")}.buttonStyle(.borderedProminent)}
            }.padding(20).background(theme.accent.opacity(0.10),in:RoundedRectangle(cornerRadius:20))
            Text(data.description).font(.callout).foregroundStyle(.secondary)
            if !data.items.isEmpty {
                HStack {TextField("Search clothing or item ID…",text:$search).textFieldStyle(.roundedBorder);Picker("Category",selection:$category){Text("All categories").tag("All");ForEach(categories,id:\.self){Text($0).tag($0)}}.frame(width:230);Picker("Show",selection:$ownership){ForEach(["All","Owned","Missing"],id:\.self){Text($0)}}.frame(width:170)}
                HStack {Text("\(items.count) items").font(.caption).foregroundStyle(.secondary);Spacer();if category != "All" {Button {giveAll(category)} label:{Label("Give All \(category)",systemImage:"gift.fill")}}}
                if items.isEmpty {ContentUnavailableView.search(text:search)}
                LazyVGrid(columns:[GridItem(.adaptive(minimum:175,maximum:240))],spacing:14) {
                    ForEach(items) {item in
                        VStack(alignment:.leading,spacing:12) {
                            ZStack(alignment:.topTrailing) {
                                FashionIllustration(category:item.category,name:item.name).frame(height:94).frame(maxWidth:.infinity).background(theme.accent.opacity(0.06),in:RoundedRectangle(cornerRadius:14))
                                if item.owned {Image(systemName:"checkmark.seal.fill").foregroundStyle(.green).padding(8).accessibilityLabel("Owned")}
                            }
                            Text(item.name).font(.callout.weight(.semibold)).lineLimit(3).frame(height:50,alignment:.topLeading)
                            Text(item.category).font(.caption).foregroundStyle(.secondary)
                            Toggle("Owned",isOn:Binding(get:{item.owned},set:{value in Task{await model.command(["op":"wardrobeSet","id":item.id,"owned":value,"revision":data.revision],status:"Wardrobe updated — Undo restores the change")}})).toggleStyle(.switch).controlSize(.small).accessibilityLabel("Own "+item.name).disabled(!item.editable)
                        }.padding(14).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:20)).overlay(RoundedRectangle(cornerRadius:20).stroke(item.owned ? theme.accent.opacity(0.4) : Color.primary.opacity(0.07)))
                    }
                }
                Text("Illustrations show clothing categories and color cues, not exact in-game garment previews. Unknown names retain their item ID.").font(.caption).foregroundStyle(.secondary)
            } else {ContentUnavailableView("Collection unlock",systemImage:"tshirt",description:Text("This game exposes a collection unlock rather than named individual garments."))}
            if !data.fields.isEmpty {DisclosureGroup("Advanced equipped outfit") {Text("These fields change the outfit stored in the game. Owning a garment does not equip it.").font(.caption).foregroundStyle(.secondary);ForEach(data.fields){FieldRow(field:$0,target:"fashion")}}}
        } else {ProgressView()}
    }.padding(28).frame(maxWidth:.infinity,alignment:.topLeading)}.disabled(model.busy || model.fieldDrafts).task{await model.loadFashion()}.onChange(of:model.state.gameVersion){_,_ in category="All";search="";ownership="All"}}
    private func wardrobeCount(_ title:String,_ count:Int)->some View {VStack(alignment:.leading){Text(count.formatted()).font(.title.bold());Text(title).font(.caption).foregroundStyle(.secondary)}}
    private func giveAll(_ category:String) {guard let data=model.fashion else{return};Task{await model.command(data.items.isEmpty ? ["op":"fashionUnlock"] : ["op":"wardrobeSet","mode":"give","category":category,"revision":data.revision],status:"Clothing added — Undo restores it; Export Copy saves it")}}
}
struct FashionIllustration:View {
    let category:String,name:String
    private var ink:Color {
        let colors:[(String,String)]=[("white","E8E5D9"),("lily","E8E5D9"),("black","3C4051"),("slate","435568"),("crimson","C95765"),("scarlet","C95765"),("red","C95765"),("pink","E58AAE"),("azalea","E58AAE"),("yellow","E2C269"),("daffodil","E2C269"),("gold","D3AD54"),("green","729C83"),("pine","467E69"),("teal","4FA49C"),("blue","658FC3"),("sapphire","658FC3"),("navy","41587C"),("purple","9982BE"),("amethyst","9982BE"),("orange","D99259"),("tangerine","D99259"),("brown","987457")]
        return Color(hex:colors.first(where:{name.lowercased().contains($0.0)})?.1 ?? "8399B1")
    }
    var body:some View {ZStack {
        Circle().fill(ink.opacity(0.12)).frame(width:84,height:84)
        FashionShape(category:category).fill(ink.gradient).overlay(FashionShape(category:category).stroke(.white.opacity(0.9),lineWidth:2)).frame(width:64,height:64).shadow(color:ink.opacity(0.22),radius:6,y:4)
        if name.localizedCaseInsensitiveContains("Pikachu") || name.localizedCaseInsensitiveContains("Eevee") || name.localizedCaseInsensitiveContains("Gengar") {PokemonSprite(name:"b_"+(name.contains("Pikachu") ? "25" : name.contains("Eevee") ? "133" : "94")).frame(width:23,height:23).offset(y:5)}
    }.accessibilityLabel("Illustration: \(category)")}
}
private struct FashionShape:Shape {
    let category:String
    func path(in r:CGRect)->Path {
        var p=Path();let w=r.width,h=r.height
        func polygon(_ points:[CGPoint]) {p.addLines(points.map{CGPoint(x:r.minX+$0.x*w,y:r.minY+$0.y*h)});p.closeSubpath()}
        switch category {
        case "Hats":p.addRoundedRect(in:CGRect(x:w*0.16,y:h*0.23,width:w*0.64,height:h*0.43),cornerSize:CGSize(width:17,height:17));p.addRoundedRect(in:CGRect(x:w*0.03,y:h*0.59,width:w*0.94,height:h*0.14),cornerSize:CGSize(width:5,height:5))
        case "Glasses":p.addRoundedRect(in:CGRect(x:w*0.02,y:h*0.37,width:w*0.4,height:h*0.31),cornerSize:CGSize(width:8,height:8));p.addRoundedRect(in:CGRect(x:w*0.58,y:h*0.37,width:w*0.4,height:h*0.31),cornerSize:CGSize(width:8,height:8));p.addRect(CGRect(x:w*0.4,y:h*0.43,width:w*0.2,height:h*0.06))
        case "Bottoms","Legwear":polygon([CGPoint(x:0.2,y:0.13),CGPoint(x:0.8,y:0.13),CGPoint(x:0.87,y:0.91),CGPoint(x:0.58,y:0.91),CGPoint(x:0.5,y:0.49),CGPoint(x:0.42,y:0.91),CGPoint(x:0.13,y:0.91)])
        case "Shoes":polygon([CGPoint(x:0.2,y:0.3),CGPoint(x:0.48,y:0.3),CGPoint(x:0.5,y:0.53),CGPoint(x:0.8,y:0.57),CGPoint(x:0.94,y:0.7),CGPoint(x:0.94,y:0.8),CGPoint(x:0.12,y:0.8),CGPoint(x:0.12,y:0.45)])
        case "Earrings":p.addEllipse(in:CGRect(x:w*0.14,y:h*0.27,width:w*0.24,height:h*0.49));p.addEllipse(in:CGRect(x:w*0.62,y:h*0.27,width:w*0.24,height:h*0.49))
        case "Gloves":polygon([CGPoint(x:0.29,y:0.9),CGPoint(x:0.24,y:0.62),CGPoint(x:0.09,y:0.4),CGPoint(x:0.17,y:0.32),CGPoint(x:0.33,y:0.45),CGPoint(x:0.29,y:0.13),CGPoint(x:0.39,y:0.09),CGPoint(x:0.46,y:0.38),CGPoint(x:0.45,y:0.04),CGPoint(x:0.56,y:0.04),CGPoint(x:0.59,y:0.38),CGPoint(x:0.63,y:0.1),CGPoint(x:0.73,y:0.13),CGPoint(x:0.73,y:0.43),CGPoint(x:0.8,y:0.24),CGPoint(x:0.89,y:0.28),CGPoint(x:0.82,y:0.62),CGPoint(x:0.74,y:0.9)])
        case "Phone cases":p.addRoundedRect(in:CGRect(x:w*0.26,y:h*0.05,width:w*0.48,height:h*0.9),cornerSize:CGSize(width:10,height:10))
        case "Bags":p.addRoundedRect(in:CGRect(x:w*0.34,y:h*0.09,width:w*0.32,height:h*0.3),cornerSize:CGSize(width:8,height:8));p.addRoundedRect(in:CGRect(x:w*0.17,y:h*0.27,width:w*0.66,height:h*0.63),cornerSize:CGSize(width:12,height:12))
        default:polygon([CGPoint(x:0.3,y:0.13),CGPoint(x:0.43,y:0.21),CGPoint(x:0.57,y:0.21),CGPoint(x:0.7,y:0.13),CGPoint(x:0.96,y:0.37),CGPoint(x:0.8,y:0.56),CGPoint(x:0.72,y:0.49),CGPoint(x:0.72,y:0.91),CGPoint(x:0.28,y:0.91),CGPoint(x:0.28,y:0.49),CGPoint(x:0.2,y:0.56),CGPoint(x:0.04,y:0.37)])
        };return p
    }
}
