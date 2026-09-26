import SwiftUI
import AppKit

@main struct PKHeXSwiftApp: App {
    init() {
        let defaults=UserDefaults.standard
        if defaults.object(forKey:"pokemonArtworkStyle") == nil {
            defaults.set(defaults.object(forKey:"showPokemonSprites") as? Bool == false ? "HD Portraits":"Sprites",forKey:"pokemonArtworkStyle")
        }
    }
    @NSApplicationDelegateAdaptor(AppDelegate.self) var delegate
    @StateObject private var model = EditorModel()
    @StateObject private var journals = JournalStore()
    @StateObject private var updater = UpdateChecker.shared
    @Environment(\.openWindow) private var openWindow
    @AppStorage("appearance") private var appearance = "System"
    @AppStorage("gameTheme") private var themeID = "classic"
    @AppStorage("matchGameTheme") private var matchGame=true
    @AppStorage("customThemeColors") private var customColors=false
    @AppStorage("themeAccentHex") private var accentHex="6750A4"
    @AppStorage("themeCompanionHex") private var companionHex="E99A70"
    private var theme:GameTheme {
        var value=matchGame && model.state.hasSave ? GameTheme.forGame(model.state.gameVersion) : GameTheme.named(themeID)
        if customColors {value.accentOverride=Color(hex:accentHex);value.companionOverride=Color(hex:companionHex)}
        return value
    }
    var body: some Scene {
        Window("KeepSake", id: "main") {
            ContentView().environmentObject(model).environmentObject(journals)
                .environment(\.gameTheme,theme)
                .environment(\.pokemonArtworkGame,model.state.gameVersion)
                .tint(theme.accent).accentColor(theme.accent)
                .preferredColorScheme(appearance == "Dark" ? .dark : appearance == "Light" ? .light : nil)
                .frame(minWidth: 1050, minHeight: 700)
                .task { delegate.model = model; await model.start() }
                .onOpenURL { model.openURL($0) }
                .task { while !Task.isCancelled { await updater.check(automatic:true); do { try await Task.sleep(for:.seconds(3600)) } catch { break } } }
                .alert("A new chapter is ready",isPresented:$updater.showAnnouncement) { Button("View Update") { updater.openRelease() }; Button("Later",role:.cancel) {} } message: { Text(updater.message) }
        }
        .defaultSize(width: 1280, height: 850)
        .commands {
            CommandGroup(replacing: .appInfo) {
                Button("About KeepSake") { openWindow(id:"about") }
                Button("Check for Updates…") { openWindow(id:"about"); Task { await updater.check() } }.disabled(updater.checking)
            }
            CommandGroup(replacing: .help) { Button("KeepSake Support") { openWindow(id:"support") } }
            CommandGroup(replacing: .newItem) {
                Button("Open Save or Pokémon…") { model.open() }.keyboardShortcut("o").disabled(model.busy)
                Button("Explore Sample Workspace") { model.demo() }.disabled(model.busy)
            }
            CommandGroup(replacing: .saveItem) {
                Button("Export Edited Save…") { model.exportSave() }.keyboardShortcut("s", modifiers: [.command, .shift]).disabled(!model.state.hasSave || model.state.demo || model.busy)
                Button("Export Pokémon…") { model.exportEntity() }.disabled(model.state.fields.isEmpty || model.busy)
            }
            CommandGroup(replacing: .undoRedo) {
                Button("Undo") { Task { await model.command(["op":"undo"]) } }.keyboardShortcut("z").disabled(!model.state.canUndo || model.busy || model.fieldDrafts)
                Button("Redo") { Task { await model.command(["op":"redo"]) } }.keyboardShortcut("z", modifiers: [.command, .shift]).disabled(!model.state.canRedo || model.busy || model.fieldDrafts)
            }
            CommandGroup(replacing: .appSettings) {
                Button("Settings…") { model.section = "Settings" }.keyboardShortcut(",")
            }
        }
        Window("About KeepSake",id:"about") { AboutKeepSakeView().environment(\.gameTheme,theme).preferredColorScheme(appearance == "Dark" ? .dark : appearance == "Light" ? .light : nil).frame(minWidth:520,minHeight:600) }.defaultSize(width:580,height:700)
        Window("KeepSake Support",id:"support") { KeepSakeSupportView().environmentObject(journals).environment(\.gameTheme,theme).preferredColorScheme(appearance == "Dark" ? .dark : appearance == "Light" ? .light : nil).frame(minWidth:600,minHeight:600) }.defaultSize(width:680,height:800)
    }
}
@MainActor final class AppDelegate: NSObject, NSApplicationDelegate {
    weak var model: EditorModel?
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        if model?.busy == true { return .terminateCancel }
        if model?.approvedClose != true && model?.confirmDiscard() == false { return .terminateCancel }
        model?.bridge.stop(); return .terminateNow
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}
struct WindowCloseGuard: NSViewRepresentable {
    @EnvironmentObject var model: EditorModel
    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async { if let window = view.window { context.coordinator.original = window.delegate; window.delegate = context.coordinator } }
        return view
    }
    func updateNSView(_ nsView: NSView, context: Context) {}
    func makeCoordinator() -> Coordinator { Coordinator(model: model) }
    @MainActor class Coordinator: NSObject, NSWindowDelegate {
        let model: EditorModel
        weak var original: NSWindowDelegate?
        init(model: EditorModel) { self.model = model }
        func windowShouldClose(_ sender: NSWindow) -> Bool {
            let close = !model.busy && model.confirmDiscard()
            model.approvedClose = close
            return close
        }
    }
}

