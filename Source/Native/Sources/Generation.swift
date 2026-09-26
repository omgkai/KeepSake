import SwiftUI
import AppKit

struct GeneratedPokemon:Codable,Identifiable {
    let id:Int,name:String,sprite:String,level:Int,`extension`:String,method:String,changes:[String],error:String,report:String
}
struct GenerationPreview:Codable {let token:String,revision:Int,ready:Bool,entries:[GeneratedPokemon],game:String}
struct GenerationSheet:View {
    @EnvironmentObject var model:EditorModel
    @EnvironmentObject var journals:JournalStore
    @Environment(\.dismiss) private var dismiss
    let team:JournalTeam?
    var showdown:String?=nil
    private var isTeam:Bool {team != nil || showdown != nil}
    @State private var preview:GenerationPreview?
    @State private var working=false
    @State private var error:String?
    @State private var destination=0
    @State private var exported=""
    var body:some View {
        VStack(alignment:.leading,spacing:18) {
            HStack {Label(!isTeam ? "Auto-Legality":"Prepare your team",systemImage:!isTeam ? "wand.and.stars":"person.3.fill").font(.title2.bold());Spacer();Button("Done"){dismiss()}.disabled(working).keyboardShortcut(.cancelAction)}
            Text(showdown != nil ? "Showdown team → \(model.state.game). Every set is generated for this game and checked for legality. Review all results before writing to empty slots or exporting files.":!isTeam ? "Find a legal encounter for the Pokémon in your editor. Review changed details before applying; Undo remains available.":"\(team?.name ?? "Showdown team") → \(model.state.game). Saved Pokémon are transferred where compatible. Personal pages generate new level 100 Pokémon, including their chosen shiny appearance.").font(.callout).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
            if working {WorkspaceEmptyState(title:"Finding the right encounter…",icon:"sparkles",message:"Each companion is checked with PKHeX. Complex searches can take a little longer."){ProgressView()}}
            else if let preview {
                ScrollView {VStack(spacing:14){ForEach(preview.entries){entry in
                    VStack(alignment:.leading,spacing:12) {
                        HStack {PokemonSprite(name:entry.sprite).frame(width:64,height:64);VStack(alignment:.leading,spacing:5){Text(entry.name).font(.title3.bold());Text(entry.error.isEmpty ? "Lv. \(entry.level) · .\(entry.extension)":"Needs attention").font(.caption).foregroundStyle(.secondary);Text(entry.method).font(.callout)};Spacer();Label(entry.error.isEmpty ? "Legal":"Unavailable",systemImage:entry.error.isEmpty ? "checkmark.shield.fill":"exclamationmark.triangle.fill").foregroundStyle(entry.error.isEmpty ? .green:.orange)}
                        if !entry.error.isEmpty {Text(entry.error).font(.callout).foregroundStyle(.orange).textSelection(.enabled)}
                        if !entry.changes.isEmpty {DisclosureGroup("\(entry.changes.count) changed details"){VStack(alignment:.leading,spacing:5){ForEach(Array(entry.changes.enumerated()),id:\.offset){_,line in Text(line).font(.caption).textSelection(.enabled)}}.frame(maxWidth:.infinity,alignment:.leading).padding(.top,8)}}
                        if !entry.report.isEmpty {DisclosureGroup("PKHeX legality report"){Text(entry.report).font(.caption.monospaced()).textSelection(.enabled).frame(maxWidth:.infinity,alignment:.leading)}}
                    }.padding(18).background(.quaternary.opacity(0.45),in:RoundedRectangle(cornerRadius:18))
                }}}
                if preview.ready {
                    if isTeam {
                        HStack {Picker("Destination box",selection:$destination){ForEach(Array(model.state.boxNames.enumerated()),id:\.offset){i,name in Text(name.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty ? "Box \(i+1)":"\(i+1) · \(name)").tag(i)}};Button("Add to Empty Slots"){apply("teamPlace")}.buttonStyle(.borderedProminent).disabled(model.state.boxCount==0)}
                        HStack {Button("Export Pokémon Files…"){exportTeam()}.disabled(model.state.demo);Text("Adds to empty, unlocked slots only. Export Copy saves the updated game file.").font(.caption).foregroundStyle(.secondary)}
                    } else {HStack {Text("Applying updates the editor; Set to Slot writes it into the save.").font(.caption).foregroundStyle(.secondary);Spacer();Button("Apply to Editor"){apply("legalityApply")}.buttonStyle(.borderedProminent).disabled(preview.entries.allSatisfy{$0.changes.isEmpty})}}
                } else {Text("No team is written until every companion passes its checks.").font(.caption).foregroundStyle(.secondary)}
            } else {Spacer()}
            if let error{Text(error).foregroundStyle(.orange).textSelection(.enabled);Button("Try Again"){Task{await prepare()}}}
            if !exported.isEmpty {Label("Exported to "+exported,systemImage:"checkmark.circle.fill").font(.caption).foregroundStyle(.green).textSelection(.enabled)}
        }.padding(24).frame(width:790,height:710).task{destination=model.state.box;await prepare()}.interactiveDismissDisabled(working)
    }
    private func prepare()async {
        guard !model.busy else{error="Wait for the current action to finish, then try again.";return}
        working=true;model.busy=true;error=nil;preview=nil
        defer{working=false;model.busy=false}
        do {
            var request:[String:Any]=["op":!isTeam ? "legalityPreview":"teamPreview"]
            if let showdown {request=["op":"showdownTeamPreview","text":showdown]}
            if let team {request["members"]=try team.members.map{key -> [String:Any] in
                guard let entry=journals.entries[key] else{throw JournalError("A team companion is missing from your journal.")}
                let sprite=entry.sprite.dropFirst(2).replacingOccurrences(of:"s",with:"").split(separator:"-")
                return ["name":entry.title,"data":entry.pokemonData ?? "","extension":entry.pokemonExtension ?? "","species":Int(sprite.first ?? "0") ?? 0,"form":sprite.count>1 ? Int(sprite[1]) ?? 0:0,"shiny":entry.sprite.hasSuffix("s")]
            }}
            preview=try await model.bridge.send(request,as:GenerationPreview.self)
        } catch {self.error=error.localizedDescription}
    }
    private func apply(_ operation:String) {
        guard let preview else{return}
        Task {await model.command(["op":operation,"token":preview.token,"box":destination]);if model.error==nil{dismiss()}}
    }
    private func exportTeam() {
        guard let preview else{return}
        let panel=NSOpenPanel();panel.canChooseFiles=false;panel.canChooseDirectories=true;panel.canCreateDirectories=true;panel.prompt="Export Team";panel.message="A new folder will contain one Pokémon file per team member."
        guard panel.runModal() == .OK,let url=panel.url else{return}
        Task {do {let result=try await model.bridge.send(["op":"teamExport","token":preview.token,"path":url.path],as:PathResult.self);exported=result.path}catch{self.error=error.localizedDescription}}
    }
}
struct TrainerTeamDisplay:View {
    @State private var partyCapture:JournalPartyCapture?
    @EnvironmentObject var model:EditorModel
    let ink:Color
    private var party:[Slot] {model.state.partySlots.filter{!$0.empty}}
    var body:some View {
        VStack(alignment:.leading,spacing:14) {
            HStack {
                Text("TRAVELING TOGETHER").font(.system(size:10,weight:.bold)).tracking(1.4).foregroundStyle(ink)
                Spacer()
                Button{partyCapture=model.state.journalParty}label:{Label("Save Party as Team",systemImage:"book.closed.fill")}.buttonStyle(.bordered).tint(ink).disabled(party.isEmpty || model.busy)
            }
            HStack(spacing:10) {ForEach(0..<6,id:\.self){index in
                VStack(spacing:5) {
                    if index<party.count {PokemonSprite(name:party[index].sprite).frame(height:46);Text(party[index].displayName).font(.caption.bold()).lineLimit(1);Text("Lv. \(party[index].level)").font(.caption2).foregroundStyle(.white.opacity(0.65))}
                    else {Image(systemName:"circle.dashed").font(.title2).foregroundStyle(.white.opacity(0.25)).frame(height:46);Text("Empty").font(.caption).foregroundStyle(.white.opacity(0.4));Text(" ").font(.caption2)}
                }.padding(10).frame(maxWidth:.infinity).background(.white.opacity(0.07),in:RoundedRectangle(cornerRadius:14))
            }}
            if !model.state.trainerBadges.isEmpty {
                TrainerBadgeStrip(badges:model.state.trainerBadges,ink:ink,stars:model.state.gameVersion=="PLA")
            }
            if !model.state.trainerJourney.isEmpty {
                HStack(spacing:12){ForEach(model.state.trainerJourney,id:\.title){item in
                    HStack(spacing:12){Image(systemName:item.symbol).font(.title2).foregroundStyle(ink);VStack(alignment:.leading,spacing:3){Text(item.value).font(.title2.bold());Text(item.title).font(.caption).foregroundStyle(.white.opacity(0.6))};Spacer()}.padding(16).background(.white.opacity(0.07),in:RoundedRectangle(cornerRadius:14))
                }}
            }
            if model.state.trainerBadges.isEmpty && model.state.trainerJourney.isEmpty {
                Label("Your adventure · \(model.state.gameVersion)",systemImage:"map.fill").font(.caption).foregroundStyle(ink)
            }
        }.sheet(item:$partyCapture){JournalPartySheet(capture:$0)}
    }

}
