import SwiftUI
import AppKit

struct ExtraTool:Codable,Identifiable {let id:String,name:String,description:String,icon:String}
struct ExtraValue:Codable,Identifiable {let id:String,label:String,value:String,kind:String,min:String,max:String,choices:[Choice];let group:String?}
struct ExtraRow:Codable,Identifiable {let id:String,name:String,detail:String,sprite:String,fields:[ExtraValue],actions:[Choice];let profile:[Int]?;let fileExtension:String?;let category:String?}
struct ExtraPage:Codable {let kind:String,revision:Int,tool:ExtraTool,entries:[ExtraRow],actions:[Choice],files:Bool}
struct ExtraSelection:Identifiable {let id=UUID();let row:ExtraRow,page:ExtraPage}
struct GameExtrasView:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var theme
    @State private var tools:[ExtraTool]=[]
    @State private var loadError:String?
    @State private var selected=""
    @State private var donutGenerator=false
    @State private var medalSelection=Set<String>()
    @State private var selectingMedals=false
    @State private var medalDate=Date()
    @State private var page:ExtraPage?
    @State private var search=""
    @State private var selection:ExtraSelection?
    @State private var collectionGroup="0"
    @State private var avenueGroup="shop"
    @State private var extraSection="Overview"
    @State private var baseIndex="0"
    @State private var passIndex="0"
    var body:some View {VStack(alignment:.leading,spacing:20) {
        HStack {VStack(alignment:.leading,spacing:6){Label(page?.tool.name ?? "Game extras",systemImage:page?.tool.icon ?? "sparkles.rectangle.stack.fill").font(.title2.bold());Text(page?.tool.description ?? "A little more of your adventure, with tools for the game you opened.").foregroundStyle(.secondary)};Spacer();if !selected.isEmpty{Button{selected="";page=nil;search=""}label:{Label("Back to Game Extras",systemImage:"chevron.left")}.buttonStyle(.bordered).keyboardShortcut("[",modifiers:.command)}}
        if !model.state.hasSave {WorkspaceEmptyState(title:"Open a game save",icon:"sparkles.rectangle.stack.fill",message:"Each game has its own collection of editors."){Button("Open a Save…"){model.open()}.buttonStyle(.borderedProminent)}}
        else if selected.isEmpty && tools.isEmpty {WorkspaceEmptyState(title:"More editors are on the way",icon:"wrench.and.screwdriver",message:"Use Save Tools and Advanced Save for this format’s available controls.")}
        else if selected.isEmpty {ScrollView {LazyVGrid(columns:[GridItem(.adaptive(minimum:270,maximum:450))],spacing:18){ForEach(tools){tool in Button{selected=tool.id}label:{VStack(alignment:.leading,spacing:14){Image(systemName:tool.icon).font(.system(size:28)).foregroundStyle(theme.accent).frame(width:54,height:54).background(theme.accent.opacity(0.12),in:RoundedRectangle(cornerRadius:16));Text(tool.name).font(.title3.bold());Text(tool.description).font(.callout).foregroundStyle(.secondary).frame(maxWidth:.infinity,alignment:.leading).fixedSize(horizontal:false,vertical:true);Spacer(minLength:0)}.padding(22).frame(maxWidth:.infinity,minHeight:190,alignment:.topLeading).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:22)).overlay(RoundedRectangle(cornerRadius:22).stroke(theme.accent.opacity(0.28),lineWidth:1))}.buttonStyle(.plain)}}}
        } else if selected=="events9a" {ZAEventsView()} else if let page {
            HStack{TextField("Search names, locations or entry number…",text:$search).textFieldStyle(.roundedBorder);Text("\(visibleEntries(page).count) of \(page.entries.count)").font(.caption).foregroundStyle(.secondary)
                ForEach(page.actions.prefix(1)){action in Button{act(action.value,page)}label:{Label(action.label,systemImage:action.value=="give" ? "gift.fill" : "sparkles")}.buttonStyle(.borderedProminent)}
                if page.actions.count>1 || (page.files && page.kind=="medals"){Menu{ForEach(Array(page.actions.dropFirst())){action in Button(action.label){act(action.value,page)}};if page.files&&page.kind=="medals"{Button("Export Medals…"){exportFile(page,id:"")};Button("Import Medals…"){importFile(page,id:"")}}}label:{Label("More actions",systemImage:"ellipsis.circle").labelStyle(.iconOnly)}}
            }
            if page.kind=="medals" {
                HStack {
                    Toggle("Select medals",isOn:$selectingMedals).toggleStyle(.button)
                    if selectingMedals {
                        Button("Select Visible"){medalSelection=Set(visibleEntries(page).filter{Int($0.id) != nil}.map(\.id))}
                        Button("Deselect"){medalSelection=[]}
                        Text("\(medalSelection.count) selected").font(.caption).foregroundStyle(.secondary)
                        Menu("Set Status") {ForEach(page.entries.first(where:{$0.id=="0"})?.fields.first(where:{$0.id=="State"})?.choices ?? []){choice in Button(choice.label){editMedals(page,field:"State",value:choice.value)}};Button("Mark Read"){editMedals(page,field:"IsUnread",value:"false")};Button("Mark Unread"){editMedals(page,field:"IsUnread",value:"true")};Button("Clear Selected"){editMedals(page,mode:"clear")}}.disabled(medalSelection.isEmpty)
                    }
                }
                if selectingMedals {HStack{DatePicker("Award date",selection:$medalDate,displayedComponents:.date);Button("Apply Date"){let f=DateFormatter();f.dateFormat="yyyy-MM-dd";editMedals(page,field:"Date",value:f.string(from:medalDate))}.disabled(medalSelection.isEmpty)}}
            }
            if page.kind=="donuts" {Button("Generate a Custom Range…"){donutGenerator=true}}
            if page.kind=="passes" {Picker("Battle Pass",selection:$passIndex){ForEach(page.entries.filter{$0.id.hasPrefix("pass:")}){row in Text(row.name).tag(String(row.id.dropFirst(5)))}}.pickerStyle(.menu)}
            if page.kind=="fame" && page.entries.contains(where:{$0.id.hasPrefix("team:")}) {
                HStack{Label("League record",systemImage:"trophy.fill").font(.callout.bold());Picker("Team",selection:$collectionGroup){ForEach(page.entries.filter{$0.id.hasPrefix("team:")}){entry in Text(entry.name).tag(String(entry.id.dropFirst(5)))}}.labelsHidden().frame(width:220);Spacer();Text("Choose a team, then a Pokémon to edit.").font(.caption).foregroundStyle(.secondary)}
            }
            if page.kind=="avenue" {Picker("Section",selection:$avenueGroup){Text("Shops").tag("shop");Text("Visitors").tag("visitor");Text("Fans").tag("fan");Text("Assistants").tag("assistant");Text("My Avenue").tag("settings");Text("Visit History").tag("history")}.pickerStyle(.segmented)}
            if ["bases3","bases6"].contains(page.kind) {
                Picker("Base",selection:$baseIndex){if page.kind=="bases6"{Text("Decoration stock").tag("stock")};ForEach(page.entries.filter{$0.id.hasPrefix("base:")}){row in Text(row.name).tag(String(row.id.dropFirst(5)))}}.pickerStyle(.menu)
                if !page.entries.contains(where:{$0.id.hasPrefix("base:")}) {ContentUnavailableView("No visiting bases",systemImage:"house",description:Text("This save has no registered Secret Base trainers."))}
            }
            if ["globallink5","link6","underground4","training6"].contains(page.kind){Picker("Collection",selection:$extraSection){ForEach(sections(page),id:\.self){Text($0).tag($0)}}.pickerStyle(.segmented)}
            if page.kind=="dlc5" || page.entries.contains(where:{$0.category != nil}){Picker("Collection",selection:$extraSection){ForEach(sections(page),id:\.self){Text($0).tag($0)}}.pickerStyle(.menu)}
            if page.kind=="pokeathlon" || page.kind=="festival" {Picker("Collection",selection:$extraSection){ForEach(sections(page),id:\.self){Text($0).tag($0)}}.pickerStyle(.segmented)}
            ScrollView {LazyVStack(spacing:10){ForEach(visibleEntries(page)){row in Button{if page.kind=="medals" && selectingMedals && Int(row.id) != nil {if medalSelection.contains(row.id){medalSelection.remove(row.id)}else{medalSelection.insert(row.id)}}else{open(row,page)}}label:{HStack(spacing:16){if page.kind=="medals" && selectingMedals && Int(row.id) != nil {Image(systemName:medalSelection.contains(row.id) ? "checkmark.circle.fill":"circle").foregroundStyle(theme.accent)};ExtraArtwork(row:row,icon:page.tool.icon).frame(width:45,height:45);VStack(alignment:.leading,spacing:5){Text(row.name).font(.headline);Text(row.detail).font(.caption).foregroundStyle(.secondary).lineLimit(3)};Spacer();if let first=row.fields.first{Text(summary(first)).font(.callout).foregroundStyle(theme.accent).lineLimit(1).frame(maxWidth:150,alignment:.trailing)};Image(systemName:"chevron.right").font(.caption).foregroundStyle(.tertiary)}.padding(14).background(Color(nsColor:.controlBackgroundColor),in:RoundedRectangle(cornerRadius:16)).overlay(RoundedRectangle(cornerRadius:16).stroke(.primary.opacity(0.08),lineWidth:1))}.buttonStyle(.plain)}}}.overlay{if visibleEntries(page).isEmpty{ContentUnavailableView.search(text:search)}}
            Text("Changes stay in memory until Export Copy. Undo restores the previous values.").font(.caption).foregroundStyle(.secondary)
        }else if let loadError {WorkspaceEmptyState(title:"Couldn’t load this collection",icon:"arrow.clockwise",message:loadError){Button("Try Again"){Task{await load()}}.buttonStyle(.borderedProminent)}}else{ProgressView().frame(maxWidth:.infinity,maxHeight:.infinity)}
    }.padding(28).frame(maxWidth:1100,maxHeight:.infinity,alignment:.topLeading).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.top).disabled(model.busy||model.fieldDrafts).task(id:"\(model.state.revision):\(selected)"){await load()}.sheet(isPresented:$donutGenerator){DonutRangeSheet()}.sheet(item:$selection){ExtraEntryEditor(selection:$0)}}
    private func visibleEntries(_ page:ExtraPage)->[ExtraRow] {
        page.entries.filter{row in
            let teamMatches=page.kind != "fame" || !page.entries.contains(where:{$0.id.hasPrefix("team:")}) || row.id=="count" || row.id=="team:"+collectionGroup || row.id.hasPrefix(collectionGroup+":")
            let avenueMatches=page.kind != "avenue" || row.id.hasPrefix(avenueGroup+":") || (avenueGroup=="settings" && ["settings","self"].contains(row.id))
            return extraMatches(row,page) && teamMatches && avenueMatches && (search.isEmpty || [row.name,row.detail,row.id].contains{$0.localizedCaseInsensitiveContains(search)})
        }
    }
    private func sections(_ page:ExtraPage)->[String] {if page.entries.contains(where:{$0.category != nil}){var result:[String]=[];for row in page.entries{if let group=row.category,!result.contains(group){result.append(group)}};return result};switch page.kind {case "training6":return ["Records","Training bags","Distribution"];case "underground4":return ["Records","Goods","Spheres","Traps","Treasures"];case "globallink5":return ["Overview","Items","Furniture"];case "link6":return ["Delivery","Items","Pokémon"];case "dlc5":return ["Appearance","Battle videos","Musicals","Memory Link"]+(page.entries.contains{$0.id.hasPrefix("pwt:")} ? ["Tournaments","Movies"]:[])+["Advanced"];case "pokeathlon":return ["Overview","Courses","Solo records","Linked records","Medals"];default:return ["Overview","Facilities","Phrases","Rewards"]+(page.entries.contains{$0.id=="agency"} ? ["Battle Agency"]:[])}}
    private func extraMatches(_ row:ExtraRow,_ page:ExtraPage)->Bool {
        if let category=row.category {return category==extraSection}
        let id=row.id
        if page.kind=="training6" {return extraSection=="Training bags" ? id=="bags" || id.hasPrefix("bag:") : extraSection=="Distribution" ? id.hasPrefix("distribution:") : id.hasPrefix("stage:")}
        if page.kind=="underground4" {let pouch=extraSection.lowercased();return extraSection=="Records" ? id=="records" : id=="pouch:"+pouch || id.hasPrefix(pouch+":")}
        if page.kind=="globallink5" {return extraSection=="Items" ? id.hasPrefix("item:"):extraSection=="Furniture" ? id.hasPrefix("furniture:"):id=="settings"}
        if page.kind=="link6" {return extraSection=="Items" ? id.hasPrefix("item:"):extraSection=="Pokémon" ? id.hasPrefix("pokemon:"):id=="settings"}
        if page.kind=="dlc5" {switch extraSection{case "Battle videos":return id.hasPrefix("video:");case "Musicals":return id=="musical";case "Memory Link":return id=="link1" || id=="link2";case "Tournaments":return id.hasPrefix("pwt:");case "Movies":return id.hasPrefix("movie:");case "Advanced":return id=="test";default:return id=="cgear" || id=="dexskin"}}
        if page.kind=="passes" {return id=="pass:"+passIndex || id.hasPrefix("member:"+passIndex+":")}
        if ["bases3","bases6"].contains(page.kind){return baseIndex=="stock" ? id.hasPrefix("good:"):id=="base:"+baseIndex || id.hasPrefix(baseIndex+":") || (baseIndex=="0" && id=="settings")}
        if page.kind=="pokeathlon" {switch extraSection {case "Courses":return id.hasPrefix("course:") || id.hasPrefix("participant:");case "Solo records":return id.hasPrefix("self:") || id.hasPrefix("record:self:");case "Linked records":return id.hasPrefix("connected:") || id.hasPrefix("record:connected:") || id.hasPrefix("trainer:");case "Medals":return id.hasPrefix("medal:");default:return id=="general" || id=="counters" || id.hasPrefix("best:")}}
        if page.kind=="festival" {switch extraSection {case "Facilities":return id.hasPrefix("facility:");case "Phrases":return id.hasPrefix("phrase:");case "Rewards":return id.hasPrefix("reward:");case "Battle Agency":return id=="agency" || id.hasPrefix("agent:");default:return id=="plaza"}}
        return true
    }
    private func summary(_ field:ExtraValue)->String {if field.kind=="dotart" {let count=field.value.filter{$0 != "0"}.count;return count==0 ? "Blank canvas" : "\(count) inked pixels"};return field.kind=="bool" ? (field.value=="true" ? "Yes" : "No") : field.choices.first{$0.value==field.value}?.label ?? field.value}
    private func load()async{guard model.state.hasSave else{return};loadError=nil;do{let list=try await model.bridge.send(["op":"extraTools"],as:[ExtraTool].self);try Task.checkCancellation();tools=list;if !selected.isEmpty && !list.contains(where:{$0.id==selected}){selected=""};if selected.isEmpty{page=nil}else{let value=try await model.bridge.send(["op":"extraPage","kind":selected],as:ExtraPage.self);try Task.checkCancellation();page=value;if !sections(value).contains(extraSection){extraSection=sections(value).first ?? "Overview"};if !value.entries.contains(where:{$0.id=="base:"+baseIndex}) && baseIndex != "stock"{baseIndex="0"};if !value.entries.contains(where:{$0.id=="team:"+collectionGroup}){collectionGroup="0"}}}catch is CancellationError{}catch{page=nil;loadError=error.localizedDescription}}
    private func open(_ row:ExtraRow,_ page:ExtraPage){Task{do{let value=try await model.bridge.send(["op":"extraEntry","kind":page.kind,"id":row.id],as:ExtraRow.self);selection=ExtraSelection(row:value,page:page)}catch{model.error=error.localizedDescription}}}
    private func editMedals(_ page:ExtraPage,mode:String="edit",field:String="",value:String="") {Task{await model.command(["op":"medalsSetSelected","revision":page.revision,"mode":mode,"ids":Array(medalSelection),"edits":[["field":field,"value":value]]])}}
    private func act(_ mode:String,_ page:ExtraPage){Task{await model.command(["op":"extraSet","kind":page.kind,"mode":mode,"revision":page.revision])}}
    private func exportFile(_ page:ExtraPage,id:String){let p=NSSavePanel();p.nameFieldStringValue="Medals.ml5";guard p.runModal() == .OK,let url=p.url else{return};Task{do{_ = try await model.bridge.send(["op":"extraExport","kind":page.kind,"id":id,"path":url.path],as:PathResult.self);model.status="Exported medals"}catch{model.error=error.localizedDescription}}}
    private func importFile(_ page:ExtraPage,id:String){let p=NSOpenPanel();guard p.runModal() == .OK,let url=p.url else{return};Task{await model.command(["op":"extraSet","kind":page.kind,"mode":"import","id":id,"path":url.path,"revision":page.revision])}}
}
private struct ExtraArtwork:View {
    let row:ExtraRow,icon:String
    var body:some View {if row.sprite.hasPrefix("donut_"){GameAsset(folder:"Donuts",name:row.sprite)}else if row.sprite.hasPrefix("bitem_"){GameAsset(folder:"Items",name:row.sprite)}else if !row.sprite.isEmpty{PokemonSprite(name:row.sprite)}else{Image(systemName:icon).font(.title2).foregroundStyle(.tint).frame(maxWidth:.infinity,maxHeight:.infinity).background(.tint.opacity(0.1),in:RoundedRectangle(cornerRadius:12))}}
}
private struct ExtraEntryEditor:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.dismiss) private var dismiss
    let selection:ExtraSelection
    @State private var values:[String:String]=[:]
    @State private var fieldGroup="Profile"
    @State private var showingCGear=false
    @State private var showingNameBytes=false
    @State private var decryptedExport=false
    @State private var droppedFile:URL?
    private var row:ExtraRow {selection.row}
    private var page:ExtraPage {selection.page}
    private var changed:Bool {row.fields.contains{(values[$0.id] ?? $0.value) != $0.value}}
    var body:some View {VStack(alignment:.leading,spacing:16){
        HStack{ExtraArtwork(row:row,icon:page.tool.icon).frame(width:40,height:40);Text(row.name).font(.title2.bold());Spacer();Button("Cancel"){dismiss()}.keyboardShortcut(.cancelAction);if row.fields.contains(where:{$0.kind != "readonly"}){Button("Save Changes"){apply("edit")}.buttonStyle(.borderedProminent).disabled(!changed).keyboardShortcut(.defaultAction)}}
        Text(row.detail).font(.callout).foregroundStyle(.secondary).fixedSize(horizontal:false,vertical:true)
        if (page.kind=="fame" && row.fields.contains(where:{$0.id=="Nickname"})) || (page.kind=="bases3" && row.id.hasPrefix("base:")) {Button("Names & Text Bytes…"){showingNameBytes=true}.disabled(changed)}
        if page.kind=="fame" {Button {let text=([row.name,row.detail]+row.fields.map{"\($0.label): \(values[$0.id] ?? $0.value)"}).joined(separator:"\n");NSPasteboard.general.clearContents();NSPasteboard.general.setString(text,forType:.string)}label:{Label("Copy Summary",systemImage:"doc.on.doc")}}
        if page.kind=="donuts" {
            HStack {Button("Copy Hex"){Task{do{let text=try await model.bridge.send(["op":"donutClipboard","id":Int(row.id) ?? -1],as:String.self);NSPasteboard.general.clearContents();NSPasteboard.general.setString(text,forType:.string)}catch{model.error=error.localizedDescription}}};Button("Paste Hex"){if let text=NSPasteboard.general.string(forType:.string){apply("import",hex:text)}}}.disabled(changed)
        }
        if let file=droppedFile {HStack{Text(file.lastPathComponent).lineLimit(1);Spacer();Button("Cancel Drop"){droppedFile=nil};Button("Import to This Entry"){apply("import",path:file.path)}.buttonStyle(.borderedProminent)}.disabled(changed)}
        if let profile=row.profile {DonutProfileView(values:profile)}
        if page.kind=="chatter" {ChatterPlayer().disabled(changed)}
        if page.kind=="dlc5" && row.id=="cgear" {Button{showingCGear=true}label:{Label("Preview & Customize Background…",systemImage:"paintpalette.fill")}.buttonStyle(.borderedProminent)}
        if fieldGroups.count>2 {Picker("Details",selection:$fieldGroup){ForEach(fieldGroups,id:\.self){Text($0.isEmpty ? "Profile" : $0).tag($0)}}.pickerStyle(.menu)}
        if !row.fields.isEmpty {Form{ForEach(visibleFieldGroups,id:\.self){group in
            if group=="Advanced" {DisclosureGroup("Advanced stored values"){Text("These values match the underlying game records. Leave them as stored unless you know the change you need.").font(.caption).foregroundStyle(.secondary);controls(group)}}
            else if group.isEmpty {controls(group)}
            else {Section(group){controls(group)}}
        }}.formStyle(.grouped)}else{Spacer(minLength:10)}
        HStack{ForEach(row.actions){action in Button(action.label){apply(action.value)}.disabled(changed)};Spacer()}
        if changed && !row.actions.isEmpty{Text("Save your edits before using an action on this entry.").font(.caption).foregroundStyle(.secondary)}
        if page.files && (page.kind=="donuts" || row.fileExtension != nil) {HStack{Button("Export \(page.kind=="chatter" ? "PCM Recording" : page.kind=="donuts" ? "Donut" : page.kind=="bases6" ? "Base" : page.kind=="festival" ? "Pokémon" : ["dlc4","dlc5","link6","passes","chatter","misc5"].contains(page.kind) ? "File" : "Person")…"){exportEntry()};Button("Import \(page.kind=="chatter" ? "PCM Recording" : page.kind=="donuts" ? "Donut" : page.kind=="bases6" ? "Base" : page.kind=="festival" ? "Pokémon" : ["dlc4","dlc5","link6","passes","chatter","misc5"].contains(page.kind) ? "File" : "Person")…"){importDonut()}}.disabled(changed)
            if ["dlc4","dlc5"].contains(page.kind) && row.id.hasPrefix("video:") {Toggle("Export decrypted copy",isOn:$decryptedExport).font(.callout)}
            if page.kind=="avenue" {Text("Import a visitor, fan, or assistant file. A different kind copies the shared profile fields, just as in PKHeX.").font(.caption).foregroundStyle(.secondary)}
        }
    }.padding(24).dropDestination(for:FileDropItem.self){items,_ in
        guard page.files,!changed,!model.busy else{return false}
        do{droppedFile=try FileDropSelection.file(from:items.map(\.url));return true}catch{model.error=error.localizedDescription;return false}
    }.frame(width:720,height:row.id=="dotart" ? 720:page.kind=="chatter" ? 640:min(680,max(300,240 + CGFloat(row.fields.count)*68 + (row.profile == nil ? 0:110)))).disabled(model.busy).sheet(isPresented:$showingNameBytes){NameBytesEditor(fameID:page.kind=="fame" ? row.id:nil,baseID:page.kind=="bases3" ? row.id:nil)}.onChange(of:model.state.revision){_,_ in if showingNameBytes {showingNameBytes=false;dismiss()}}.sheet(isPresented:$showingCGear){CGearPreview()}.onAppear{fieldGroup=fieldGroups.first ?? "";values=Dictionary(uniqueKeysWithValues:row.fields.map{($0.id,$0.value)})}}
    private var visibleFieldGroups:[String] {fieldGroups.count>2 ? [fieldGroup]:fieldGroups}
    private var fieldGroups:[String] {var result:[String]=[];for field in row.fields {let group=field.group ?? "";if !result.contains(group){result.append(group)}};return result.filter{$0 != "Advanced"} + (result.contains("Advanced") ? ["Advanced"]:[])}
    @ViewBuilder private func controls(_ group:String)->some View {ForEach(row.fields.filter{($0.group ?? "")==group}){field in ExtraValueControl(field:field,value:binding(field))}}
    private func binding(_ field:ExtraValue)->Binding<String>{Binding(get:{values[field.id] ?? field.value},set:{values[field.id]=$0})}
    private func apply(_ mode:String,path:String?=nil,hex:String?=nil){var req:[String:Any]=["op":mode=="view" ? "extraView":"extraSet","kind":page.kind,"id":row.id,"mode":mode,"revision":page.revision];if mode=="edit"{req["edits"]=row.fields.filter{(values[$0.id] ?? $0.value) != $0.value}.map{["field":$0.id,"value":values[$0.id] ?? $0.value]}};if let path{req["path"]=path};if let hex{req["hex"]=hex};Task{await model.command(req);if model.error==nil{dismiss();if mode=="view"{model.section="Pokémon"}}}}
    private func exportEntry(){let p=NSSavePanel();p.nameFieldStringValue="\(page.tool.name.replacingOccurrences(of:" ",with:"-"))-\(row.id.replacingOccurrences(of:":",with:"-")).\(row.fileExtension ?? "donut")";guard p.runModal() == .OK,let url=p.url else{return};Task{do{_ = try await model.bridge.send(["op":"extraExport","kind":page.kind,"id":row.id,"path":url.path,"decrypted":decryptedExport],as:PathResult.self);model.status="Exported \(row.name)"}catch{model.error=error.localizedDescription}}}
    private func importDonut(){let p=NSOpenPanel();guard p.runModal() == .OK,let url=p.url else{return};apply("import",path:url.path)}
}
private struct ExtraValueControl:View {
    let field:ExtraValue
    @Binding var value:String
    var body:some View {if field.kind=="readonly"{LabeledContent(field.label,value:value)}
        else if field.kind=="dotart"{DotArtistCanvas(value:$value)}
        else if field.kind=="bool"{Toggle(field.label,isOn:Binding(get:{value.lowercased()=="true"},set:{value=$0 ? "true" : "false"}))}
        else if field.kind=="enum" {CatalogChoiceButton(title:field.label,options:options,value:$value).tint(field.id.hasPrefix("Gender") ? (value=="1" ? .pink : value=="0" ? .blue : .secondary) : .accentColor)}
        else if field.kind=="multiline"{VStack(alignment:.leading){Text(field.label);TextEditor(text:$value).frame(minHeight:70);Text("Up to \(field.max) game characters").font(.caption2).foregroundStyle(.secondary)}}
        else if field.kind=="timestamp"{if value=="0"{HStack{Text("Creation date");Spacer();Button("Set to Now"){value=String(Int64(Date().timeIntervalSince1970*1000))}}}else{DatePicker("Creation date",selection:Binding(get:{Date(timeIntervalSince1970:(Double(value) ?? 0)/1000)},set:{value=String(max(0,Int64($0.timeIntervalSince1970*1000)))}),displayedComponents:[.date,.hourAndMinute])}}
        else {VStack(alignment:.leading,spacing:5){TextField(field.label,text:$value).textFieldStyle(.roundedBorder);Text(field.kind=="float" ? "Decimal seconds · 0 or greater" : field.kind=="date" ? "YYYY-MM-DD · 2000–2099" : field.kind=="hex" ? "Hexadecimal · up to \(field.max) digits (0–9, A–F)" : field.kind=="text" ? "Up to \(field.max) characters" : "\(field.min)–\(field.max)").font(.caption2).foregroundStyle(.tertiary)}}
    }
    private var options:[Choice]{field.choices.contains{$0.value==value} ? field.choices : field.choices+[Choice(value:value,label:"Stored value · "+value)]}
}

private struct DonutProfileView:View {
    let values:[Int]
    private let names=["Spicy","Fresh","Sweet","Bitter","Sour"]
    private let colors:[Color]=[.orange,.mint,.pink,.green,.yellow]
    var body:some View {HStack(alignment:.bottom,spacing:14){ForEach(Array(values.prefix(5).enumerated()),id:\.offset){i,value in VStack(spacing:5){Text("\(value)").font(.caption.monospacedDigit());RoundedRectangle(cornerRadius:5).fill(colors[i].gradient).frame(height:max(3,44*Double(value)/Double(max(values.max() ?? 1,1))));Text(names[i]).font(.caption2)}.frame(maxWidth:.infinity)}}.frame(height:80).padding(12).background(.quaternary.opacity(0.3),in:RoundedRectangle(cornerRadius:12)).accessibilityElement(children:.combine)}
}
