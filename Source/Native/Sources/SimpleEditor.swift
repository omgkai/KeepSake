import SwiftUI

struct SimplePokemonEditor: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    let tab: String
    @State private var showRecords=false
    @State private var showPlus=false
    @State private var showTraining=false
    var body: some View {
        ScrollView {
            VStack(alignment:.leading, spacing:18) {
                switch tab {
                case "Main": main
                case "Stats": stats
                case "Moves": moves
                case "Met": met
                default: trainer
                }
            }.padding(20)
        }.frame(maxWidth:.infinity, maxHeight:.infinity, alignment:.topLeading)
        .sheet(isPresented:$showPlus) { PlusRecordsView() }
        .sheet(isPresented:$showRecords) { MoveRecordsView() }
        .sheet(isPresented:$showTraining) { SuperTrainingView() }
    }
    private var main: some View {
        VStack(alignment:.leading, spacing:18) {
            ScrollView(.horizontal,showsIndicators:false) {HStack(spacing:12) {
                QuickFlag(id:"IsShiny", title:"Shiny", icon:"sparkles")
                QuickFlag(id:"IsEgg", title:"Egg", icon:"oval.fill")
                if model.state.fields.contains(where: { $0.id == "IsAlpha" && $0.editable }) {
                    QuickFlag(id:"IsAlpha", title:"Alpha", icon:"a.circle.fill")
                }
                PokerusQuickStatus()
            }}.padding(12).background(theme.accent.opacity(0.07), in:RoundedRectangle(cornerRadius:12))
            EditorCard(title:"Pokémon") {
                SimpleField(id:"Species", title:"Species")
                SimpleField(id:"Nickname", title:"Nickname")
                SimpleField(id:"Form", title:"Form", lookup:"forms")
                HStack(spacing:20) {
                    SimpleField(id:"CurrentLevel", title:"Level", compact:true)
                    SimpleField(id:"Gender", title:"Gender", options:genders)
                }
            }
            if model.state.fields.contains(where:{["Nature","Ability","HeldItem"].contains($0.id) && $0.editable}) {EditorCard(title:"Details") {
                SimpleField(id:"Nature", title:"Nature")
                SimpleField(id:"Ability", title:"Ability", lookup:"entityAbilities")
                if !model.state.abilityDescription.isEmpty {Text(model.state.abilityDescription).font(.callout).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)}
                SimpleField(id:"HeldItem", title:"Held item")

            }
            }
            GrowthCards()
            Text("Type freely, then press Return or Set to Slot. Use More for every detailed field.").font(.caption).foregroundStyle(.secondary)
        }
    }
    private var genders: [Choice] { [Choice(value:"0",label:"Male ♂"),Choice(value:"1",label:"Female ♀"),Choice(value:"2",label:"Genderless")] }
    private var met: some View {
        VStack(spacing:18) {
            HStack(spacing:16) {
                GameAsset(folder:"Balls",name:"_ball"+(model.state.fields.first{$0.id=="Ball"}?.value ?? "4"),fallback:"circle.fill").frame(width:48,height:48)
                VStack(alignment:.leading,spacing:5){Label("The beginning of your story",systemImage:"map.fill").font(.headline);Text(model.state.originGame).foregroundStyle(.secondary)}
                Spacer()
                SaveGameLogo(version:model.state.originVersion,name:model.state.originGame)
            }.padding(18).background(theme.accent.opacity(0.13),in:RoundedRectangle(cornerRadius:16))
            EditorCard(title:"Encounter") {
                SimpleField(id:"Version", title:"Origin game")
                SimpleField(id:"Ball", title:"Poké Ball")
                SimpleField(id:"MetLocation", title:"Met location")
                SimpleField(id:"MetLevel", title:"Met level")
                SimpleField(id:"MetDate", title:"Met date")
                SimpleField(id:"FatefulEncounter", title:"Fateful encounter")
            }
            EditorCard(title:"Egg encounter") {
                SimpleField(id:"EggLocation", title:"Egg location")
                SimpleField(id:"EggMetDate", title:"Egg date")
            }
        }
    }
    private var trainer: some View {
        VStack(spacing:18) {
            EditorCard(title:"Original trainer") {
                SimpleField(id:"OriginalTrainerName", title:"Name")
                SimpleField(id:"OriginalTrainerGender", title:"Gender", options:Array(genders.prefix(2)))
                SimpleField(id:"DisplayTID", title:"Trainer ID")
                SimpleField(id:"DisplaySID", title:"Secret ID")
                SimpleField(id:"Language", title:"Language", options:[Choice(value:"1",label:"Japanese"),Choice(value:"2",label:"English"),Choice(value:"3",label:"French"),Choice(value:"4",label:"Italian"),Choice(value:"5",label:"German"),Choice(value:"7",label:"Spanish"),Choice(value:"8",label:"Korean"),Choice(value:"9",label:"Chinese (Simplified)"),Choice(value:"10",label:"Chinese (Traditional)")])
            }
            EditorCard(title:"Handling trainer") {
                SimpleField(id:"HandlingTrainerName", title:"Name")
                SimpleField(id:"HandlingTrainerGender", title:"Gender", options:Array(genders.prefix(2)))
                SimpleField(id:"HandlingTrainerFriendship", title:"Friendship")
                SimpleField(id:"CurrentHandler", title:"Current handler", options:[Choice(value:"0",label:"Original trainer"),Choice(value:"1",label:"Handling trainer")])
            }
        }
    }
    private var hasGrit: Bool { model.state.fields.contains { $0.id == "GV_HP" && $0.editable } }
    private var hasAV: Bool { model.state.fields.contains { $0.id == "AV_HP" && $0.editable } }
    private var effortPrefix: String { hasGrit ? "GV_" : hasAV ? "AV_" : "EV_" }
    private let statRows: [(String,String,Int)] = [("HP","HP",0),("Attack","ATK",1),("Defense","DEF",2),("Sp. Atk","SPA",4),("Sp. Def","SPD",5),("Speed","SPE",3)]
    private var stats: some View {
        VStack(alignment:.leading, spacing:18) {
            if model.state.superTraining != nil {
                HStack {Text("Training history").font(.caption).foregroundStyle(.secondary);Spacer();Button("Super Training…"){showTraining=true}.disabled(model.fieldDrafts)}
            }
            StatsProfile()
            if model.state.fields.contains(where:{$0.id=="IsAlpha" && $0.editable}) {
                HStack {QuickFlag(id:"IsAlpha",title:"Alpha",icon:"eye.fill");if model.state.fields.contains(where:{$0.id=="IsNoble" && $0.editable}){QuickFlag(id:"IsNoble",title:"Noble",icon:"crown.fill")};Spacer()}
            }
            EditorCard(title:"Training") {
                ViewThatFits(in:.horizontal) {
                    HStack(spacing:12) {trainingButtons("IVs",suffix:"IV");Divider().frame(height:20);if !hasGrit && !hasAV {trainingButtons("EVs",suffix:"EV")} else if hasGrit {Button("Max Grit"){action("maxGrit")}}}
                    VStack(alignment:.leading,spacing:10) {trainingButtons("IVs",suffix:"IV");if !hasGrit && !hasAV {trainingButtons("EVs",suffix:"EV")} else if hasGrit {Button("Max Grit"){action("maxGrit")}}}
                }.controlSize(.small).disabled(model.fieldDrafts)

            }
            if model.state.generation>=3 {EditorCard(title:"Personality") {
                HStack {SimpleField(id:"PID",title:"Personality ID (PID)");Button {action("rerollPID")} label:{Image(systemName:"arrow.trianglehead.2.clockwise.rotate.90")}.help("Reroll PID using PKHeX. Produces a non-shiny PID; review legality afterward.").accessibilityLabel("Reroll PID").disabled(model.fieldDrafts)}
                if let raw=model.state.fields.first(where:{$0.id=="PID"})?.value,let pid=UInt32(raw){Text(String(format:"Hexadecimal · %08X",pid)).font(.caption.monospaced()).foregroundStyle(.secondary)}
            }}
            EditorCard(title:"Nature") {
                SimpleField(id:"Nature", title:"Nature")
                SimpleField(id:"StatAlignment", title:"Mint nature")
                if !model.state.characteristic.isEmpty {Text("Characteristic").font(.caption).foregroundStyle(.secondary);Label(model.state.characteristic,systemImage:"quote.bubble.fill").foregroundStyle(theme.accent).padding(.top,4)}
            }
            if model.state.fields.contains(where:{$0.id=="TeraTypeOriginal"}) {
                EditorCard(title:"Tera types") {
                    ForEach(["TeraTypeOriginal","TeraTypeOverride"],id:\.self){id in
                        if let field=model.state.fields.first(where:{$0.id==id}),let info=PokemonExtraField.catalog[id] {SimpleField(id:id,title:info.title,options:info.choices(field))}
                    }
                }
            }
            if hasGrit { Text("Grit is the stored value, as in PKHeX. The IV bonus also contributes to effort levels.").font(.caption).foregroundStyle(.secondary) }
            Text("Totals refresh when you commit edits. More has extra game settings and the full technical field list.").font(.caption).foregroundStyle(.secondary)
        }
    }
    private var moves: some View {
        VStack(alignment:.leading, spacing:18) {
            EditorCard(title:"Current moves") {
                HStack { SuggestMovesButton(); Spacer() }
                if !model.state.suggestionMessage.isEmpty {
                    Label(model.state.suggestionMessage,systemImage:"info.circle.fill").font(.callout).foregroundStyle(theme.accent)
                        .padding(10).frame(maxWidth:.infinity,alignment:.leading).background(theme.accent.opacity(0.09),in:RoundedRectangle(cornerRadius:9))
                }
                Text("Type colors identify moves. Green means legal; red explains a problem.").font(.caption).foregroundStyle(.secondary)
                HStack { Text("Move"); Spacer(); Text("PP").frame(width:48); Text("PP Ups").frame(width:48) }.font(.caption).foregroundStyle(.secondary)
                ForEach(1...4, id:\.self) { i in
                    HStack(spacing:10) {
                        MovePicker(slot:i)
                        StatInput(id:"Move\(i)_PP", title:"Move \(i) PP").frame(width:48)
                        StatInput(id:"Move\(i)_PPUps", title:"Move \(i) PP Ups").frame(width:48)
                    }
                    MoveStatusView(slot:i)
                }
                if model.state.canPlusRecords {Button("Move-Plus Records…"){showPlus=true}.disabled(model.fieldDrafts)}
                if model.state.canMoveRecords {
                    Button(model.state.entityExtension == "pa8" ? "Move Shop & Mastery…" : "Technical Records (TM / TR)…") { showRecords=true }.disabled(model.fieldDrafts)
                }
            }
            if model.state.entityExtension == "pa8" {
                DisclosureGroup("Alpha encounter details") {
                    VStack(alignment:.leading,spacing:10) {
                        SimpleField(id:"AlphaMove", title:"Encounter move", lookup:"moves")
                        Text("The special move recorded when this Alpha was encountered in Legends: Arceus. It should match its encounter and mastery records. This is separate from its four current moves. To toggle Alpha status, use the Alpha switch on Main.").font(.caption).foregroundStyle(.secondary)
                    }.padding(.top,10)
                }.font(.callout)
            }
            if model.state.fields.contains(where:{$0.id == "RelearnMove1" && $0.editable}) {
            EditorCard(title:"Relearn moves") {
                SuggestMovesButton(relearn:true)
                ForEach(1...4, id:\.self) { i in
                    MovePicker(slot:i,relearn:true)
                    MoveStatusView(slot:i,relearn:true)
                }
            }
            }
        }
    }
    private func trainingButtons(_ title:String,suffix:String)->some View {HStack(spacing:5){Text(LocalizedStringKey(title)).font(.caption.bold());ForEach(["Max","Random","Clear"],id:\.self){label in Button(LocalizedStringKey(label)){action(label.lowercased()+suffix)}.help(label+" "+title)}}}
    private func action(_ name:String) { Task { await model.command(["op":"entityAction", "action":name]) } }
}

