import SwiftUI

struct LibraryEntry:Codable,Identifiable {
    var portrait:String?=nil
    let id:Int,path:String,name:String,nickname:String,level:Int,format:String,game:String,shiny:Bool,alpha:Bool,trainer:String,sprite:String,checksum:Bool
}
struct LibraryData:Codable {let token:String,path:String,paths:[String],skipped:Int,truncated:Bool,entries:[LibraryEntry]}
struct PokemonLibraryView:View {
    @EnvironmentObject var model:EditorModel
    @State private var recursive=true
    @State private var query=""
    @State private var shinyOnly=false
    @State private var alphaOnly=false
    @State private var selection:Int?
    @State private var batchSheet=false
    var rows:[LibraryEntry] {(model.library?.entries ?? []).filter{(!shinyOnly || $0.shiny) && (!alphaOnly || $0.alpha) && (query.isEmpty || [$0.name,$0.nickname,$0.trainer,$0.path,$0.game,$0.format].joined(separator:" ").localizedCaseInsensitiveContains(query))}}
    var chosen:LibraryEntry? {rows.first{$0.id==selection}}
    var body:some View {
        VStack(alignment:.leading,spacing:16) {
            HStack {Text("Pokémon file library").font(.title2.bold());Spacer();Menu("Manage Folders"){Button("Add Folders…"){model.chooseLibrary(recursive:recursive,append:true)};Button("Reopen Saved Folders"){Task{await model.scanLibrary(paths:UserDefaults.standard.stringArray(forKey:"pokemonLibraryRoots") ?? [],recursive:recursive)}}.disabled((UserDefaults.standard.stringArray(forKey:"pokemonLibraryRoots") ?? []).isEmpty)};Button("Choose Folders…"){selection=nil;model.chooseLibrary(recursive:recursive)}.buttonStyle(.borderedProminent)}
            Text("Search Pokémon files on your Mac and copy one into the editor. The library reads your files without changing them.").foregroundStyle(.secondary)
            HStack {Toggle("Include subfolders",isOn:$recursive).toggleStyle(.checkbox);Spacer();if let data=model.library{Button("Refresh"){selection=nil;Task{await model.scanLibrary(paths:data.paths,recursive:recursive)}}}}
            if let data=model.library {
                DisclosureGroup("\(data.paths.count) library folders") {ForEach(data.paths,id:\.self){path in HStack {Text(path).font(.caption).textSelection(.enabled);Spacer();Button("Remove"){Task{await model.scanLibrary(paths:data.paths.filter{$0 != path},recursive:recursive)}}}}}
                HStack {TextField("Find species, nickname, trainer, game, or filename…",text:$query).textFieldStyle(.roundedBorder);Toggle("Shiny",isOn:$shinyOnly).toggleStyle(.checkbox);Toggle("Alpha",isOn:$alphaOnly).toggleStyle(.checkbox)}
                Table(rows,selection:$selection) {
                    TableColumn("Pokémon"){entry in HStack{PokemonSprite(name:entry.sprite,portrait:entry.portrait).frame(width:28,height:24);VStack(alignment:.leading){Text(entry.nickname.isEmpty ? entry.name : entry.nickname).fontWeight(.medium);Text(entry.name).font(.caption).foregroundStyle(.secondary)}}}.width(min:130,ideal:160)
                                        TableColumn("Level"){Text(String($0.level))}.width(45)
                    TableColumn("Format",value:\.format).width(55)
                    TableColumn("Trainer",value:\.trainer).width(min:80,ideal:100)
                    TableColumn("File",value:\.path)
                }.overlay{if rows.isEmpty{WorkspaceEmptyState(title:data.entries.isEmpty ? "No Pokémon files here":"No matching companions",icon:"folder",message:data.entries.isEmpty ? "Choose another folder or include subfolders to find supported Pokémon files.":"Try a different search or turn off the Shiny and Alpha filters.").background(Color(nsColor:.windowBackgroundColor))}}
                if let chosen {Text(chosen.game+(chosen.checksum ? "" : " · File checksum needs review")).font(.caption).foregroundStyle(chosen.checksum ? Color.secondary : .orange)}
                HStack {
                    Text("\(rows.count) Pokémon · \(data.skipped) unreadable or empty files skipped"+(data.truncated ? " · Scan limit reached; choose a smaller folder" : "")).font(.caption).foregroundStyle(.secondary)
                    Spacer()
                    Button("Batch Edit Visible Files…"){batchSheet=true}.disabled(rows.isEmpty)
                    Button("Load into Editor"){if let chosen{model.prepareLibrary(chosen,token:data.token)}}.buttonStyle(.borderedProminent).disabled(chosen==nil)
                }
                Text("When a save is open, compatible formats are converted for that game. Review legality before Set to Slot or exporting a new Pokémon file.").font(.caption).foregroundStyle(.secondary)
            } else {WorkspaceEmptyState(title:"A home for your collection",icon:"folder.fill",message:"Choose a folder of Pokémon files to browse your companions. Open game saves with the main Open button."){Button("Choose Folder…"){model.chooseLibrary(recursive:recursive)}.buttonStyle(.borderedProminent)}}
        }.padding(24).frame(maxWidth:.infinity,maxHeight:.infinity,alignment:.topLeading)
            .sheet(isPresented:$batchSheet){if let library=model.library{FolderBatchView(token:library.token,ids:rows.map(\.id))}}
    }
}
extension EditorModel {
    func chooseLibrary(recursive:Bool,append:Bool=false) {
        guard !busy else{return}
        let panel=NSOpenPanel();panel.title="Choose a Pokémon file folder";panel.canChooseFiles=false;panel.canChooseDirectories=true;panel.allowsMultipleSelection=true
        guard panel.runModal() == .OK,!panel.urls.isEmpty else{return}
        let paths=(append ? library?.paths ?? []:[])+panel.urls.map(\.path)
        Task{await scanLibrary(paths:paths,recursive:recursive)}
    }
    func scanLibrary(paths:[String],recursive:Bool) async {
        guard !busy else{return};if paths.isEmpty{library=nil;UserDefaults.standard.set([],forKey:"pokemonLibraryRoots");return};busy=true;defer{busy=false}
        do{library=try await bridge.send(["op":"libraryScan","paths":paths,"recursive":recursive],as:LibraryData.self);UserDefaults.standard.set(library?.paths ?? [],forKey:"pokemonLibraryRoots");status="Pokémon folder scanned"}
        catch{self.error=error.localizedDescription}
    }
    func prepareLibrary(_ entry:LibraryEntry,token:String) {
        guard !busy,confirmDiscard(all:false) else{return}
        Task{await command(["op":"libraryPrepare","id":entry.id,"token":token],status:"Library Pokémon loaded — review before saving");if error==nil{section="Pokémon"}}
    }
}
