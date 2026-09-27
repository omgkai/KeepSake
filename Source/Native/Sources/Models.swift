import SwiftUI
import AppKit

struct Field: Codable, Identifiable {
    let id: String, label: String, group: String, value: String, kind: String
    let editable: Bool
    let help: String
    let lookup: String?
    let choices: [Choice]
}
struct Choice: Codable, Identifiable { let value: String; let label: String; var id: String { value } }
struct Slot: Codable, Identifiable {
    let index: Int, party: Bool, species: Int, name: String, nickname: String, level: Int, shiny: Bool, empty: Bool, sprite: String
    let gender:Int, alpha:Bool, egg:Bool
    let pokemonData:String,pokemonExtension:String
    let journalKey:String
    var portrait:String?=nil
    var heldItem:Int?=nil
    var heldItemIcon:String?=nil
    var heldItemName:String?=nil
    var displayName:String {nickname.isEmpty ? name : nickname}
    var id: String { "\(party)-\(index)" }
}
struct TrainerJourney:Codable {let title:String,value:String,symbol:String}
struct TrainerBadge:Codable,Identifiable {let id:Int,name:String,earned:Bool,group:String,artwork:String,symbol:String,recorded:Bool}
struct GrowthInfo:Codable {let exp:Int,level:Int,floor:Int,next:Int,friendship:Int,egg:Bool,friendshipField:String,pokerus:Bool,pokerusState:String}
struct EditorState: Codable {
    var growth:GrowthInfo?
    var abilityDescription=""
    var entityData=""
    var entityPortrait:String?=nil
    var trainerBadges:[TrainerBadge]=[]
    var trainerJourney:[TrainerJourney]=[]
    var entityJournalKey=""
    var canTreats=false
    var canFashion=false, characteristic="", originGame="", originVersion=""
    var engineVersion = "", loaded = false, hasSave = false, demo = false, dirty = false, pending = false
    var sourceName = "", game = "", gameVersion = "", generation = 0, boxCount = 0, box = 0, slot = -1, party = false
    var slots: [Slot] = [], partySlots: [Slot] = [], boxNames: [String] = []
    var entitySprite = "b_0", entityName = "", entityNickname = "", entityExtension = "pk9", entityLevel = 0
    var fields: [Field] = [], saveFields: [Field] = []
    var stats: [Int] = [], baseStats:[Int]=[]
    var potential=0
    var cosmeticInfo:CosmeticInfo?
    var moveChecks: [MoveCheck] = []
    var decorations:DecorationData?
    var superTraining:SuperTrainingData?
    var boxLayout:BoxLayoutData?
    var revision=0, dragSession=""
    var gameLogoVersion=""
    var canDexRecords=false, canPlusRecords=false, canMoveRecords=false, canResearch=false, canGiftAlbum=false
    var suggestionMessage = "", legality = "empty", report = "", canUndo = false, canRedo = false, checksumValid = true, canDex = false, canInventory = false, canEvents = false
}
struct BatchChange: Codable { let location: String, name: String, detail: String }
struct BatchPreview: Codable { let token: String, count: Int; let changes: [BatchChange]; let errors: [String] }
struct SaveNode: Codable, Identifiable { let id: String, label: String, type: String }
struct SaveObjectData: Codable { let path: String, title: String, type: String; let fields: [Field]; let nodes: [SaveNode] }
struct EventEntry: Codable, Identifiable { let id: Int; let value: Bool }
struct EventData: Codable { let supported: Bool; let entries: [EventEntry] }
struct InventoryItem: Codable, Identifiable { let id: Int, item: Int, count: Int, name: String, icon:String }
struct Pouch: Codable, Identifiable { let id: Int, name: String, max: Int; let fixedItems: Bool; let limits: [String:Int]; let choices: [Choice]; let items: [InventoryItem] }
struct InventoryData: Codable { let pouches: [Pouch]; let dirty: Bool, canUndo: Bool }
struct DexEntry: Codable, Identifiable { let id: Int, name: String; let details:Bool; var seen: Bool, caught: Bool }
struct DexData: Codable { let supported: Bool; let entries: [DexEntry]; let dirty: Bool, canUndo: Bool }
struct Envelope<T: Decodable>: Decodable { let ok: Bool; let data: T?; let error: String? }
struct BridgeError: LocalizedError { let message: String; var errorDescription: String? { message } }