struct EditorCard<Content:View>: View {
    @Environment(\.gameTheme) private var theme
    let title:String
    @ViewBuilder let content:Content
    var body: some View {
        VStack(alignment:.leading,spacing:12) {
            Text(LocalizedStringKey(title)).textCase(.uppercase).font(.system(size:10,weight:.semibold)).tracking(1).foregroundStyle(.secondary)
            content
        }.padding(16).frame(maxWidth:.infinity,alignment:.leading)
            .background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:12))
            .overlay(RoundedRectangle(cornerRadius:12).strokeBorder(.primary.opacity(0.045)))
    }
}
struct QuickFlag: View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    let id:String, title:String, icon:String
    var body: some View {
        if let field = model.state.fields.first(where:{$0.id == id}), field.editable || id == "IsShiny" {
            Toggle(isOn:Binding(get:{field.value == "true"},set:{new in Task { await model.editPokemon(id,value:new ? "true" : "false") } })) {
                HStack(spacing:6){PokemonEmblem(kind:id);Text(LocalizedStringKey(title)).font(.system(size:12,weight:.medium))}
            }.toggleStyle(.switch).controlSize(.small).fixedSize()
                .accessibilityLabel(title).help(id == "IsAlpha" ? "Alpha Pokémon — the same Alpha flag used by PKHeX" : id == "IsNoble" ? "Noble encounter flag. PKHeX marks Noble Pokémon as invalid for normal player ownership." : title)
        }
    }
}
struct StatInput:View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    let id:String, title:String
    var body:some View {
        if let field = model.state.fields.first(where:{$0.id == id && $0.editable}) {
            TextField(title,text:Binding(get:{model.value(field,target:"entity")},set:{model.draft($0,field:field,target:"entity")}))
                .textFieldStyle(.roundedBorder).multilineTextAlignment(.trailing).font(.system(.body,design:.monospaced))
                .accessibilityLabel(title).onSubmit { Task { await model.editPokemon() } }
        } else { Text("—").foregroundStyle(.tertiary) }
    }
}
struct SimpleField:View {
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model:EditorModel
    let id:String, title:String
    var lookup:String? = nil
    var options:[Choice] = []
    var compact = false
    var hideLabel = false
    @State private var choicePresentation: ChoicePresentation?
    @State private var loaded:[Choice] = []
    private var field:Field? { model.state.fields.first {$0.id == id && ($0.editable || lookup == "entityAbilities" && !["pk1", "pk2"].contains(model.state.entityExtension))} }
    private var choiceContext: String {
        let species = model.state.fields.first { $0.id == "Species" }?.value ?? ""
        let version = model.state.fields.first { $0.id == "Version" }?.value ?? ""
        let form = model.state.fields.first { $0.id == "Form" }?.value ?? ""
        return species + "|" + form + "|" + version + "|" + model.state.entityExtension
    }
    var body:some View {
        if let field {
            HStack(spacing:12) {
                if !hideLabel { Text(LocalizedStringKey(title)).font(.callout).foregroundStyle(.secondary).frame(width:compact ? 40 : 94,alignment:.leading) }
                let kind = lookup ?? field.lookup
                let choices = !options.isEmpty ? options : kind.flatMap { model.catalogs[$0] } ?? (!loaded.isEmpty ? loaded : field.choices)
                let storedValue = model.value(field,target:"entity")
                let number = Int(model.state.fields.first { $0.id == "AbilityNumber" }?.value ?? "1") ?? 1
                let value = kind == "entityAbilities" ? storedValue + ":" + String(number == 4 ? 2 : number == 2 ? 1 : 0) : storedValue
                let displayLabel = choices.first(where:{$0.value == value})?.label ?? (kind == "entityAbilities" ? (model.catalogs["abilities"]?.first(where:{$0.value == storedValue})?.label ?? storedValue) + " · Stored ability" : id == "Form" && value == "0" ? "Normal" : value)
                if kind != nil || !choices.isEmpty {
                    Button {
                        Task {
                            do {
                                let options: [Choice]
                                if let kind { options = try await model.fetchChoices(kind) }
                                else { options = choices }
                                loaded = options
                                choicePresentation = ChoicePresentation(title:title, options:options, selected:value)
                            } catch { model.error = error.localizedDescription }
                        }
                    } label: {
                        HStack { if id.contains("Gender") {GenderBadge(value:Int(value) ?? 2)} else {Text(displayLabel).lineLimit(1)}; Spacer(minLength:3); Image(systemName:"chevron.up.chevron.down").font(.system(size:9,weight:.semibold)).foregroundStyle(.secondary) }
                        .frame(maxWidth:.infinity,alignment:.leading).padding(.horizontal,8).padding(.vertical,5)
                        .background(.quaternary.opacity(0.5),in:RoundedRectangle(cornerRadius:6))
                    }.buttonStyle(.plain).accessibilityLabel(title + ": " + displayLabel)
                    .popover(item:$choicePresentation, arrowEdge:.bottom) { presentation in
                        ChoicePopover(presentation:presentation) { choice in
                            choicePresentation = nil
                            Task { await model.editPokemon(kind == "entityAbilities" ? "AbilityChoice" : id,value:choice.value) }
                        }
                    }
                    .task(id:choiceContext) { await refreshChoices(kind) }
                } else if field.kind == "bool" {
                    Toggle(title,isOn:Binding(get:{value == "true"},set:{new in Task { await model.editPokemon(id,value:new ? "true" : "false") } })).labelsHidden().toggleStyle(.switch).controlSize(.small)
                    Spacer()
                } else {
                    TextField(field.kind == "date" ? "yyyy-mm-dd" : title,text:Binding(get:{value},set:{model.draft($0,field:field,target:"entity")}))
                        .textFieldStyle(.roundedBorder).accessibilityLabel(title)
                        .onSubmit { Task { await model.editPokemon() } }
                }
            }.frame(minHeight:25)
        }
    }
    private func refreshChoices(_ kind: String?) async {
        guard let kind, ["forms", "met", "eggMet", "entityAbilities"].contains(kind) else { return }
        let context = choiceContext
        let choices = await model.choices(kind)
        guard !Task.isCancelled, context == choiceContext else { return }
        loaded = choices
    }
}
struct ChoicePopover: View {
    @Environment(\.gameTheme) private var theme
    let presentation: ChoicePresentation
    let choose: (Choice) -> Void
    @State private var search = ""
    private var filtered: [Choice] { presentation.options.filter { search.isEmpty || $0.label.localizedCaseInsensitiveContains(search) || $0.value == search } }
    var body: some View {
        VStack(spacing:8) {
            TextField("Search \(presentation.title.lowercased())",text:$search).textFieldStyle(.roundedBorder).padding([.horizontal,.top],12)
                .onSubmit { if filtered.count == 1 { choose(filtered[0]) } }
            ChoiceRows(options:filtered, selected:presentation.selected, choose:choose)
                .overlay {
                    if filtered.isEmpty { Text(presentation.options.isEmpty ? "No choices are available for this field." : "No matches. Try another name or ID.").foregroundStyle(.secondary).padding() }
                }
            Text("\(filtered.count) choices").font(.caption).foregroundStyle(.secondary).padding(.bottom,8)
        }.frame(width:300,height:330)
    }
}
