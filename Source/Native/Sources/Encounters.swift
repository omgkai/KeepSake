import SwiftUI

struct EncounterEntry:Codable,Identifiable {
    var portrait:String?=nil
    let id:Int, name:String, form:Int, formName:String, game:String, kind:String, level:String, location:String, shiny:String, egg:Bool, details:String, sprite:String
}
struct EncounterData:Codable {let token:String, truncated:Bool, entries:[EncounterEntry]}

struct CatalogChoiceButton:View {
    let title:String, options:[Choice]
    @Binding var value:String
    @State private var presentation:ChoicePresentation?
    var body:some View {
        Button {presentation=ChoicePresentation(title:title,options:options,selected:value)} label: {
            HStack {Text(title).foregroundStyle(.secondary);Text(options.first{$0.value==value}?.label ?? "Choose…").lineLimit(1);Spacer();Image(systemName:"chevron.down").font(.caption)}
        }.disabled(options.isEmpty).sheet(item:$presentation) {p in ChoiceSheet(title:p.title,options:p.options,selected:p.selected){choice in value=choice.value;presentation=nil}}
    }
}
struct EncountersView:View {
    @EnvironmentObject var model:EditorModel
    @State private var species="25"
    @State private var version="Any"
    @State private var category="Any"
    @State private var shiny="Any"
    @State private var moves=["0","0","0","0"]
    @State private var form="-1"
    @State private var instructions=""
    @State private var buildRule=false
    @State private var useEditorCriteria=false
    @State private var customCriteria=false
    @State private var criteriaSheet=false
    @State private var preferences=EncounterPreferences()
    @State private var trainer:EncounterTrainer?
    @State private var selection:Int?
    @State private var filter=""
    @State private var advanced=false
    var rows:[EncounterEntry] {(model.encounters?.entries ?? []).filter{filter.isEmpty || [$0.name,$0.game,$0.kind,$0.location].joined(separator:" ").localizedCaseInsensitiveContains(filter)}}
    var chosen:EncounterEntry? {rows.first{$0.id==selection}}
    var body:some View {
        if !model.state.hasSave {WorkspaceEmptyState(title:"Find their beginning",icon:"binoculars",message:"Open a game save or sample workspace to explore wild encounters, eggs, trades and gifts.")}
        else {
            VStack(alignment:.leading,spacing:16) {
                Text("Encounter search").font(.title2.bold())
                Text("Find wild encounters, eggs, trades, and gifts in PKHeX’s database. Prepare one in the editor, review its legality, then Set to Slot.").foregroundStyle(.secondary)
                HStack {
                    CatalogChoiceButton(title:"Pokémon",options:(model.catalogs["species"] ?? []).filter{$0.value != "0"},value:$species)
                    CatalogChoiceButton(title:"Origin",options:[Choice(value:"Any",label:"Any compatible origin")]+(model.catalogs["games"] ?? []),value:$version)
                    Button("Search") {selection=nil;Task{await model.searchEncounters(species:species,version:version,category:category,shiny:shiny,moves:moves,form:form,filters:instructions)}}.buttonStyle(.borderedProminent)
                }
                DisclosureGroup("More filters",isExpanded:$advanced) {
                    HStack {
                        Picker("Encounter",selection:$category){Text("All types").tag("Any");Text("Wild Pokémon").tag("Slot");Text("Eggs").tag("Egg");Text("In-game gifts & fixed encounters").tag("Static");Text("NPC trades").tag("Trade");Text("Mystery Gifts").tag("Mystery")}
                        Picker("Shiny rule",selection:$shiny){Text("Any").tag("Any");Text("Guaranteed shiny").tag("Always");Text("Shiny locked").tag("Never")}
                        TextField("Form (−1 = all)",text:$form).frame(width:120)
                    }.padding(.top,8)
                    LazyVGrid(columns:[GridItem(.flexible()),GridItem(.flexible())]){ForEach(0..<4,id:\.self){i in CatalogChoiceButton(title:"Move \(i+1)",options:[Choice(value:"0",label:"Any move")]+(model.catalogs["moves"] ?? []).filter{$0.value != "0"},value:$moves[i])}}
                    HStack { TextField("Advanced filters, e.g. =LevelMin=5",text:$instructions).textFieldStyle(.roundedBorder); Button("Add Rule…"){buildRule=true} }.sheet(isPresented:$buildRule){SearchRuleBuilder(instructions:$instructions,encounter:true)}
                    Toggle("Prepare using the editor’s nature, gender, ability and IV criteria",isOn:$useEditorCriteria).disabled(customCriteria).toggleStyle(.checkbox).help("Requires the same species. PKHeX may relax impossible criteria; review the prepared Pokémon and legality report.")
                    HStack {Toggle("Use custom generation preferences",isOn:$customCriteria).toggleStyle(.checkbox);Button("Edit Preferences…"){criteriaSheet=true}.disabled(!customCriteria)}

                }
                EncounterTrainerPicker(selected:$trainer)
                if let data=model.encounters {
                    TextField("Filter results by location, game, or encounter…",text:$filter).textFieldStyle(.roundedBorder)
                    Table(rows,selection:$selection) {
                        TableColumn("Pokémon"){entry in HStack {PokemonSprite(name:entry.sprite,portrait:entry.portrait).frame(width:26,height:26);Text(entry.name)}}.width(min:110,ideal:145)
                        TableColumn("Form",value:\.formName).width(min:65,ideal:90)
                        TableColumn("Origin",value:\.game).width(min:100,ideal:155)
                        TableColumn("Encounter",value:\.kind)
                        TableColumn("Level",value:\.level).width(55)
                        TableColumn("Location",value:\.location)
                    }.overlay{if rows.isEmpty{WorkspaceEmptyState(title:"No encounters found",icon:"sparkle.magnifyingglass",message:"Try another Pokémon or broaden the origin, move and encounter filters.").background(Color(nsColor:.windowBackgroundColor))}}
                    if let chosen {Text(chosen.details).font(.caption).foregroundStyle(.secondary).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading)}
                    HStack {
                        Text("\(rows.count) results"+(data.truncated ? " · First 2,000 shown; narrow the filters" : "")).font(.caption).foregroundStyle(.secondary)
                        Spacer()
                        Button("Prepare Pokémon") {if let chosen{model.prepareEncounter(chosen,token:data.token,useEditorCriteria:useEditorCriteria && !customCriteria,criteria:customCriteria ? preferences:nil,trainer:trainer)}}.buttonStyle(.borderedProminent).disabled(chosen==nil)
                    }
                    Text("Results can include earlier evolutions. Move filters find possible learning paths; preparation keeps the encounter’s starting moves and form.").font(.caption).foregroundStyle(.secondary)
                } else {WorkspaceEmptyState(title:"A new adventure awaits",icon:"binoculars",message:"Choose a Pokémon above and search for its story. Your current Pokémon and save stay unchanged until you prepare a result.")}
            }.padding(24).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.topLeading)
                .sheet(isPresented:$criteriaSheet){EncounterPreferencesView(preferences:$preferences)}
        }
    }
}
extension EditorModel {
    func searchEncounters(species:String,version:String,category:String,shiny:String,moves:[String],form:String,filters:String) async {
        guard !busy else{return};busy=true;defer{busy=false}
        do {encounters=try await bridge.send(["op":"encounterSearch","species":Int(species) ?? 0,"version":version,"category":category,"shiny":shiny,"moves":moves.map{Int($0) ?? 0},"form":Int(form) ?? -1,"filters":filters.replacingOccurrences(of:"|",with:"\n")],as:EncounterData.self);status="Encounter search complete"}
        catch{self.error=error.localizedDescription}
    }
    func prepareEncounter(_ entry:EncounterEntry,token:String,useEditorCriteria:Bool=false,criteria:EncounterPreferences?=nil,trainer:EncounterTrainer?=nil) {
        guard !busy,confirmDiscard(all:false) else{return}
        var request:[String:Any]=["op":"encounterPrepare","id":entry.id,"token":token,"useEditorCriteria":useEditorCriteria]
        if let criteria{request["criteria"]=criteria.request}
        if let trainer{request["trainer"]=trainer.request}
        Task{await command(request,status:"Encounter prepared — review legality and Set to Slot");if error==nil{section="Pokémon"}}
    }
}
