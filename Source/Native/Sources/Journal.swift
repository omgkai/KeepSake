import SwiftUI
import AppKit
import UniformTypeIdentifiers

struct JournalSelection:Identifiable {let id:String;let fallback:PokemonJournal}
extension EditorState {
    var journalSelection:JournalSelection {JournalSelection(id:entityJournalKey,fallback:PokemonJournal(species:entityName,nickname:entityNickname,sprite:entitySprite,game:originGame,level:entityLevel,pokemonData:entityData.isEmpty ? nil:entityData,pokemonExtension:entityExtension,portrait:entityPortrait))}
}
struct JournalEditor:View {
    @EnvironmentObject var journals:JournalStore
    @Environment(\.dismiss) private var dismiss
    let selection:JournalSelection
    @State private var draft=PokemonJournal()
    @State private var error:String?
    var body:some View {ScrollView{VStack(alignment:.leading,spacing:18) {
        HStack{Label("A little more yours",systemImage:"book.closed.fill").font(.title2.bold());Spacer();Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction);Button("Save Journal"){do{try journals.save(draft,for:selection.id);dismiss()}catch{self.error=error.localizedDescription}}.buttonStyle(.borderedProminent).keyboardShortcut(.defaultAction)}
        JournalCard(entry:draft)
        HStack {TextField("KeepSake display name",text:$draft.displayName).textFieldStyle(.roundedBorder);Toggle("Favorite",isOn:$draft.favorite).toggleStyle(.button)}
        Picker("Card style",selection:$draft.style){ForEach(["Midnight","Meadow","Sunset","Aurora"],id:\.self){Text($0)}}.pickerStyle(.segmented)
        JournalCustomColors(first:$draft.accentHex,second:$draft.companionHex,style:draft.style)
        HStack{Text("Our story").font(.headline);Spacer();Text("Started "+draft.created.formatted(date:.abbreviated,time:.omitted)).font(.caption).foregroundStyle(.secondary)}
        TextEditor(text:$draft.notes).font(.body).padding(8).background(.quaternary,in:RoundedRectangle(cornerRadius:12)).frame(minHeight:150)
        HStack {Text("Saved only in KeepSake. Your Pokémon name, stats, legality and exports stay unchanged.").font(.caption).foregroundStyle(.secondary);Spacer();Text("\(draft.notes.count)/20,000").font(.caption.monospacedDigit()).foregroundStyle(draft.notes.count>20000 ? .red : .secondary)}
        if let error {Text(error).font(.caption).foregroundStyle(.red)}
    }.padding(24)}.frame(width:660,height:750).onAppear{draft=journals.entries[selection.id] ?? selection.fallback;draft.species=selection.fallback.species;draft.nickname=selection.fallback.nickname;draft.sprite=selection.fallback.sprite;draft.portrait=selection.fallback.portrait;draft.game=selection.fallback.game;draft.level=selection.fallback.level;if let data=selection.fallback.pokemonData{draft.pokemonData=data;draft.pokemonExtension=selection.fallback.pokemonExtension}}.alert("Journal could not be saved",isPresented:Binding(get:{journals.error != nil},set:{if !$0{journals.error=nil}})){Button("OK"){journals.error=nil}}message:{Text(journals.error ?? "")}}
}
struct JournalCard:View {
    let entry:PokemonJournal
    private var colors:[Color] {journalColors(entry.style,entry.accentHex,entry.companionHex)}
    var body:some View {HStack(spacing:20) {
        PokemonSprite(name:entry.sprite,portrait:entry.portrait).frame(width:100,height:100).padding(14).background(.white.opacity(0.09),in:RoundedRectangle(cornerRadius:24))
        VStack(alignment:.leading,spacing:10){HStack{Text("KEEPSAKE · MY COMPANION").font(.system(size:10,weight:.bold,design:.rounded)).tracking(2);Spacer();if entry.favorite{Image(systemName:"star.fill").foregroundStyle(Color(hex:"E3C576"))}}
            Text(entry.displayName.isEmpty ? (entry.nickname.isEmpty ? entry.species : entry.nickname) : entry.displayName).font(.system(size:29,weight:.bold,design:.rounded)).lineLimit(2)
            Text(entry.game=="Personal journal" ? entry.species:entry.species+" · Lv. \(entry.level)").font(.callout).foregroundStyle(.white.opacity(0.8))
            Text(entry.game).font(.caption).foregroundStyle(.white.opacity(0.7))
        }
    }.padding(24).foregroundStyle(.white).background(LinearGradient(colors:colors,startPoint:.topLeading,endPoint:.bottomTrailing),in:RoundedRectangle(cornerRadius:26)).overlay(RoundedRectangle(cornerRadius:26).stroke(Color(hex:"DFC480").opacity(0.6),lineWidth:1))}
}
struct JournalLibrary:View {
    @EnvironmentObject var model:EditorModel
    @EnvironmentObject var journals:JournalStore
    @State private var search=""
    @State private var favoritesOnly=false
    @State private var tab="Companions"
    @State private var selection:JournalSelection?
    @State private var team:JournalTeam?
    @State private var partyCapture:JournalPartyCapture?
    @State private var transferring:JournalTeam?
    @State private var adding=false
    @State private var customizing=false
    @State private var pending:JournalSelection?
    @State private var error:String?
    private var keys:[String] {journals.entries.keys.filter{key in guard let e=journals.entries[key] else{return false};return (!favoritesOnly||e.favorite) && (search.isEmpty||[e.displayName,e.nickname,e.species,e.notes].contains{$0.localizedCaseInsensitiveContains(search)})}.sorted{journals.entries[$0]!.updated>journals.entries[$1]!.updated}}
    private var teams:[JournalTeam] {journals.teams.values.filter{search.isEmpty || [$0.name,$0.notes].contains{$0.localizedCaseInsensitiveContains(search)}}.sorted{$0.updated>$1.updated}}
    var body:some View {VStack(spacing:20){
        VStack(alignment:.leading,spacing:18){
            HStack{Label("My journal",systemImage:"book.closed.fill").font(.title2.bold());Spacer();Button{partyCapture=model.state.journalParty}label:{Label("Save Party as Team",systemImage:"person.3.fill")}.disabled(!model.state.hasSave || model.state.partySlots.allSatisfy{$0.empty} || model.busy);Button("Customize Cover"){customizing=true};Menu {Button("Export Journal Backup…"){exportBackup()};Button("Import Journal Backup…"){importBackup()}}label:{Label("Backup",systemImage:"externaldrive")}}
            JournalCoverView(cover:journals.cover,companions:journals.entries.count,teams:journals.teams.count)
            HStack{Picker("Pages",selection:$tab){Text("Companions").tag("Companions");Text("Teams").tag("Teams")}.pickerStyle(.segmented).labelsHidden().frame(width:240);TextField("Search names and memories…",text:$search).textFieldStyle(.roundedBorder);if tab=="Companions"{Toggle("Favorites",isOn:$favoritesOnly).toggleStyle(.button)};Button{if tab=="Companions"{adding=true}else{team=JournalTeam()}}label:{Label(tab=="Companions" ? "Add Pokémon":"Create Team",systemImage:"plus")}.buttonStyle(.borderedProminent)}
        }.frame(maxWidth:1100)
        if tab=="Companions" && keys.isEmpty {
            WorkspaceEmptyState(title:journals.entries.isEmpty ? "Your story starts here":"No matching companions",icon:"heart.text.square",message:journals.entries.isEmpty ? "Add a Pokémon, write a memory, and build a team to remember.":"Try another search or turn off Favorites.") {Button("Add Pokémon"){adding=true}.buttonStyle(.borderedProminent)}
        } else if tab=="Teams" && teams.isEmpty {
            WorkspaceEmptyState(title:journals.teams.isEmpty ? "Adventures are better together":"No matching teams",icon:"person.3.fill",message:"Gather up to six journal companions, choose their order, and give the team its own story and style."){Button("Create Team"){team=JournalTeam()}.buttonStyle(.borderedProminent)}
        } else {ScrollView{LazyVGrid(columns:[GridItem(.adaptive(minimum:400))],spacing:20){if tab=="Companions"{ForEach(keys,id:\.self){key in if let entry=journals.entries[key]{Button{selection=JournalSelection(id:key,fallback:entry)}label:{JournalCard(entry:entry)}.buttonStyle(.plain).contextMenu{Button("Use This Journal for Current Pokémon"){link(entry)}.disabled(model.state.entityJournalKey.isEmpty)}}}}else{ForEach(teams){value in VStack(alignment:.leading,spacing:10){Button{team=value}label:{JournalTeamCard(team:value)}.buttonStyle(.plain);Button{transferring=value}label:{Label("Prepare for Game…",systemImage:"arrow.down.doc")}.disabled(!model.state.hasSave || value.members.isEmpty || model.fieldDrafts)}}}}.padding(.bottom,16).frame(maxWidth:1100).frame(maxWidth:.infinity)}}
        Text("Memories and styles stay in KeepSake. Prepare a team to export Pokémon or add them to your game.").font(.caption).foregroundStyle(.secondary)
        if let error=error ?? journals.error{Text(error).foregroundStyle(.red)}
    }.padding(28).frame(maxWidth:.infinity,maxHeight:.infinity)
        .sheet(item:$selection){JournalEditor(selection:$0)}
        .sheet(item:$team){JournalTeamEditor(team:$0)}
        .sheet(item:$partyCapture){JournalPartySheet(capture:$0)}
        .sheet(item:$transferring){GenerationSheet(team:$0)}
        .sheet(isPresented:$customizing){JournalCoverEditor()}
        .sheet(isPresented:$adding,onDismiss:{if let value=pending{pending=nil;selection=value}}){JournalAddPokemon{pending=$0}}
    }
    private func link(_ entry:PokemonJournal){do{var linked=entry;let current=model.state.journalSelection.fallback;linked.species=current.species;linked.nickname=current.nickname;linked.sprite=current.sprite;linked.portrait=current.portrait;linked.game=current.game;linked.level=current.level;linked.pokemonData=current.pokemonData;linked.pokemonExtension=current.pokemonExtension;try journals.save(linked,for:model.state.entityJournalKey)}catch{self.error=error.localizedDescription}}
    private func exportBackup(){let p=NSSavePanel();p.nameFieldStringValue="KeepSake Journals.json";p.allowedContentTypes=[.json];guard p.runModal() == .OK,let url=p.url else{return};do{try journals.exportData().write(to:url,options:.atomic)}catch{self.error=error.localizedDescription}}
    private func importBackup(){let p=NSOpenPanel();p.allowedContentTypes=[.json];guard p.runModal() == .OK,let url=p.url else{return};do{try journals.merge(Data(contentsOf:url))}catch{self.error=error.localizedDescription}}
}
