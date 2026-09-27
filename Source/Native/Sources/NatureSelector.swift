import SwiftUI

struct NatureSelector:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    var mint=false
    @State private var choosing=false
    private var choices:[Choice]{model.catalogs["natures"] ?? []}
    private let stats=["Attack","Defense","Speed","Sp. Atk","Sp. Def"]
    private var selected:Int{mint ? model.state.natureInfo?.alignment ?? 0:model.state.natureInfo?.nature ?? 0}
    private var field:String{mint ? "StatAlignment":"Nature"}
    private var editable:Bool{model.state.fields.contains{$0.id==field && $0.editable}}
    private func name(_ index:Int)->String{choices.indices.contains(index) ? choices[index].label:model.state.fields.first{$0.id==field}?.value ?? "Nature"}
    var body:some View {
        if editable,let info=model.state.natureInfo {
            VStack(alignment:.leading,spacing:10){
                Button{choosing=true}label:{
                    HStack(spacing:10){Image(systemName:mint ? "leaf.fill":"sun.max.fill").foregroundStyle(theme.accent).font(.title3)
                        VStack(alignment:.leading,spacing:3){Text(mint ? "Mint effect":"Nature").font(.caption).foregroundStyle(.secondary);Text(name(selected)).font(.headline)}
                        Spacer();Image(systemName:"chevron.up.chevron.down").font(.caption).foregroundStyle(.secondary)
                    }.padding(12).background(theme.accent.opacity(0.08),in:RoundedRectangle(cornerRadius:12))
                }.buttonStyle(.plain).disabled(model.busy || model.fieldDrafts).accessibilityLabel((mint ? "Mint effect: ":"Nature: ")+name(selected))
                if info.hasEffects {effects(selected)}else{Text("Natures do not affect stats in this format.").font(.caption).foregroundStyle(.secondary)}
                if !mint && info.mint && info.nature != info.alignment {Label("A mint changes the stat effects below, not this Pokémon’s original nature.",systemImage:"leaf").font(.caption).foregroundStyle(.secondary)}
                if mint {Text("Changes stat growth without changing the original nature or flavor preference.").font(.caption).foregroundStyle(.secondary)}
            }.task{if choices.isEmpty {do{model.catalogs["natures"]=try await model.fetchChoices("natures")}catch{model.error=error.localizedDescription}}}
            .popover(isPresented:$choosing){
                VStack(alignment:.leading,spacing:14){Text(mint ? "Choose a mint effect":"Choose a nature").font(.title3.bold());Text("↑ Boosted · ↓ Reduced · = Neutral").font(.caption).foregroundStyle(.secondary)
                    ScrollView{LazyVGrid(columns:[GridItem(.fixed(156)),GridItem(.fixed(156)),GridItem(.fixed(156))],spacing:10){
                        ForEach(Array(choices.enumerated()),id:\.element.id){index,choice in
                            Button{choosing=false;Task{await model.editPokemon(field,value:choice.value)}}label:{VStack(alignment:.leading,spacing:6){HStack{Text(choice.label).fontWeight(.semibold);Spacer();if index==selected{Image(systemName:"checkmark.circle.fill")}};Text(effectText(index)).font(.caption).foregroundStyle(.secondary)}.padding(10).frame(width:156,height:68,alignment:.leading).background(theme.accent.opacity(index==selected ? 0.18:0.055),in:RoundedRectangle(cornerRadius:10))}.buttonStyle(.plain)
                        }
                    }}
                }.padding(18).frame(width:530,height:440)
            }
        }
    }
    private func effectText(_ n:Int)->String{guard (0..<25).contains(n) else{return "Unavailable"};return n/5==n%5 ? "= All stats unchanged":"↑ \(stats[n/5]) · ↓ \(stats[n%5])"}
    @ViewBuilder private func effects(_ n:Int)->some View {
        if (0..<25).contains(n) {
            if n/5==n%5 {Label("Neutral · all stats unchanged",systemImage:"equal.circle.fill").font(.callout).foregroundStyle(theme.accent).padding(10).frame(maxWidth:.infinity,alignment:.leading).background(theme.accent.opacity(0.05),in:RoundedRectangle(cornerRadius:10))}
            else {HStack(spacing:8){effectChip(stats[n/5],"+10%","arrow.up.circle.fill",.red);effectChip(stats[n%5],"−10%","arrow.down.circle.fill",.blue)};Text("HP and all other stats are unchanged.").font(.caption2).foregroundStyle(.secondary)}
        }
    }
    private func effectChip(_ stat:String,_ amount:String,_ icon:String,_ color:Color)->some View{HStack(spacing:7){Image(systemName:icon);Text(stat).fontWeight(.medium);Spacer(minLength:3);Text(amount).monospacedDigit()}.font(.caption).foregroundStyle(color).padding(10).frame(maxWidth:.infinity).background(color.opacity(0.09),in:RoundedRectangle(cornerRadius:10))}
}
