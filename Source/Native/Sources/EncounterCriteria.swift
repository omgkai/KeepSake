import SwiftUI

struct EncounterPreferences:Codable,Equatable {
    var nature=25,gender=2,ability = -1,shiny=0
    var ivs=[-1,-1,-1,-1,-1,-1]
    var levelMin=0,levelMax=0,hiddenPower = -1,form = -1,mutations=0
    var request:[String:Any] { ["nature":nature,"gender":gender,"ability":ability,"shiny":shiny,"ivs":ivs,"levelMin":levelMin,"levelMax":levelMax,"hiddenPower":hiddenPower,"form":form,"mutations":mutations] }
}

struct EncounterPreferencesView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    @Binding var preferences:EncounterPreferences
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            HStack {Label("Encounter preferences",systemImage:"slider.horizontal.3").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}
            Text("Choose how a new encounter is generated. Fixed encounter rules can override these preferences; check the result and its legality report before placing it.").foregroundStyle(.secondary)
            HStack {
                Button("Reset to Any"){preferences=EncounterPreferences()}
                Button("Copy from Editor"){Task{do{preferences=try await model.bridge.send(["op":"encounterCriteria","fromEditor":true],as:EncounterPreferences.self)}catch{model.error=error.localizedDescription}}}.disabled(model.state.fields.isEmpty)
            }
            GroupBox("Personality") {
                VStack(spacing:12) {
                    Picker("Nature",selection:$preferences.nature) {Text("Any nature").tag(25);ForEach(Array((model.catalogs["natures"] ?? []).enumerated()),id:\.offset){index,choice in Text(choice.label).tag(index)}}
                    Picker("Gender",selection:$preferences.gender){Text("Any / genderless").tag(2);Text("Male").tag(0);Text("Female").tag(1)}
                    Picker("Ability",selection:$preferences.ability){Text("Any ability").tag(-1);Text("Any regular ability").tag(0);Text("First regular ability").tag(1);Text("Second regular ability").tag(2);Text("Hidden ability").tag(4)}
                    Picker("Shiny",selection:$preferences.shiny){Text("Random").tag(0);Text("Not shiny").tag(1);Text("Shiny").tag(2);Text("Star shiny").tag(3);Text("Square shiny").tag(4)}
                }.frame(maxWidth:.infinity,alignment:.leading).padding(10)
            }
            GroupBox("Individual values") {
                HStack {ForEach(0..<6,id:\.self){i in
                    VStack {Text(["HP","Attack","Defense","Sp. Atk","Sp. Def","Speed"][i]).font(.caption).foregroundStyle(.secondary)
                        Picker("IV \(i+1)",selection:$preferences.ivs[i]){Text("Any").tag(-1);ForEach(0...31,id:\.self){Text(String($0)).tag($0)}}.labelsHidden()
                    }
                }}.frame(maxWidth:.infinity,alignment:.leading).padding(10)
            }
            DisclosureGroup("Advanced constraints") {
                VStack(alignment:.leading,spacing:12) {
                    HStack {Text("Level range (0 / 0 = any)");TextField("Minimum",value:$preferences.levelMin,format:.number);Text("to");TextField("Maximum",value:$preferences.levelMax,format:.number)}
                    HStack {Text("Hidden Power type (−1 = any, 0–15)");TextField("Type",value:$preferences.hiddenPower,format:.number)}
                    HStack {Text("Random encounter form (−1 = any)");TextField("Form",value:$preferences.form,format:.number)}
                    Text("Allow the generator to account for later changes:").font(.caption).foregroundStyle(.secondary)
                    HStack {flag("Mints",1);flag("Hyper Training",2);flag("Ability Capsule",4);flag("Ability Patch",8)}
                    flag("Only neutral natures",128)
                }.textFieldStyle(.roundedBorder).padding(.top,8)
            }
            Spacer(minLength:0)
        }.padding(24).frame(width:650,height:660)
    }
    private func flag(_ title:String,_ bit:Int)->some View {
        Toggle(title,isOn:Binding(get:{preferences.mutations & bit != 0},set:{if $0{preferences.mutations |= bit}else{preferences.mutations &= ~bit}})).toggleStyle(.checkbox)
    }
}