struct ContentView: View {
    @Environment(\.gameTheme) private var theme
    @AppStorage("themeDecorations") private var decorations=true
    @EnvironmentObject var model: EditorModel
    @State private var showAppearance=false
    @State private var fileTargeted=false
    @State private var visibility: NavigationSplitViewVisibility = .detailOnly
    private var sections:[(String,String)] { baseSections.filter { name,_ in
        switch name {case "Treats":return model.state.canTreats;case "Research":return model.state.canResearch;case "Fashion":return model.state.canFashion;case "Gift Album":return model.state.canGiftAlbum;case "Inventory":return model.state.canInventory;case "Pokédex":return model.state.canDex;case "Event Flags":return model.state.canEvents;default:return true}
    }}
    private let baseSections: [(String, String)] = [("My Journal","book.closed.fill"),("Pokémon","square.grid.3x3"),("Trainer","person.crop.circle"),("Inventory","backpack"),("Storage","tray.full"),("Encounters","binoculars"),("File Library","folder"),("Mystery Gifts","gift"),("Gift Album","giftcard"),("Research","list.clipboard"),("Fashion","tshirt.fill"),("Treats","birthday.cake.fill"),("Pokédex","book.closed"),("Game Extras","sparkles.rectangle.stack.fill"),("Save Tools","wrench.and.screwdriver"),("Advanced Save","slider.horizontal.3"),("Event Flags","flag"),("Batch Editor","square.stack.3d.up")]
    var body: some View {
        NavigationSplitView(columnVisibility:$visibility) {
            VStack(alignment: .leading, spacing: 0) {
                HStack(spacing: 10) {
                    JournalMark().frame(width: 34, height: 34)
                    VStack(alignment: .leading, spacing: 1) { Text("KeepSake").font(.title2.bold()); Text("POKÉMON JOURNAL").font(.system(size: 9, weight: .semibold, design: .rounded)).tracking(1.8).foregroundStyle(.secondary) }
                }.padding(20)
                List(selection: $model.section) {
                    Section("Workspace") {
                        ForEach(sections, id: \.0) { name, icon in Label(name, systemImage: icon).tag(name).padding(.vertical, 5) }
                    }
                    Section("Application") {
                        Label("Settings", systemImage:"gearshape").tag("Settings").padding(.vertical, 5)
                        Label("Port Coverage", systemImage:"checklist").tag("Port Coverage").padding(.vertical, 5)
                    }
                }.listStyle(.sidebar)
                VStack(alignment: .leading, spacing: 6) {
                    Label("Native SwiftUI", systemImage: "apple.logo").font(.caption.weight(.medium))
                    Text("PKHeX engine \(model.state.engineVersion)").font(.caption2).foregroundStyle(.secondary)
                    Text("Local files · Offline editing").font(.caption2).foregroundStyle(.tertiary)
                }.padding(18)
            }.navigationSplitViewColumnWidth(min: 190, ideal: 205, max: 250)
        } detail: {
            VStack(spacing: 0) {
                if model.state.loaded && model.section != "Settings" && model.section != "Port Coverage" { documentHeader }
                Group {
                    if model.section == "Settings" { PreferencesView() }
                    else if model.section == "My Journal" { JournalLibrary() }
                    else if model.section == "Port Coverage" { CoverageView() }
                    else if model.section == "Mystery Gifts" { GiftDatabaseView() }
                    else if model.section == "Gift Album" { GiftAlbumView() }
                    else if model.section == "File Library" { PokemonLibraryView() }
                    else if model.section == "Encounters" { EncountersView() }
                    else if model.section == "Game Extras" { GameExtrasView() }
                    else if !model.state.loaded { WelcomeView() }
                    else if model.section == "Pokémon" { PokemonWorkspace() }
                    else if model.section == "Storage" { StorageView() }
                    else if model.section == "Research" { ResearchView() }
                    else if model.section == "Trainer" { TrainerCardView() }
                    else if model.section == "Fashion" { FashionView() }
                    else if model.section == "Treats" { TreatsView() }
                    else if model.section == "Save Tools" { SaveToolsView() }
                    else if model.section == "Advanced Save" { AdvancedSaveView() }
                    else if model.section == "Event Flags" { EventFlagsView() }
                    else if model.section == "Batch Editor" { BatchEditorView() }
                    else if model.section == "Inventory" { InventoryView() }
                    else { DexView() }
                }.frame(maxWidth: .infinity, maxHeight: .infinity)
                Divider()
                HStack(spacing: 8) {
                    if model.busy { ProgressView().controlSize(.mini) } else { Circle().fill(.green).frame(width: 5, height: 5) }
                    Text(model.busy ? "Working…" : model.status).lineLimit(1)
                    Spacer()
                    if !model.drafts.isEmpty { Text("Uncommitted fields").foregroundStyle(.orange) }
                    else if model.state.pending { Text("Pokémon edits pending").foregroundStyle(.orange) }
                    else if model.state.dirty { Text("Save has unexported edits").foregroundStyle(.orange) }
                }.font(.caption).foregroundStyle(.secondary).padding(.horizontal, 18).frame(height: 30)
            }.background(Color(nsColor: .windowBackgroundColor))
            .toolbar {
                ToolbarItem(placement: .navigation) {
                    Menu {
                        ForEach(sections, id:\.0) { name, icon in Button { model.section=name } label: { Label(name,systemImage:icon) } }
                        Button {model.section="Settings"} label:{Label("Settings",systemImage:"gearshape")}
                        Button {model.section="Port Coverage"} label:{Label("Port Coverage",systemImage:"checklist")}
                    } label: {
                        HStack(spacing:8) {Image(systemName:sections.first{$0.0==model.section}?.1 ?? "book.closed");Text(model.section).font(.headline).lineLimit(1)}.frame(minWidth:160,alignment:.leading)
                    }.menuStyle(.borderlessButton).fixedSize().help("Current page: "+model.section)
                }
                ToolbarItemGroup(placement: .primaryAction) {
                    Button { showAppearance=true } label: { HStack(spacing:6) { Image(systemName:"paintpalette.fill"); Text("Appearance") } }.help("Game themes, custom colors, and light or dark appearance")
                    Button { model.open() } label: { Label("Open", systemImage:"folder") }.help("Open save or Pokémon file (⌘O)")
                    Button { Task { await model.command(["op":"undo"]) } } label: { Image(systemName:"arrow.uturn.backward") }.disabled(!model.state.canUndo || model.fieldDrafts).help("Undo")
                    Button { model.exportSave() } label: { Label("Export Copy", systemImage:"square.and.arrow.up") }.disabled(!model.state.hasSave || model.state.demo)
                }
            }
        }
        .disabled(model.busy)
        .background(WindowCloseGuard())
        .dropDestination(for:FileDropItem.self) { files,_ in model.openFiles(files.map(\.url)) } isTargeted: { fileTargeted=$0 }
        .overlay {
            if fileTargeted && !model.busy {
                ZStack {
                    RoundedRectangle(cornerRadius:24).fill(.regularMaterial)
                    RoundedRectangle(cornerRadius:24).strokeBorder(theme.accent,style:StrokeStyle(lineWidth:3,dash:[10,6]))
                    VStack(spacing:14) {
                        Image(systemName:"doc.badge.arrow.up").font(.system(size:44)).foregroundStyle(theme.accent)
                        Text("Open your next adventure").font(.title2.bold())
                        Text("Drop one save or Pokémon file here").foregroundStyle(.secondary)
                        Text("Your original file stays untouched.").font(.caption).foregroundStyle(.secondary)
                    }
                }.padding(20).allowsHitTesting(false)
            }
        }
        .sheet(isPresented:$model.showSamplePicker) { SampleWorkspacePicker() }
        .sheet(isPresented:$showAppearance) { AppearanceSheet() }
        .onChange(of: model.state.gameVersion) { _,_ in if !sections.contains(where:{$0.0==model.section}) && !["Settings","Port Coverage"].contains(model.section) {model.section="Pokémon"} }
        .onChange(of: model.section) { _, _ in Task { await model.loadSection() } }
        .alert("Couldn’t complete that action", isPresented: Binding(get:{ model.error != nil }, set:{ if !$0 { model.error = nil } })) { Button("OK") { model.error = nil } } message: { Text(model.error ?? "") }
    }
    private var documentHeader: some View {
        HStack(spacing: 12) {
            Image(systemName: model.state.demo ? "sparkles" : "doc.fill").foregroundStyle(theme.accent).font(.title2)
            VStack(alignment: .leading, spacing: 3) {
                Text(model.state.sourceName).font(.headline)
                Text("\(model.state.game) · Generation \(model.state.generation)").font(.caption).foregroundStyle(.secondary)
            }
            Spacer()
            if model.state.hasSave { SaveGameLogo(version:model.state.gameLogoVersion,name:model.state.game) }
            if model.state.demo { Pill(text: "SAMPLE", color: .orange) }
            else if model.state.dirty { Pill(text: "EDITED", color: .blue).help("Unexported changes. Export Copy recalculates and verifies save checksums.") }
            else if !model.state.checksumValid { Pill(text: "CHECKSUM WARNING", color: .orange) }
            if model.state.hasSave { Text("\(model.state.boxCount) boxes").font(.caption).foregroundStyle(.secondary) }
        }.padding(.horizontal, 24).padding(.vertical, 15).background(LinearGradient(colors:[theme.accent.opacity(0.23),theme.companion.opacity(0.17)],startPoint:.leading,endPoint:.trailing))
    }
}

