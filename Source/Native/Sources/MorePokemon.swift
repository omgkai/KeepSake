import SwiftUI

struct MorePokemonView: View {
    @EnvironmentObject private var model: EditorModel
    let openTab: (String) -> Void
    @State private var search = ""
    @State private var showNavigation = false
    @State private var technical: TechnicalFieldRequest?
    private var sections: [PokemonExtraSection] { PokemonExtraSection.available(fields:model.state.fields, format:model.state.entityExtension) }
    private var query: String { search.trimmingCharacters(in:.whitespacesAndNewlines) }
    private var visible: [PokemonExtraSection] {
        sections.compactMap { section in
            let entries = section.entries.filter { query.isEmpty || (section.title + " " + $0.title + " " + $0.help + " " + $0.id).localizedCaseInsensitiveContains(query) }
            return entries.isEmpty ? nil : PokemonExtraSection(id:section.id,title:section.title,detail:section.detail,entries:entries)
        }
    }
    var body: some View {
        ScrollView {
            VStack(alignment:.leading,spacing:18) {
                VStack(alignment:.leading,spacing:6) {
                    Text("A few extra details").font(.title3.bold())
                    Text("Less common settings for this Pokémon. Open a section to see what each option does.").font(.callout).foregroundStyle(.secondary)
                }
                TextField("Find an extra setting…",text:$search).textFieldStyle(.roundedBorder)
                if query.isEmpty {
                    DisclosureGroup("Find settings in other tabs",isExpanded:$showNavigation) {
                        VStack(spacing:12) {
                            navigation("Main",title:"Appearance & identity",detail:"Species, nickname, Shiny, Egg and Alpha when supported.",icon:"sparkles")
                            navigation("Stats",title:"Stats & training",detail:"IVs, effort values, nature and Super Training.",icon:"chart.bar")
                            navigation("Moves",title:"Moves & move history",detail:"Current moves, relearn moves, mastery and encounter moves.",icon:"bolt")
                            navigation("Cosmetics",title:"Cosmetics & awards",detail:"Ribbons, markings, size, contest conditions and personal details.",icon:"rosette")
                        }.padding(.top,12)
                    }.font(.callout).padding(14).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:12))
                }
                ForEach(visible) { section in
                    ExtraSectionView(section:section, expandedForSearch:!query.isEmpty)
                }
                if visible.isEmpty {
                    Text(query.isEmpty ? "Other settings for this Pokémon are in the tabs above." : "No everyday settings match. You can search the full technical field list below.")
                        .font(.callout).foregroundStyle(.secondary)
                }
                Divider()
                VStack(alignment:.leading,spacing:8) {
                    HStack {
                        Label("Technical details",systemImage:"slider.horizontal.3").font(.headline)
                        Spacer()
                        Button(query.isEmpty ? "Open All Fields…" : "Search All Fields…") { technical = TechnicalFieldRequest(search:query) }
                    }
                    Text("PID, HOME tracker, transfer history and every other stored field are available here. Most edits only need the main tabs.").font(.caption).foregroundStyle(.secondary)
                }
                Text("Edits stay in the Pokémon editor until you use Set to Slot or export the Pokémon.").font(.caption).foregroundStyle(.secondary)
            }.padding(20)
        }.sheet(item:$technical) { request in TechnicalPokemonFields(initialSearch:request.search) }
    }
    private func navigation(_ tab:String,title:String,detail:String,icon:String) -> some View {
        Button { openTab(tab) } label: {
            HStack(alignment:.top,spacing:10) {
                Image(systemName:icon).frame(width:20).padding(.top,2)
                VStack(alignment:.leading,spacing:3) { Text(title).font(.callout.weight(.medium));Text(detail).font(.caption).foregroundStyle(.secondary) }
                Spacer(minLength:4)
                Image(systemName:"chevron.right").font(.caption)
            }.frame(maxWidth:.infinity,alignment:.leading).contentShape(Rectangle())
        }.buttonStyle(.plain).padding(.vertical,3)
    }
}

private struct TechnicalFieldRequest: Identifiable { let id = UUID(); let search:String }
private struct TechnicalPokemonFields: View {
    @EnvironmentObject private var model: EditorModel
    @Environment(\.dismiss) private var dismiss
    let initialSearch:String
    var body: some View {
        VStack(alignment:.leading,spacing:0) {
            HStack {
                VStack(alignment:.leading,spacing:5) {
                    Text("All Pokémon fields").font(.title2.bold())
                    Text("Exact stored values and internal data. Search by a familiar name or the original field name.").font(.callout).foregroundStyle(.secondary)
                }
                Spacer()
                Button("Done") { dismiss() }.keyboardShortcut(.cancelAction)
            }.padding(22)
            FieldList(fields:model.state.fields.map(PokemonExtraField.annotated),target:"entity",grouped:true,initialSearch:initialSearch)
        }.frame(width:750,height:650)
    }
}