final class Bridge: @unchecked Sendable {
    private let queue = DispatchQueue(label: "io.pkhex.swift.engine", qos: .userInitiated)
    private var process: Process?
    private var input: FileHandle?
    private var output: FileHandle?
    private var buffer = Data()
    func send<T: Decodable>(_ payload: [String: Any], as: T.Type) async throws -> T {
        try await withCheckedThrowingContinuation { continuation in
            queue.async { [self] in
                do {
                    if process == nil { try start() }
                    guard process?.isRunning == true, let input, let output else { throw BridgeError(message: "The save engine stopped. Reopen the app to restart it.") }
                    var request = try JSONSerialization.data(withJSONObject: payload)
                    request.append(10)
                    try input.write(contentsOf: request)
                    while buffer.firstIndex(of: 10) == nil {
                        let chunk = output.availableData
                        if chunk.isEmpty { throw BridgeError(message: "The save engine closed its connection.") }
                        buffer.append(chunk)
                    }
                    let end = buffer.firstIndex(of: 10)!
                    let line = buffer.prefix(upTo: end)
                    buffer.removeSubrange(...end)
                    let response = try JSONDecoder().decode(Envelope<T>.self, from: line)
                    guard response.ok, let data = response.data else { throw BridgeError(message: response.error ?? "The engine could not complete this action.") }
                    continuation.resume(returning: data)
                } catch let error as DecodingError {
                    let detail:String
                    switch error {
                    case .keyNotFound(let key,let context): detail="Missing \(key.stringValue) at \(context.codingPath.map(\.stringValue).joined(separator:"."))"
                    case .valueNotFound(_,let context),.typeMismatch(_,let context),.dataCorrupted(let context): detail=context.debugDescription + " at " + context.codingPath.map(\.stringValue).joined(separator:".")
                    @unknown default: detail=error.localizedDescription
                    }
                    continuation.resume(throwing:BridgeError(message:"Couldn’t read the engine’s \(payload["op"] ?? "") response (\(payload["kind"] ?? "")). \(detail)."))
                } catch { continuation.resume(throwing: error) }
            }
        }
    }
    private func start() throws {
        let executable = Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/PKHeXBridge")
        guard FileManager.default.isExecutableFile(atPath: executable.path) else { throw BridgeError(message: "The bundled save engine is missing. Rebuild the complete app with build.sh.") }
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("PKHeXSwift")
        let task = Process(), stdin = Pipe(), stdout = Pipe()
        task.executableURL = executable
        task.arguments = [appSupport.appendingPathComponent("settings.json").path]
        task.standardInput = stdin; task.standardOutput = stdout; task.standardError = FileHandle.standardError
        task.environment = ProcessInfo.processInfo.environment.merging(["DOTNET_CLI_TELEMETRY_OPTOUT":"1", "DOTNET_EnableDiagnostics":"0"]) { _, new in new }
        try task.run()
        process = task; input = stdin.fileHandleForWriting; output = stdout.fileHandleForReading
    }
    func stop() { process?.terminate() }
    deinit { process?.terminate() }
}

