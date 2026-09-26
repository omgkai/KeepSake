import SwiftUI

struct StatsProfile:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    private let rows:[(String,String,Int,Color)]=[("HP","HP",0,.pink),("Attack","ATK",1,.orange),("Defense","DEF",2,.yellow),("Sp. Atk","SPA",4,.cyan),("Sp. Def","SPD",5,.green),("Speed","SPE",3,.purple)]
    private var grit:Bool{has("GV_HP")}
    private var awakened:Bool{has("AV_HP")}
    private var effort:String{grit ? "GV_":awakened ? "AV_":"EV_"}
    private var hyper:Bool{has("HT_HP")}
    private func has(_ id:String)->Bool{model.state.fields.contains{$0.id==id && $0.editable}}
    private func number(_ id:String)->Int{Int(model.state.fields.first{$0.id==id}?.value ?? "0") ?? 0}
    private var nature:Int{number(has("StatAlignment") ? "StatAlignment":"Nature")}
    private func natureColor(_ i:Int)->Color{if i==0 || nature/5==nature%5{return .primary};return i==nature/5+1 ? .red:i==nature%5+1 ? .blue:.primary}
    var body:some View {
        VStack(alignment:.leading,spacing:18){
            HStack(alignment:.top){VStack(alignment:.leading,spacing:5){Label("Stat profile",systemImage:"chart.bar.xaxis").font(.title3.bold());Text("Base total · \(model.state.baseStats.reduce(0,+))").font(.caption).foregroundStyle(.secondary)};Spacer();VStack(alignment:.trailing,spacing:5){HStack(spacing:4){ForEach(0..<4){i in Image(systemName:i<=model.state.potential ? "star.fill":"star").foregroundStyle(i<=model.state.potential ? theme.companion:.secondary.opacity(0.5))}}.font(.system(size:15));Text("IV potential").font(.caption2).foregroundStyle(.secondary)}.accessibilityElement(children:.ignore).accessibilityLabel("IV potential: \(model.state.potential+1) of 4 stars").help("PKHeX’s overall IV rating. Hyper Training does not change the original IV rating.")}
            Grid(alignment:.leading,horizontalSpacing:10,verticalSpacing:13){
                GridRow{Text("Stat").frame(minWidth:58,alignment:.leading);Text("Base").frame(width:55);Text(model.state.generation<3 ? "DV":"IV").frame(width:46);Text(grit ? "Grit":awakened ? "AV":"EV").frame(width:52);if hyper{Text("HT").frame(width:24)};Text("Total").frame(width:46,alignment:.trailing)}.font(.caption2.weight(.semibold)).foregroundStyle(.secondary)
                ForEach(rows,id:\.1){title,key,index,color in
                    if model.state.generation != 1 || key != "SPD" {GridRow{
                        HStack(spacing:6){Capsule().fill(color).frame(width:3,height:20);Text(model.state.generation==1 && key=="SPA" ? "Special":title).font(.system(size:11,weight:.medium))}
                        baseCell(index,color)
                        StatInput(id:"IV_"+key,title:title+" IV").frame(width:46)
                        StatInput(id:effort+key,title:title+" effort").frame(width:52)
                        if hyper{Toggle("Hyper Train "+title,isOn:Binding(get:{model.state.fields.first{$0.id=="HT_"+key}?.value=="true"},set:{value in Task{await model.editPokemon("HT_"+key,value:value ? "true":"false")}})).labelsHidden().toggleStyle(.checkbox).frame(width:24).disabled(model.fieldDrafts)}
                        Text(model.state.stats.indices.contains(index) ? String(model.state.stats[index]):"—").font(.system(.body,design:.rounded).bold()).foregroundStyle(natureColor(index)).frame(width:46,alignment:.trailing)
                    }}
                }
            }.frame(maxWidth:.infinity)
            HStack{Label("IVs · \(rows.reduce(0){$0+number("IV_"+$1.1)})",systemImage:"sparkle");Spacer();Text("\(grit ? "Grit":awakened ? "AVs":"EVs") · \(rows.reduce(0){$0+number(effort+$1.1)})")}.font(.caption).foregroundStyle(.secondary)
            if hyper{Text("HT = Hyper Trained. Red totals are boosted by nature; blue totals are lowered.").font(.caption2).foregroundStyle(.secondary)}
        }.padding(18).background(theme.accent.opacity(0.055),in:RoundedRectangle(cornerRadius:20)).overlay(RoundedRectangle(cornerRadius:20).stroke(theme.accent.opacity(0.16),lineWidth:1))
    }
    private func baseCell(_ index:Int,_ color:Color)->some View {
        let value=model.state.baseStats.indices.contains(index) ? model.state.baseStats[index]:0
        return VStack(spacing:4){Text("\(value)").font(.system(.callout,design:.rounded).weight(.semibold));GeometryReader{g in Capsule().fill(color.opacity(0.15)).overlay(alignment:.leading){Capsule().fill(color).frame(width:g.size.width*min(1,Double(value)/255))}}.frame(height:3)}.frame(width:55).accessibilityLabel("Base stat \(value)")
    }
}
