import SwiftUI

func journalColors(_ style:String,_ first:String?=nil,_ second:String?=nil)->[Color] {
    if let first,let second{return [Color(hex:first),Color(hex:second)]}
    switch style {case "Meadow":return [Color(hex:"234C40"),Color(hex:"466957")];case "Sunset":return [Color(hex:"663E59"),Color(hex:"B77361")];case "Aurora":return [Color(hex:"343D70"),Color(hex:"356C78")];default:return [Color(hex:"142640"),Color(hex:"304A68")]}
}
struct JournalCoverView:View {
    let cover:JournalCover
    let companions:Int,teams:Int
    var body:some View {HStack(spacing:24){Image(systemName:cover.emblem).font(.system(size:38,weight:.light)).foregroundStyle(Color(hex:"E3C576")).frame(width:86,height:100).background(.white.opacity(0.06),in:RoundedRectangle(cornerRadius:16));VStack(alignment:.leading,spacing:10){Text("K E E P S A K E  /  V O L .  0 1").font(.system(size:10,weight:.semibold,design:.monospaced)).foregroundStyle(Color(hex:"E3C576"));Text(cover.title).font(.system(size:30,weight:.semibold,design:.serif)).lineLimit(2);if !cover.subtitle.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty {Text(cover.subtitle).font(.callout).foregroundStyle(.white.opacity(0.75)).lineLimit(3)};Text("\(companions) \(companions == 1 ? "companion":"companions")  ·  \(teams) \(teams == 1 ? "team":"teams")").font(.caption).foregroundStyle(.white.opacity(0.6))};Spacer(minLength:0)}.padding(26).foregroundStyle(.white).frame(maxWidth:.infinity,alignment:.leading).background(LinearGradient(colors:journalColors(cover.style,cover.accentHex,cover.companionHex),startPoint:.topLeading,endPoint:.bottomTrailing),in:RoundedRectangle(cornerRadius:24)).overlay(RoundedRectangle(cornerRadius:24).stroke(Color(hex:"E3C576").opacity(0.45),lineWidth:1))}
}
struct JournalTeamCard:View {
    @EnvironmentObject var journals:JournalStore
    let team:JournalTeam
    var body:some View {VStack(alignment:.leading,spacing:16){HStack{Image(systemName:team.emblem).foregroundStyle(Color(hex:"E3C576"));Text(team.name).font(.title3.bold()).lineLimit(1);Spacer();Text("\(team.members.count)/6").font(.caption)};HStack(spacing:4){ForEach(0..<6,id:\.self){i in Group{if i<team.members.count,let e=journals.entries[team.members[i]]{PokemonSprite(name:e.sprite,portrait:e.portrait).accessibilityLabel(e.title)}else{Image(systemName:"plus").foregroundStyle(.white.opacity(0.3))}}.frame(maxWidth:.infinity).frame(height:48).background(.white.opacity(0.06),in:RoundedRectangle(cornerRadius:12))}};Text(team.notes.isEmpty ? "Add team notes…" : team.notes).font(.callout).foregroundStyle(.white.opacity(0.7)).lineLimit(2).frame(height:36,alignment:.top)}.padding(20).foregroundStyle(.white).background(LinearGradient(colors:journalColors(team.style,team.accentHex,team.companionHex),startPoint:.topLeading,endPoint:.bottomTrailing),in:RoundedRectangle(cornerRadius:22))}
}
struct JournalStylePicker:View {
    @Binding var style:String
    @Binding var emblem:String
    var body:some View {VStack(alignment:.leading,spacing:12){Picker("Palette",selection:$style){ForEach(journalStyles,id:\.self){Text($0).tag($0)}}.pickerStyle(.segmented);HStack{Text("Emblem").foregroundStyle(.secondary);Spacer();ForEach(journalEmblems,id:\.self){symbol in Button{emblem=symbol}label:{Image(systemName:symbol).frame(width:30,height:28).background(emblem==symbol ? Color.accentColor.opacity(0.2):.clear,in:RoundedRectangle(cornerRadius:8))}.buttonStyle(.plain).accessibilityLabel(symbol.replacingOccurrences(of:".fill",with:"").replacingOccurrences(of:".",with:" ")).accessibilityAddTraits(emblem==symbol ? .isSelected:[])}}}}
}
struct JournalCoverEditor:View {
    @EnvironmentObject var journals:JournalStore
    @Environment(\.dismiss) private var dismiss
    @State private var draft=JournalCover()
    @State private var error:String?
    var body:some View {
        ScrollView {
            VStack(alignment:.leading,spacing:20) {
                HStack {
                    Text("Customize Cover").font(.title2.bold())
                    Spacer()
                    Button("Cancel"){dismiss()}
                    Button("Save Cover") {
                        do {try journals.saveCover(draft);dismiss()}
                        catch {self.error=error.localizedDescription}
                    }.buttonStyle(.borderedProminent)
                }
                JournalCoverView(cover:draft,companions:journals.entries.count,teams:journals.teams.count)
                VStack(alignment:.leading,spacing:8) {
                    Text("Journal title").font(.headline)
                    TextField("Journal title",text:$draft.title).textFieldStyle(.roundedBorder)
                }
                VStack(alignment:.leading,spacing:8) {
                    HStack {
                        Text("Subtitle (optional)").font(.headline)
                        Spacer()
                        Button("Clear"){draft.subtitle=""}.disabled(draft.subtitle.isEmpty)
                    }
                    TextField("Write your own motto or dedication",text:$draft.subtitle).textFieldStyle(.roundedBorder)
                    Text("Leave blank for a title-only cover.").font(.caption).foregroundStyle(.secondary)
                }
                JournalStylePicker(style:$draft.style,emblem:$draft.emblem)
                JournalCustomColors(first:$draft.accentHex,second:$draft.companionHex,style:draft.style)
                if let error {Text(error).foregroundStyle(.red)}
            }.padding(26)
        }.frame(width:670,height:760).onAppear{draft=journals.cover}
    }
}
struct JournalTeamEditor:View {
    @EnvironmentObject var journals:JournalStore
    @Environment(\.dismiss) private var dismiss
    let team:JournalTeam
    @State private var draft=JournalTeam()
    @State private var search=""
    @State private var error:String?
    private var choices:[String] {journals.entries.keys.filter{search.isEmpty || journals.entries[$0]!.title.localizedCaseInsensitiveContains(search) || journals.entries[$0]!.species.localizedCaseInsensitiveContains(search)}.sorted{journals.entries[$0]!.title<journals.entries[$1]!.title}}
    var body:some View {
        VStack(spacing:0) {
            HStack(spacing:14) {
                Image(systemName:"book.closed.fill").font(.title2).foregroundStyle(Color(hex:"C9A75D"))
                VStack(alignment:.leading,spacing:3) {Text("Your team, your story").font(.system(.title2,design:.serif).weight(.semibold));Text("Six companions. A thousand memories.").font(.caption).foregroundStyle(.secondary)}
                Spacer()
                Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction)
                Button("Save Team"){do{try journals.saveTeam(draft);dismiss()}catch{self.error=error.localizedDescription}}.buttonStyle(.borderedProminent)
            }.padding(24)
            Divider()
            ScrollView {
                VStack(alignment:.leading,spacing:22) {
                    JournalTeamCard(team:draft)
                    HStack(alignment:.top,spacing:20) {
                        VStack(alignment:.leading,spacing:20) {
                            EditorCard(title:"Team identity") {
                                TextField("Give your team a name",text:$draft.name).textFieldStyle(.roundedBorder)
                                DisclosureGroup("Cover palette & emblem") {
                                    VStack(spacing:16) {JournalStylePicker(style:$draft.style,emblem:$draft.emblem);JournalCustomColors(first:$draft.accentHex,second:$draft.companionHex,style:draft.style)}.padding(.top,14)
                                }
                            }
                            EditorCard(title:"Lineup · \(draft.members.count)/6") {
                                if draft.members.isEmpty {emptyLineup}
                                ForEach(Array(draft.members.enumerated()),id:\.element){i,key in
                                    HStack(spacing:10) {
                                        Text(String(format:"%02d",i+1)).font(.caption.monospaced()).foregroundStyle(.secondary)
                                        PokemonSprite(name:journals.entries[key]?.sprite ?? "b_0",portrait:journals.entries[key]?.portrait).frame(width:38,height:38)
                                        VStack(alignment:.leading,spacing:2) {Text(journals.entries[key]?.title ?? "Companion").font(.callout.weight(.medium)).lineLimit(1);Text(journals.entries[key]?.species ?? "").font(.caption2).foregroundStyle(.secondary)}
                                        Spacer(minLength:2)
                                        Button{draft.members.swapAt(i,i-1)}label:{Image(systemName:"arrow.up")}.disabled(i==0).help("Move earlier in the lineup")
                                        Button{draft.members.remove(at:i)}label:{Image(systemName:"minus.circle")}.help("Remove from this team")
                                    }.padding(8).background(.quaternary.opacity(0.45),in:RoundedRectangle(cornerRadius:12))
                                }
                            }
                        }.frame(maxWidth:.infinity)
                        EditorCard(title:"Your companions") {
                            TextField("Find a companion…",text:$search).textFieldStyle(.roundedBorder)
                            ScrollView {
                                LazyVStack(spacing:7) {
                                    ForEach(choices,id:\.self){key in if let e=journals.entries[key] {
                                        Button{draft.members.append(key)}label:{HStack(spacing:10){PokemonSprite(name:e.sprite,portrait:e.portrait).frame(width:38,height:38);VStack(alignment:.leading,spacing:3){Text(e.title).font(.callout.weight(.medium)).lineLimit(1);Text(e.species).font(.caption2).foregroundStyle(.secondary)};Spacer();Image(systemName:draft.members.contains(key) ? "checkmark.circle.fill":"plus.circle.fill").foregroundStyle(Color.accentColor)}.padding(9).background(.quaternary.opacity(0.4),in:RoundedRectangle(cornerRadius:12))}.buttonStyle(.plain).disabled(draft.members.contains(key)||draft.members.count==6)
                                    }}
                                    if choices.isEmpty {VStack(spacing:12){Image(systemName:"leaf.fill").font(.largeTitle).foregroundStyle(.secondary);Text(journals.entries.isEmpty ? "Your next adventure starts here":"No companions found").font(.headline);Text(journals.entries.isEmpty ? "Add Pokémon to your journal, then bring them together in a team.":"Try another name or species.").font(.callout).foregroundStyle(.secondary).multilineTextAlignment(.center)}.padding(.vertical,36).frame(maxWidth:.infinity)}
                                }
                            }.frame(height:300)
                        }.frame(width:285)
                    }
                    EditorCard(title:"Team memories") {
                        Text("The first victory, the close calls, the places you found each other.").font(.caption).foregroundStyle(.secondary)
                        TextEditor(text:$draft.notes).scrollContentBackground(.hidden).padding(10).frame(height:110).background(.quaternary.opacity(0.4),in:RoundedRectangle(cornerRadius:12)).accessibilityLabel("Team memories")
                    }
                    Label("Your story stays in KeepSake. Prepare for Game lets you review a team before adding it to a save.",systemImage:"lock.shield").font(.caption).foregroundStyle(.secondary)
                    if let error{Text(error).foregroundStyle(.red)}
                }.padding(24)
            }
        }.frame(width:820,height:760).onAppear{draft=team}
    }
    private var emptyLineup:some View {VStack(spacing:10){Image(systemName:"person.3.sequence.fill").font(.title).foregroundStyle(.secondary);Text("Make room for your favorites").font(.callout.weight(.medium));Text("Choose up to six companions from your journal.").font(.caption).foregroundStyle(.secondary)}.frame(maxWidth:.infinity).padding(.vertical,24)}

}
struct JournalAddPokemon:View {
    @EnvironmentObject var model:EditorModel
    @EnvironmentObject var journals:JournalStore
    @Environment(\.dismiss) private var dismiss
    let choose:(JournalSelection)->Void
    @State private var species="25"
    @State private var shiny=false
    @State private var catalog:[Choice]=[]
    @State private var error:String?
    var body:some View {VStack(alignment:.leading,spacing:20){HStack{Text("Add a companion").font(.title2.bold());Spacer();Button("Cancel"){dismiss()}};Text("Keep a memory of a Pokémon you have, or start a journal page for one you love.").foregroundStyle(.secondary)
        if !model.state.entityJournalKey.isEmpty{Button{finish(model.state.journalSelection)}label:{Label("Add Current Pokémon · "+model.state.journalSelection.fallback.title,systemImage:"heart.fill")}.buttonStyle(.borderedProminent)}
        if model.state.hasSave {Text("Current box & party").font(.headline);ScrollView{LazyVGrid(columns:[GridItem(.adaptive(minimum:160))]){ForEach(model.state.partySlots.filter{!$0.empty}+model.state.slots.filter{!$0.empty}){slot in Button{finish(JournalSelection(id:slot.journalKey,fallback:PokemonJournal(species:slot.name,nickname:slot.nickname,sprite:slot.sprite,game:model.state.game,level:slot.level,pokemonData:slot.pokemonData,pokemonExtension:slot.pokemonExtension,portrait:slot.portrait)))}label:{HStack{PokemonSprite(name:slot.sprite,portrait:slot.portrait).frame(width:32,height:32);Text(slot.displayName).lineLimit(1)}.frame(maxWidth:.infinity,alignment:.leading).padding(8)}.buttonStyle(.bordered)}}}.frame(maxHeight:160)}
        Divider();Text("Create a personal page").font(.headline);HStack{PokemonSprite(name:"b_"+species+(shiny ? "s":"")).frame(width:60,height:60);CatalogChoiceButton(title:"Species",options:catalog,value:$species);Toggle("Shiny artwork",isOn:$shiny)};Button("Create Pokémon Page"){let key=(UUID().uuidString+UUID().uuidString).replacingOccurrences(of:"-",with:"").lowercased();finish(JournalSelection(id:key,fallback:PokemonJournal(species:catalog.first{$0.value==species}?.label ?? "Pokémon",sprite:"b_"+species+(shiny ? "s":""),game:"Personal journal",level:1)))}.disabled(catalog.isEmpty).buttonStyle(.borderedProminent);Text("Pages added from your save keep a Pokémon snapshot for team transfers. Personal pages can generate new Pokémon when you prepare a team. Your artwork and memories never change a save.").font(.caption).foregroundStyle(.secondary);if let error{Text(error).foregroundStyle(.red)}}.padding(26).frame(width:700).task{do{catalog=try await model.bridge.send(["op":"lookup","kind":"journalSpecies"],as:[Choice].self).filter{$0.value != "0"}}catch{self.error=error.localizedDescription}}}
    private func finish(_ value:JournalSelection){choose(value);dismiss()}
}
