import SwiftUI
import UniformTypeIdentifiers

struct EncounterTrainer:Codable,Identifiable,Equatable {
    var id:String {"\(version):\(name):\(tid):\(sid):\(gender):\(language):\(consoleRegion):\(country):\(region)"}
    let name:String,version:String,game:String
    let tid:Int,sid:Int,gender:Int,language:Int,consoleRegion:Int,country:Int,region:Int
    var request:[String:Any] {["name":name,"version":version,"tid":tid,"sid":sid,"gender":gender,"language":language,"consoleRegion":consoleRegion,"country":country,"region":region]}
}
struct EncounterTrainerPicker:View {
    @EnvironmentObject var model:EditorModel
    @AppStorage("encounterTrainerProfiles") private var stored=Data()
    @Binding var selected:EncounterTrainer?
    @State private var profiles:[EncounterTrainer]=[]
    var body:some View {
        HStack {
            Picker("Original trainer",selection:Binding(get:{selected?.id ?? ""},set:{id in selected=profiles.first{$0.id==id}})) {
                Text("Loaded save").tag("")
                ForEach(profiles){p in Text("\(p.name) · \(p.game) · \(p.tid)").tag(p.id)}
            }
            Menu {
                Button("Remember Loaded Trainer"){remember(nil)}
                Button("Import from Save or Pokémon…"){
                    let panel=NSOpenPanel();panel.canChooseDirectories=false;panel.allowsMultipleSelection=false
                    if panel.runModal() == .OK,let url=panel.url{remember(url)}
                }
                if let selected {Button("Forget Selected Trainer",role:.destructive){profiles.removeAll{$0.id==selected.id};self.selected=nil;persist()}}
            } label:{Label("Trainers",systemImage:"person.crop.rectangle.stack")}
            .help("Profiles are stored locally. Pick one that matches the encounter's origin game. Fixed event trainers remain controlled by the encounter.")
        }.task{profiles=(try? JSONDecoder().decode([EncounterTrainer].self,from:stored)) ?? []}
    }
    private func persist(){if let data=try? JSONEncoder().encode(profiles){stored=data}}
    private func remember(_ url:URL?){
        guard !model.busy else{return}
        Task {
            do {
                var request:[String:Any]=["op":"encounterTrainer"];if let url{request["path"]=url.path}
                let profile=try await model.bridge.send(request,as:EncounterTrainer.self)
                if !profiles.contains(where:{$0.id==profile.id}){profiles.append(profile);persist()}
                selected=profile
            }catch{model.error=error.localizedDescription}
        }
    }
}
