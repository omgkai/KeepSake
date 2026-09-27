import SwiftUI

struct GrowthCards:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    var body:some View {
        if let g=model.state.growth {
            VStack(spacing:18) {
                EditorCard(title:"Experience") {
                    HStack(alignment:.firstTextBaseline) {
                        Label("Level \(g.level)",systemImage:"chart.line.uptrend.xyaxis").font(.title2.bold()).foregroundStyle(theme.accent)
                        Spacer()
                        Text(g.level==100 ? "Maximum level" : "\(max(0,g.next-g.exp).formatted()) to level \(g.level+1)").font(.caption).foregroundStyle(.secondary)
                    }
                    ProgressView(value:g.level==100 ? 1:Double(max(0,g.exp-g.floor))/Double(max(1,g.next-g.floor))).tint(theme.accent)
                    SimpleField(id:"EXP",title:"Total EXP")
                }
                if model.state.fields.contains(where:{$0.id==g.friendshipField && $0.editable}) {EditorCard(title:g.egg ? "Egg progress":"Friendship") {
                    HStack(spacing:14) {
                        Image(systemName:g.egg ? "oval.fill":"heart.fill").font(.system(size:28)).foregroundStyle(.pink).frame(width:48,height:48).background(.pink.opacity(0.1),in:RoundedRectangle(cornerRadius:14))
                        VStack(alignment:.leading,spacing:5) {Text(g.egg ? "Hatch counter":"Friendship").font(.headline);Text(g.egg ? "This stored value tracks hatching instead of friendship." : "Friendship with the current trainer").font(.caption).foregroundStyle(.secondary)}
                        Spacer()
                        Text("\(g.friendship)").font(.system(.title2,design:.rounded).bold()).foregroundStyle(.pink)
                    }
                    if !g.egg {ProgressView(value:Double(g.friendship),total:255).tint(.pink)}
                    SimpleField(id:g.friendshipField,title:g.egg ? "Hatch counter":"Friendship")
                }
                }

            }
        }
    }
}

struct PokerusQuickStatus:View {
    @EnvironmentObject var model:EditorModel
    @State private var details=false
    private let modes=[("None","minus.circle",Color.secondary),("Infected","microbe.fill",Color.purple),("Cured","checkmark.seal.fill",Color.teal)]
    private func pokerusHelp(_ mode:String)->String {
        switch mode {
        case "Infected": return "Infected: active Pokérus. In games that use its training effect, this Pokémon earns double effort values and can spread Pokérus to its party. Right-click for strain and duration."
        case "Cured": return "Cured: Pokérus is no longer contagious. Its effort-value training bonus remains in games that support it. Right-click for strain and duration."
        default: return "None: this Pokémon has no Pokérus record. Click Infected to add it, or Cured to record a past infection. Some games only retain this data from transfers."
        }
    }
    var body:some View {
        if let g=model.state.growth,g.pokerus {
            HStack(spacing:4) {
                Text("Pokérus").font(.system(size:11,weight:.medium)).foregroundStyle(.secondary).padding(.trailing,3)
                ForEach(modes,id:\.0){mode,symbol,color in
                    Button {Task{await model.command(["op":"entityAction","action":"pokerus"+mode])}} label: {
                        Image(systemName:symbol).font(.system(size:15,weight:.semibold)).foregroundStyle(g.pokerusState==mode ? color:.secondary.opacity(0.65)).frame(width:28,height:30)
                            .background(g.pokerusState==mode ? color.opacity(0.17):.clear,in:RoundedRectangle(cornerRadius:8))
                            .overlay(RoundedRectangle(cornerRadius:8).stroke(g.pokerusState==mode ? color.opacity(0.4):.clear,lineWidth:1))
                    }.buttonStyle(.plain).accessibilityLabel("Pokérus: "+mode).accessibilityValue(g.pokerusState==mode ? "Selected":"Not selected").accessibilityAddTraits(g.pokerusState==mode ? .isSelected:[]).help(pokerusHelp(mode)).disabled(model.fieldDrafts || model.busy)
                }
            }.fixedSize().contextMenu {Button("Strain & duration…"){details=true}}
                .popover(isPresented:$details){VStack(alignment:.leading,spacing:14){Label("Pokérus details",systemImage:"microbe.fill").font(.headline);SimpleField(id:"PokerusStrain",title:"Strain");SimpleField(id:"PokerusDays",title:"Days");Text("None, Infected and Cured are available beside Shiny and Egg.").font(.caption).foregroundStyle(.secondary)}.padding(20).frame(width:320)}
        }
    }
}