private struct ExtraSectionView: View {
    @EnvironmentObject private var model: EditorModel
    let section:PokemonExtraSection
    let expandedForSearch:Bool
    @State private var expanded = false
    init(section:PokemonExtraSection,expandedForSearch:Bool) {
        self.section=section; self.expandedForSearch=expandedForSearch
        _expanded=State(initialValue:section.id == "personal")
    }
    var body: some View {
        VStack(alignment:.leading,spacing:8) {
            if expandedForSearch {
                Text(section.title).font(.headline)
                content
            } else {
                DisclosureGroup(isExpanded:$expanded) { content.padding(.top,8) } label: {
                    VStack(alignment:.leading,spacing:4) { Text(section.title).font(.callout.weight(.semibold));Text(section.detail).font(.caption).foregroundStyle(.secondary) }
                }
            }
        }.padding(14).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:12))
    }
    private var content: some View {
        VStack(alignment:.leading,spacing:12) {
            if expandedForSearch { Text(section.detail).font(.caption).foregroundStyle(.secondary) }
            ForEach(section.entries) { entry in
                if let field = model.state.fields.first(where:{$0.id == entry.id}) {
                    VStack(alignment:.leading,spacing:5) {
                        Text(entry.title).font(.callout.weight(.medium))
                        // Reuse the same underlying field id and transaction path as the regular editor.
                        SimpleField(id:entry.id,title:entry.title,options:entry.choices(field),hideLabel:true)
                        Text(entry.help).font(.caption).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
                    }
                    .accessibilityElement(children:.contain)
                }
            }
        }
    }
}

struct PokemonExtraSection: Identifiable {
    let id:String, title:String, detail:String
    let entries:[PokemonExtraField]
    static func available(fields:[Field],format:String) -> [Self] {
        let editable = Set(fields.filter(\.editable).map(\.id))
        func section(_ id:String,_ title:String,_ detail:String,_ keys:[String]) -> Self {
            Self(id:id,title:title,detail:detail,entries:keys.compactMap { key in editable.contains(key) ? PokemonExtraField.catalog[key] : nil })
        }
        var sections = [section("personal","Personal touches","A small preference saved with this Pokémon.",["IsFavorite"])]
        if ["pk7","pk8","pb8","pk9","pa9"].contains(format) {
            sections.append(section("hyper","Hyper Training","Choose which stats have been Hyper Trained. The original IVs stay unchanged.",["HT_HP","HT_ATK","HT_DEF","HT_SPA","HT_SPD","HT_SPE"]))
        }
        if format == "pk8" { sections.append(section("dynamax","Dynamax & Gigantamax","Battle traits used in Sword and Shield.",["DynamaxLevel","CanGigantamax"])) }
        if ["pk3","ck3","xk3","pk4","bk4","pk6","pb8"].contains(format) {
            sections.append(section("contest","Contest conditions","Coolness, beauty and other values used by Pokémon Contests.",["ContestCool","ContestBeauty","ContestCute","ContestSmart","ContestTough","ContestSheen"]))
        }
        if ["pk6","pk7"].contains(format) {
            sections.append(section("care","Affection & care","Stored care values from Pokémon-Amie or Pokémon Refresh.",["OriginalTrainerAffection","HandlingTrainerAffection","Fullness","Enjoyment"]))
        }
        return sections.filter { !$0.entries.isEmpty }
    }
}