struct Pill: View {
    @Environment(\.gameTheme) private var theme
    let text: String; let color: Color
    var body: some View { Text(text).font(.system(size: 10, weight: .bold)).tracking(0.8).foregroundStyle(color).padding(.horizontal, 9).padding(.vertical, 5).background(color.opacity(0.1), in: Capsule()) }
}
struct PokeballView: View {
    @Environment(\.gameTheme) private var theme
    var body: some View {
        GeometryReader { g in
            ZStack {
                Circle().fill(LinearGradient(colors: [theme.accent, .purple], startPoint:.topLeading, endPoint:.bottomTrailing))
                Rectangle().fill(.white.opacity(0.75)).frame(height:g.size.height * 0.075)
                Circle().fill(.white).frame(width:g.size.width * 0.32, height:g.size.height * 0.32)
                Circle().fill(theme.accent).frame(width:g.size.width * 0.17, height:g.size.height * 0.17)
            }.clipShape(Circle())
        }.aspectRatio(1, contentMode:.fit)
    }
}
struct WelcomeView: View {
    @AppStorage("keepsakeOwnerName") private var ownerName=""
    @Environment(\.gameTheme) private var theme
    @EnvironmentObject var model: EditorModel
    var body: some View {
        VStack(spacing: 22) {
            JournalMark().frame(width: 86, height: 86).shadow(color:theme.accent.opacity(0.2), radius:25, y:8)
            VStack(spacing: 10) {
                Text(ownerName.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty ? "Your next chapter starts here." : "Welcome back, \(ownerName).").font(.system(size: 30, weight:.bold, design:.rounded))
                Text("Open or drop a game save or Pokémon file to begin.\nPowered by the original PKHeX save and legality engine.")
                    .font(.body).foregroundStyle(.secondary).multilineTextAlignment(.center).lineSpacing(4)
            }
            TextField("What should we call you?",text:$ownerName).textFieldStyle(.roundedBorder).frame(width:280).onChange(of:ownerName){_,value in ownerName=String(value.prefix(50))}.accessibilityLabel("Your name")
            Text("Your name is saved on this Mac for your welcome screen.").font(.caption).foregroundStyle(.secondary)
            HStack(spacing: 12) {
                Button("Open a File…") { model.open() }.buttonStyle(.borderedProminent).controlSize(.large)
                Button("Explore a Sample") { model.demo() }.controlSize(.large)
            }.padding(.top, 6)
            HStack(spacing: 24) {
                Label("Pokémon & boxes", systemImage:"square.grid.3x3")
                Label("Trainer & inventory", systemImage:"backpack")
                Label("Legality checks", systemImage:"checkmark.shield")
            }.font(.caption).foregroundStyle(.secondary).padding(.top, 18)
            Text("Your companions. Your memories. Your KeepSake.")
                .font(.caption).foregroundStyle(.tertiary).padding(.top, 28)
            Button("View feature coverage") { model.section = "Port Coverage" }.buttonStyle(.link).font(.caption)
        }.padding(40)
    }
}