@MainActor final class EditorModel: ObservableObject {
    @Published var state = EditorState()
    @Published var busy = false
    @Published var error: String?
    @Published var section = "Pokémon"
    @Published var fields: [Field] = []
    @Published var inventory: [Pouch] = []
    @Published var inventoryExtra: [Field] = []
    var itemPouch = 0, itemSlot = 0
    @Published var dex: [DexEntry] = []
    @Published var saveObject: SaveObjectData?
    @Published var batch: BatchPreview?
    @Published var events: [EventEntry] = []
    @Published var drafts: [String: String] = [:]
    @Published var status = "Ready"
    @Published var encounters:EncounterData?
    @Published var library:LibraryData?
    @Published var plusRecords:MoveRecordData?
    @Published var moveRecords:MoveRecordData?
    @Published var dexRecord:DexRecordData?
    @Published var dexDetails:DexDetailData?
    @Published var giftAlbum:AlbumData?
    @Published var pendingAlbumGift:GiftEntry?
    @Published var fashion:FashionData?
    @Published var research:ResearchData?
    @Published var researchSpecies=25
    @Published var storage:[StorageEntry]=[]
    @Published var gifts:[GiftEntry]=[]
    @Published var catalogs: [String: [Choice]] = [:]
    let bridge = Bridge()
    var unsaved: Bool { state.dirty || state.pending || !drafts.isEmpty }
    var fieldDrafts: Bool { !drafts.isEmpty }
    private var didStart = false
    func start(reopenLast:Bool=true) async {
        guard !didStart else { return }; didStart = true
        await command(["op":"state"])
        if ProcessInfo.processInfo.arguments.contains("--demo") { await command(["op":"demo"]) }
        else if reopenLast, !state.loaded, UserDefaults.standard.bool(forKey:"reopenLastDocument"), let path = UserDefaults.standard.string(forKey:"lastOpenedDocument"), FileManager.default.fileExists(atPath:path) { await command(["op":"open", "path":path]) }
    }
    func command(_ payload: [String: Any], status message: String? = nil) async {
        guard !busy else { return }
        busy = true
        do {
            let previous = state
            state = try await bridge.send(payload, as: EditorState.self)
            if payload["op"] as? String == "open", let path = payload["path"] as? String { UserDefaults.standard.set(path, forKey:"lastOpenedDocument") }
            if ["open", "demo"].contains(payload["op"] as? String ?? "") { drafts.removeAll(); section="Pokémon"; saveObject = nil; dexRecord=nil; dexDetails=nil; giftAlbum=nil; pendingAlbumGift=nil; research=nil; researchSpecies=25; storage=[]; moveRecords=nil; plusRecords=nil; encounters=nil }
            let catalogKinds=["species", "items", "moves", "abilities", "natures", "balls", "games"]
            if state.loaded && (previous.game != state.game || previous.entityExtension != state.entityExtension || !catalogKinds.allSatisfy({catalogs[$0] != nil})) {
                var loaded:[String:[Choice]]=[:]
                for kind in catalogKinds { loaded[kind] = try await bridge.send(["op":"lookup", "kind":kind], as: [Choice].self) }
                catalogs=loaded
            }
            if let message { status = message }
            else if payload["op"] as? String == "undo" {status="Undo complete"}
            else if payload["op"] as? String == "redo" {status="Redo complete"}
            if ["undo","redo","dexRecordSet"].contains(payload["op"] as? String ?? ""), let record=dexRecord, state.canDexRecords {dexRecord=try await bridge.send(["op":"dexRecord","species":record.species],as:DexRecordData.self)}
            if ["undo","redo"].contains(payload["op"] as? String ?? ""), plusRecords != nil, state.canPlusRecords {plusRecords=try await bridge.send(["op":"plusRecords"],as:MoveRecordData.self)}
            if ["undo","redo"].contains(payload["op"] as? String ?? ""), moveRecords != nil, state.canMoveRecords { moveRecords = try await bridge.send(["op":"moveRecords"],as:MoveRecordData.self) }
            if ["undo","redo"].contains(payload["op"] as? String ?? ""), let data=dexDetails, state.canResearch {dexDetails=try await bridge.send(["op":"dexDetails","species":data.species,"form":data.form],as:DexDetailData.self)}
            if section == "Advanced Save", state.hasSave { saveObject = try await bridge.send(["op":"saveObject", "path":saveObject?.path ?? ""], as: SaveObjectData.self) }
            if section == "Event Flags", state.hasSave { events = try await bridge.send(["op":"events"], as: EventData.self).entries }
            if section == "Inventory", state.canInventory { inventory = try await bridge.send(["op":"inventory"], as: InventoryData.self).pouches }
            if section == "Pokédex", state.hasSave { dex = try await bridge.send(["op":"dex"], as: DexData.self).entries }
            if section == "Storage", state.hasSave { storage = try await bridge.send(["op":"storage"],as:[StorageEntry].self) }
            if section == "Fashion", state.canFashion {fashion=try await bridge.send(["op":"fashion"],as:FashionData.self)}
            if section == "Research", state.canResearch { research = try await bridge.send(["op":"research","species":researchSpecies],as:ResearchData.self) }
            if section == "Gift Album", state.canGiftAlbum {giftAlbum=try await bridge.send(["op":"giftAlbum"],as:AlbumData.self)}
        } catch { self.error = error.localizedDescription }
        busy = false
    }
    func loadSection() async {
        guard !busy else { return }
        busy = true
        do {
            if section == "Pokémon" { state = try await bridge.send(["op":"state"], as: EditorState.self) }
            if section == "Mystery Gifts", gifts.isEmpty { gifts = try await bridge.send(["op":"gifts"],as:[GiftEntry].self) }
            if section == "Settings" { fields = try await bridge.send(["op":"settings"], as: [Field].self) }
            if section == "Advanced Save", state.hasSave { saveObject = try await bridge.send(["op":"saveObject", "path":saveObject?.path ?? ""], as: SaveObjectData.self) }
            if section == "Event Flags", state.hasSave { events = try await bridge.send(["op":"events"], as: EventData.self).entries }
            if section == "Inventory", state.canInventory { inventory = try await bridge.send(["op":"inventory"], as: InventoryData.self).pouches }
            if section == "Pokédex", state.hasSave { dex = try await bridge.send(["op":"dex"], as: DexData.self).entries }
            if section == "Storage", state.hasSave { storage = try await bridge.send(["op":"storage"],as:[StorageEntry].self) }
            if section == "Fashion", state.canFashion {fashion=try await bridge.send(["op":"fashion"],as:FashionData.self)}
            if section == "Research", state.canResearch { research = try await bridge.send(["op":"research","species":researchSpecies],as:ResearchData.self) }
            if section == "Gift Album", state.canGiftAlbum {giftAlbum=try await bridge.send(["op":"giftAlbum"],as:AlbumData.self)}
        } catch { self.error = error.localizedDescription }
        busy = false
    }
    func value(_ field: Field, target: String) -> String { drafts["\(target)|\(field.id)"] ?? field.value }
    func draft(_ value: String, field: Field, target: String) {
        let key = "\(target)|\(field.id)"
        if value == field.value { drafts.removeValue(forKey: key) } else { drafts[key] = value }
    }
    func commit(_ field: Field, target: String, value: String? = nil) async {
        guard !busy else { return }
        let key = "\(target)|\(field.id)", newValue = value ?? self.value(field, target: target)
        busy = true
        do {
            let request: [String: Any] = ["op": target + "Set", "field":field.id, "value":newValue]
            if target == "object" {
                var request = request; request["path"] = saveObject?.path ?? ""
                saveObject = try await bridge.send(request, as: SaveObjectData.self)
                state = try await bridge.send(["op":"state"], as: EditorState.self)
            }
            else if target == "inventoryField" {
                var request = request; request["pouch"] = itemPouch; request["slot"] = itemSlot
                inventoryExtra = try await bridge.send(request, as: [Field].self)
                state = try await bridge.send(["op":"state"], as: EditorState.self)
            }
            else if target == "fashion" {fashion=try await bridge.send(request,as:FashionData.self);state=try await bridge.send(["op":"state"],as:EditorState.self)}
            else if target == "settings" {
                fields = try await bridge.send(request, as: [Field].self)
                if field.id == "CatalogLanguage" {
                    catalogs.removeAll(); gifts.removeAll(); encounters = nil; library = nil
                    state = try await bridge.send(["op":"state"], as: EditorState.self)
                    for kind in ["species", "items", "moves", "abilities", "natures", "balls", "games"] { catalogs[kind] = try await bridge.send(["op":"lookup", "kind":kind], as:[Choice].self) }
                }
            }
            else { state = try await bridge.send(request, as: EditorState.self) }
            drafts.removeValue(forKey: key)
            status = "Updated \(field.label)"
        } catch { self.error = error.localizedDescription }
        busy = false
    }
    func editPokemon(_ id: String? = nil, value: String? = nil, apply: Bool = false, destination: Slot? = nil) async {
        guard !busy else { return }
        busy = true
        var values = drafts.filter { $0.key.hasPrefix("entity|") }
        if let id, let value { values["entity|" + id] = value }
        let ordered = values.keys.sorted { a, b in
            let priority = ["entity|Species", "entity|Form", "entity|Nickname"]
            return (priority.firstIndex(of:a) ?? 100, a) < (priority.firstIndex(of:b) ?? 100, b)
        }
        let edits = ordered.map { ["field":String($0.dropFirst(7)), "value":values[$0]!] }
        do {
            var request: [String:Any] = ["op":"entityEdit", "edits":edits, "apply":apply]
            if let destination { request["destinationBox"] = state.box; request["destinationSlot"] = destination.index; request["destinationParty"] = destination.party }
            state = try await bridge.send(request, as:EditorState.self)
            for key in values.keys { drafts.removeValue(forKey:key) }
            status = apply ? "Pokémon set to slot — export a copy to save" : "Pokémon updated"
        } catch { self.error = error.localizedDescription }
        busy = false
    }
    func fetchChoices(_ kind: String) async throws -> [Choice] {
        if let saved = catalogs[kind] { return saved }
        return try await bridge.send(["op":"lookup", "kind":kind], as: [Choice].self)
    }
    func choices(_ kind: String) async -> [Choice] {
        do { return try await fetchChoices(kind) }
        catch { self.error = error.localizedDescription; return [] }
    }
    func confirmDiscard(all: Bool = true, clearDrafts: Bool = true) -> Bool {
        guard all ? unsaved : state.pending || !drafts.isEmpty else { return true }
        let alert = NSAlert()
        alert.messageText = L(all ? "Discard unsaved changes?" : "Discard uncommitted edits?")
        alert.informativeText = all ? "Export a copy first to keep your changes. The original file has not been changed." : "Apply the edited Pokémon to its slot before choosing another slot, or discard these edits."
        alert.addButton(withTitle: L("Cancel")); alert.addButton(withTitle: L("Discard"))
        let discard = alert.runModal() == .alertSecondButtonReturn
        if discard && clearDrafts { drafts.removeAll() }
        return discard
    }
    func open() {
        guard !busy else { return }
        let panel = NSOpenPanel(); panel.title = "Open a Pokémon save or Pokémon file"; panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        openURL(url)
    }
    func openURL(_ url: URL) { _ = openFiles([url]) }
    @discardableResult func openFiles(_ urls: [URL]) -> Bool {
        guard !busy else { return false }
        do {
            let url = try FileDropSelection.file(from: urls)
            guard confirmDiscard(clearDrafts: false) else { return false }
            Task { await command(["op":"open", "path":url.path], status:"Opened \(url.lastPathComponent)") }
            return true
        } catch { self.error = error.localizedDescription; return false }
    }
    @Published var showSamplePicker=false
    func demo() { guard !busy else{return};showSamplePicker=true }
    func startSample(_ version:String) { guard !busy, confirmDiscard() else{return};showSamplePicker=false;Task { await command(["op":"demo","version":version],status:"Sample workspace — explore without a save file") } }
    func select(_ slot: Slot) {
        guard confirmDiscard(all: false) else { return }
        Task { await command(["op":"select", "box":state.box, "slot":slot.index, "party":slot.party]) }
    }
    func chooseBox(_ box: Int) {
        guard confirmDiscard(all: false) else { return }
        Task { await command(["op":"select", "box":box, "slot":0, "party":false]) }
    }
    func exportSave() {
        guard !busy else { return }
        guard drafts.isEmpty else { error = "Commit your typed edits with Return or Set to Slot before exporting."; return }
        guard !state.pending else { error = "Click Set to Slot to store the Pokémon edits before exporting the save."; return }
        let panel = NSSavePanel(); panel.title = "Export an edited copy"; panel.nameFieldStringValue = state.sourceName + ".edited"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        Task { await command(["op":"exportSave", "path":url.path], status:"Exported \(url.lastPathComponent)") }
    }
    func exportEntity() {
        guard !busy, !state.fields.isEmpty else { return }
        guard drafts.isEmpty else { error = "Commit your typed edits with Return or Set to Slot before exporting."; return }
        let panel = NSSavePanel(); panel.title = "Export Pokémon"; panel.nameFieldStringValue = state.entityName + "." + state.entityExtension
        guard panel.runModal() == .OK, let url = panel.url else { return }
        Task { await command(["op":"exportEntity", "path":url.path], status:"Exported Pokémon") }
    }
    func importEntity() {
        guard !busy, confirmDiscard(all: false) else { return }
        let panel = NSOpenPanel(); panel.title = "Import Pokémon into the editor"; panel.canChooseDirectories = false
        guard panel.runModal() == .OK, let url = panel.url else { return }
        Task { await command(["op":"importEntity", "path":url.path], status:"Imported Pokémon — apply it to a slot to keep it in the save") }
    }
    func copyShowdown() async {
        struct TextResult: Decodable { let text: String }
        do { let result = try await bridge.send(["op":"showdown"], as: TextResult.self); NSPasteboard.general.clearContents(); NSPasteboard.general.setString(result.text, forType:.string); status = "Copied Showdown set" }
        catch { self.error = error.localizedDescription }
    }
    func previewBatch(text: String, scope: String) async {
        guard !busy else { return }
        guard drafts.isEmpty else { error = "Commit or discard typed fields before previewing a batch."; return }
        busy = true; batch = nil
        do { batch = try await bridge.send(["op":"batchPreview", "text":text, "scope":scope], as: BatchPreview.self) }
        catch { self.error = error.localizedDescription }; busy = false
    }
    func navigateObject(_ path: String) async {
        guard !busy else { return }; busy = true
        do { saveObject = try await bridge.send(["op":"saveObject", "path":path], as: SaveObjectData.self) }
        catch { self.error = error.localizedDescription }; busy = false
    }
    func updateEvent(_ entry: EventEntry, value: Bool) async {
        guard !busy else { return }; busy = true
        do {
            events = try await bridge.send(["op":"eventSet", "index":entry.id, "value":value], as: EventData.self).entries
            state = try await bridge.send(["op":"state"], as: EditorState.self)
        } catch { self.error = error.localizedDescription }; busy = false
    }
    func inventoryDetails(pouch: Int, slot: Int) async {
        guard !busy else { return }; busy = true; itemPouch = pouch; itemSlot = slot; inventoryExtra = []
        do { inventoryExtra = try await bridge.send(["op":"inventoryFields", "pouch":pouch, "slot":slot], as: [Field].self) }
        catch { self.error = error.localizedDescription }; busy = false
    }
    func addInventory(pouch: Int, item: Int, count: Int) async -> Bool {
        guard !busy else { return false }; busy = true
        defer { busy = false }
        do {
            inventory = try await bridge.send(["op":"inventoryAdd", "pouch":pouch, "item":item, "count":count], as:InventoryData.self).pouches
            state = try await bridge.send(["op":"state"], as:EditorState.self)
            status = "Items added — export a copy to save"
            return true
        } catch { self.error = error.localizedDescription; return false }
    }
    func updateInventory(pouch: Int, slot: Int, item: Int, count: Int, giveMax: Bool = false) async {
        guard !busy else { return }; busy = true
        do {
            inventory = try await bridge.send(["op":"inventorySet", "pouch":pouch, "slot":slot, "item":item, "count":count, "max":giveMax], as: InventoryData.self).pouches
            state = try await bridge.send(["op":"state"], as: EditorState.self); drafts.removeValue(forKey:"quantity:\(pouch):\(slot)"); status = "Updated inventory"
        } catch { self.error = error.localizedDescription }; busy = false
    }
    func updateDex(_ entry: DexEntry, seen: Bool, caught: Bool) async {
        guard !busy else { return }; busy = true
        do {
            dex = try await bridge.send(["op":"dexSet", "species":entry.id, "seen":seen, "caught":caught], as: DexData.self).entries
            state = try await bridge.send(["op":"state"], as: EditorState.self)
        } catch { self.error = error.localizedDescription }; busy = false
    }
}