struct PokemonExtraField: Identifiable {
    let id:String, title:String, help:String
    func choices(_ field:Field) -> [Choice] {
        if id == "TeraTypeOriginal" || id == "TeraTypeOverride" {
            var values = field.choices.filter { $0.value != "Any" }
            values.append(Choice(value:"99",label:"Stellar"))
            if id == "TeraTypeOverride" { values.insert(Choice(value:"19",label:"Use original type"),at:0) }
            return values
        }
        return []
    }
    static let catalog: [String:Self] = {
        var result:[String:Self] = [:]
        func add(_ id:String,_ title:String,_ help:String) { result[id] = Self(id:id,title:title,help:help) }
        add("IsFavorite","Favorite Pokémon","The favorite flag stored with this Pokémon in supported games.")
        for (suffix,name) in [("HP","HP"),("ATK","Attack"),("DEF","Defense"),("SPA","Special Attack"),("SPD","Special Defense"),("SPE","Speed")] {
            add("HT_"+suffix,name,"Marks this stat as Hyper Trained. It does not change its inherited IV.")
        }
        add("DynamaxLevel","Dynamax level","A value from 0 to 10 that affects the HP boost while Dynamaxed.")
        add("CanGigantamax","Gigantamax factor","Records the Gigantamax factor. The species and form must also support Gigantamax.")
        add("TeraTypeOriginal","Original Tera type","The Tera type recorded for the original encounter.")
        add("TeraTypeOverride","Changed Tera type","Choose the later Tera type, or Use original type to clear the change.")
        for (id,name) in [("ContestCool","Coolness"),("ContestBeauty","Beauty"),("ContestCute","Cuteness"),("ContestSmart","Cleverness"),("ContestTough","Toughness")] {
            add(id,name,"Contest condition, from 0 to 255. This is separate from battle stats.")
        }
        add("ContestSheen","Sheen","The stored feeding limit used by some contest games, from 0 to 255.")
        add("OriginalTrainerAffection","Affection for original trainer","Care-based affection, from 0 to 255. This is separate from friendship.")
        add("HandlingTrainerAffection","Affection for handling trainer","Care-based affection for the later trainer, from 0 to 255.")
        add("Fullness","Fullness","The stored fullness value after feeding, from 0 to 255.")
        add("Enjoyment","Enjoyment","The stored enjoyment value from care activities, from 0 to 255.")
        add("PID","Personality ID (PID)","A stored identifier used by encounter rules. In older games it is linked to traits such as nature and Shininess.")
        add("EncryptionConstant","Encryption constant","A stored identifier used to encode Pokémon data and determine some appearance details.")
        add("Tracker","Pokémon HOME tracker","The tracking number assigned by Pokémon HOME.")
        add("AlphaMove","Alpha encounter move","The special move recorded for an Alpha encounter. Use Moves → Alpha encounter details to choose it by name; Main has the Alpha switch.")
        add("IsNoble","Noble encounter flag","The separate Noble flag used by Legends: Arceus encounters. This is different from Alpha status.")
        add("HeightScalar","Height value","Stored relative height, from 0 to 255. This is not a measurement in metres.")
        add("WeightScalar","Weight value","Stored relative weight, from 0 to 255. This is not a measurement in kilograms.")
        add("Scale","Model size value","A separate size value, from 0 to 255. Its use depends on the game format.")
        add("HeightAbsolute","Stored height measurement","The absolute height record. Some formats also store separate relative size values.")
        add("WeightAbsolute","Stored weight measurement","The absolute weight record. Some formats also store separate relative size values.")
        add("FormArgument","Form-specific record","A species-dependent record used for certain forms and evolution progress. Its meaning depends on the Pokémon.")
        add("FormArgumentElapsed","Elapsed form counter","One part of the form-specific record. Its meaning depends on the species.")
        add("FormArgumentRemain","Remaining form counter","One part of the form-specific record. Its meaning depends on the species.")
        add("FormArgumentMaximum","Maximum form counter","One part of the form-specific record. Its meaning depends on the species.")
        add("IV32","Combined IV data","Packed storage for multiple IV-related values. Use Stats to edit individual IVs.")
        add("HyperTrainFlags","Combined Hyper Training data","Packed storage for the six Hyper Training switches.")
        add("PokerusState","Combined Pokérus data","Packed storage for the Pokérus strain and remaining contagious days.")
        add("PokerusStrain","Pokérus strain","The stored virus variant. Valid values and duration depend on the game.")
        add("PokerusDays","Contagious days remaining","The stored number of days that Pokérus remains contagious.")
        add("BattleVersion","Battle-ready game record","Records the game associated with the battle-ready mark. This is separate from the origin game on Met.")
        add("ObedienceLevel","Obedience reference level","The level record used by the game's obedience rules. This is separate from the Pokémon's current level.")
        return result
    }()
    static func annotated(_ field:Field) -> Field {
        let entry = catalog[field.id]
        let internalField = field.id.hasPrefix("Unk") || field.id.hasPrefix("RIB") || field.id.hasPrefix("Unused") || field.id.hasPrefix("Flag")
        let title = entry.map { field.id.hasPrefix("HT_") ? "Hyper Training · " + $0.title : $0.title }
        let label = title ?? (internalField ? "Internal data · " + field.id : field.label)
        let help = entry?.help ?? (internalField ? "A low-level stored value with no everyday editor control." : field.help)
        let options = entry?.choices(field) ?? []
        return Field(id:field.id,label:label,group:internalField ? "Internal data" : field.group,value:field.value,kind:field.kind,editable:field.editable,help:help,lookup:field.lookup,choices:options.isEmpty ? field.choices : options)
    }
}
