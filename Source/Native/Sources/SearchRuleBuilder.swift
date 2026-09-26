import SwiftUI

struct SearchRuleBuilder:View {
    @EnvironmentObject private var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @Binding var instructions:String
    var separator=" | "
    var encounter=false
    var gift=false
    @State private var field="Species"
    @State private var comparison="="
    @State private var value="25"
    private var properties:[Choice] {
        let names=gift ? ["Species","Form","CardID","Generation","Level","HeldItem","IsShiny","IsEgg","OriginalTrainerName"] : encounter ? ["Species","Form","LevelMin","LevelMax","Generation","Version","IsShiny","IsEgg"] : ["Species","Form","CurrentLevel","HeldItem","Ability","Nature","Gender","IsShiny","IsEgg","OriginalTrainerName","TID16","SID16","IV_HP","IV_ATK","IV_DEF","IV_SPA","IV_SPD","IV_SPE","EV_HP","EV_ATK","EV_DEF","EV_SPA","EV_SPD","EV_SPE"]
        return names.map{key in Choice(value:key,label:model.state.fields.first{$0.id==key}?.label ?? key)}
    }
    private var choices:[Choice] {
        if field == "IsShiny" || field == "IsEgg" {return [Choice(value:"true",label:"Yes"),Choice(value:"false",label:"No")]}
        let catalog=field == "Species" ? "species" : field == "HeldItem" ? "items" : field == "Ability" ? "abilities" : field == "Nature" ? "natures" : field == "Version" ? "games" : ""
        return model.catalogs[catalog] ?? []
    }
    var body:some View {
        VStack(alignment:.leading,spacing:20) {
            HStack {Label("Build a search rule",systemImage:"line.3.horizontal.decrease.circle.fill").font(.title2.bold());Spacer();Button("Cancel"){dismiss()}}
            Text("Add a condition without typing PKHeX syntax. Combine rules to narrow the results; properties absent from a format won’t match.").font(.callout).foregroundStyle(.secondary)
            CatalogChoiceButton(title:"Property",options:properties,value:$field)
            Picker("Comparison",selection:$comparison) {Text("Equals").tag("=");Text("Does not equal").tag("!");Text("Greater than").tag(">");Text("Less than").tag("<")}
            if !choices.isEmpty {CatalogChoiceButton(title:"Value",options:choices,value:$value)}
            else {TextField("Value",text:$value).textFieldStyle(.roundedBorder)}
            Text(comparison+field+"="+value).font(.body.monospaced()).textSelection(.enabled).padding(14).frame(maxWidth:.infinity,alignment:.leading).background(.quaternary,in:RoundedRectangle(cornerRadius:12))
            HStack {Spacer();Button("Add Rule") {let rule=comparison+field+"="+value;instructions += instructions.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty ? rule : separator+rule;dismiss()}.buttonStyle(.borderedProminent).disabled(value.isEmpty || value.contains("\n") || value.contains("\r") || value.contains("|"))}
        }.padding(24).frame(width:560).onChange(of:field){_,_ in value=choices.first?.value ?? "0"}
    }
}
