import SwiftUI

struct JournalPartyCapture: Identifiable {
    let id=UUID()
    let game:String
    let members:[JournalPartyMember]
}
extension EditorState {
    var journalParty:JournalPartyCapture {
        JournalPartyCapture(game:game,members:partySlots.filter{!$0.empty}.sorted{$0.index<$1.index}.map {
            JournalPartyMember(sourceKey:$0.journalKey,entry:PokemonJournal(species:$0.name,nickname:$0.nickname,sprite:$0.sprite,game:game,level:$0.level,pokemonData:$0.pokemonData,pokemonExtension:$0.pokemonExtension,portrait:$0.portrait))
        })
    }
}
struct JournalPartySheet:View {
    @EnvironmentObject var journals:JournalStore
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let capture:JournalPartyCapture
    @State private var name=""
    @State private var destination="new"
    @State private var style="Midnight"
    @State private var emblem="flag.fill"
    @State private var error:String?
    private var available:[JournalTeam] {
        journals.teams.values.filter{$0.members.count+capture.members.count<=6}.sorted{$0.name.localizedStandardCompare($1.name) == .orderedAscending}
    }
    var body:some View {
        VStack(alignment:.leading,spacing:20) {
            HStack {
                Label("Keep this adventure",systemImage:"book.closed.fill").font(.title2.bold())
                Spacer();Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction)
            }
            Text("Your current party · \(capture.game)").font(.headline)
            HStack(spacing:10) {
                ForEach(Array(capture.members.enumerated()),id:\.offset) { index,member in
                    VStack(spacing:8) {
                        PokemonSprite(name:member.entry.sprite,portrait:member.entry.portrait).frame(width:58,height:58)
                        Text(member.entry.title).font(.callout.bold()).lineLimit(1)
                        Text("\(index+1) · Lv. \(member.entry.level)").font(.caption).foregroundStyle(.secondary)
                    }.frame(maxWidth:.infinity).padding(.vertical,12).background(.quaternary.opacity(0.4),in:RoundedRectangle(cornerRadius:16))
                }
            }
            Picker("Save to",selection:$destination) {
                Text("A new journal team").tag("new")
                ForEach(available){team in Text("\(team.name) · \(team.members.count)/6").tag(team.id)}
            }
            if destination=="new" {
                TextField("Team name",text:$name).textFieldStyle(.roundedBorder)
                JournalStylePicker(style:$style,emblem:$emblem)
            } else {Text("The party will be added after this team's existing companions.").font(.callout).foregroundStyle(.secondary)}
            Text("Creates companion pages with Pokémon file snapshots in party order. Existing pages and teams keep their saved snapshots. Only teams with enough free places are listed.").font(.callout).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
            if model.state.pending || model.fieldDrafts {Label("This captures the party as stored in the workspace. Apply any Pokémon editor changes to a slot first to include them.",systemImage:"info.circle").font(.caption).foregroundStyle(.secondary)}
            if let error{Text(error).foregroundStyle(.red)}
            HStack {Text("Your game save stays unchanged.").font(.caption).foregroundStyle(.secondary);Spacer();Button("Save Party as Team"){save()}.buttonStyle(.borderedProminent).disabled(capture.members.isEmpty || (destination=="new" && name.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty))}
        }.padding(26).frame(width:760).onAppear{name="My \(capture.game) team"}
    }
    private func save() {
        do {
            let team:JournalTeam
            if destination=="new" {team=JournalTeam(name:name.trimmingCharacters(in:.whitespacesAndNewlines),style:style,emblem:emblem)}
            else {guard let existing=journals.teams[destination] else{throw JournalError("Choose an existing team or create a new one.")};team=existing}
            let saved=try journals.saveParty(capture.members,to:team)
            model.status="Saved \(saved.name) to My Journal";dismiss()
        }catch{self.error=error.localizedDescription}
    }
}
