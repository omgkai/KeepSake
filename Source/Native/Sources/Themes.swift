import SwiftUI
import AppKit

struct GameTheme: Identifiable {
    let id:String, name:String, region:String, mascot:String, symbol:String
    let hue:Double, companionHue:Double
    var accentOverride: Color? = nil, companionOverride: Color? = nil
    var accent:Color { accentOverride ?? Color(hue:hue,saturation:0.63,brightness:0.78) }
    var companion:Color { companionOverride ?? Color(hue:companionHue,saturation:0.55,brightness:0.92) }
    static let all:[GameTheme] = [
        .init(id:"classic",name:"Classic",region:"A little of every adventure",mascot:"b_25",symbol:"sparkles",hue:0.66,companionHue:0.77),
        .init(id:"kanto",name:"Red & Blue",region:"Kanto",mascot:"b_1",symbol:"leaf.fill",hue:0.99,companionHue:0.59),
        .init(id:"johto",name:"Gold & Silver",region:"Johto",mascot:"b_155",symbol:"sun.max.fill",hue:0.105,companionHue:0.60),
        .init(id:"hoenn",name:"Ruby & Sapphire",region:"Hoenn",mascot:"b_258",symbol:"drop.fill",hue:0.57,companionHue:0.97),
        .init(id:"emerald",name:"Emerald",region:"Hoenn's green skies",mascot:"b_252",symbol:"leaf.fill",hue:0.40,companionHue:0.14),
        .init(id:"sinnoh",name:"Diamond & Pearl",region:"Sinnoh",mascot:"b_393",symbol:"snowflake",hue:0.61,companionHue:0.89),
        .init(id:"unova",name:"Black & White",region:"Unova",mascot:"b_501",symbol:"circle.lefthalf.filled",hue:0.54,companionHue:0.68),
        .init(id:"kalos",name:"X & Y",region:"Kalos",mascot:"b_653",symbol:"sparkle",hue:0.59,companionHue:0.97),
        .init(id:"alola",name:"Sun & Moon",region:"Alola",mascot:"b_722",symbol:"moon.stars.fill",hue:0.07,companionHue:0.73),
        .init(id:"galar",name:"Sword & Shield",region:"Galar",mascot:"b_813",symbol:"shield.fill",hue:0.54,companionHue:0.92),
        .init(id:"hisui",name:"Legends: Arceus",region:"Hisui",mascot:"b_155",symbol:"mountain.2.fill",hue:0.46,companionHue:0.12),
        .init(id:"za",name:"Legends: Z-A",region:"Lumiose City",mascot:"b_152",symbol:"building.2.fill",hue:0.43,companionHue:0.69),
        .init(id:"scarlet",name:"Scarlet",region:"Paldea · Naranja Academy",mascot:"b_909",symbol:"sun.max.fill",hue:0.015,companionHue:0.09),
        .init(id:"violet",name:"Violet",region:"Paldea · Uva Academy",mascot:"b_912",symbol:"sparkles",hue:0.74,companionHue:0.63),
        .init(id:"paldea",name:"Scarlet & Violet",region:"Paldea",mascot:"b_906",symbol:"sun.horizon.fill",hue:0.77,companionHue:0.04)
    ]
    static func forGame(_ version:String)->GameTheme {
        let mapping:[String:String] = ["RD":"kanto","GN":"kanto","BU":"kanto","YW":"kanto","FR":"kanto","LG":"kanto","GP":"kanto","GE":"kanto","GD":"johto","SI":"johto","C":"johto","HG":"johto","SS":"johto","R":"hoenn","S":"hoenn","OR":"hoenn","AS":"hoenn","E":"emerald","D":"sinnoh","P":"sinnoh","Pt":"sinnoh","BD":"sinnoh","SP":"sinnoh","B":"unova","W":"unova","B2":"unova","W2":"unova","X":"kalos","Y":"kalos","SN":"alola","MN":"alola","US":"alola","UM":"alola","SW":"galar","SH":"galar","PLA":"hisui","SL":"scarlet","VL":"violet","ZA":"za"]
        return named(mapping[version] ?? "classic")
    }
    static func named(_ id:String) -> GameTheme { all.first{$0.id==id} ?? all[0] }
}
private struct GameThemeKey:EnvironmentKey { static let defaultValue=GameTheme.all[0] }
extension EnvironmentValues { var gameTheme:GameTheme {get{self[GameThemeKey.self]} set{self[GameThemeKey.self]=newValue}} }

struct ThemePicker:View {
    @EnvironmentObject var model:EditorModel
    @Environment(\.gameTheme) private var current
    @AppStorage("gameTheme") private var selected="classic"
    @AppStorage("matchGameTheme") private var matchGame=true
    @AppStorage("customThemeColors") private var custom=false
    @AppStorage("themeAccentHex") private var accentHex="6750A4"
    @AppStorage("themeCompanionHex") private var companionHex="E99A70"
    @AppStorage("appearance") private var appearance="System"
    @AppStorage("themeDecorations") private var decorations=true
    @AppStorage("pokemonArtworkStyle") private var artworkStyle="Sprites"
    @AppStorage("boxSlotOpacity") private var slotOpacity=0.82
    @AppStorage("boxSlotBorders") private var slotBorders=true
    @AppStorage("themeTintStrength") private var tintStrength=1.0
    @State private var section:String
    init(initialSection:String="Colors") { _section=State(initialValue:initialSection) }
    private var activeTheme:String {custom ? "" : matchGame && model.state.hasSave ? GameTheme.forGame(model.state.gameVersion).id : selected}
    var body:some View {
        VStack(alignment:.leading,spacing:20) {
            AppearancePreview()
            Picker("Appearance section",selection:$section) {
                Text("Colors").tag("Colors"); Text("Display").tag("Display"); Text("Motion & hover").tag("Motion")
            }.pickerStyle(.segmented).labelsHidden()
            if section == "Colors" { colors }
            else if section == "Display" { display }
            else { AppearanceCard(title:"A little personality",symbol:"hand.draw.fill") { MotionPreferences() } }
            Label("Saved on this Mac. Your Pokémon and save files stay unchanged.",systemImage:"lock.shield")
                .font(.caption).foregroundStyle(.secondary)
        }.frame(maxWidth:820).frame(maxWidth:.infinity)
    }
    private var colors:some View {
        VStack(spacing:18) {
            AppearanceCard(title:"Your palette",symbol:"paintpalette.fill") {
                Picker("Appearance",selection:$appearance) { ForEach(["System","Light","Dark"],id:\.self) {Text($0)} }.pickerStyle(.segmented)
                Toggle("Follow the loaded game’s palette",isOn:$matchGame).toggleStyle(.switch)
                Toggle("Use my custom colors",isOn:$custom).toggleStyle(.switch)
                HStack(spacing:24) {
                    ColorPicker("Accent",selection:colorBinding($accentHex),supportsOpacity:false)
                    ColorPicker("Companion",selection:colorBinding($companionHex),supportsOpacity:false)
                }
                ColorPresetsPicker(first:$accentHex,second:$companionHex,onSelect:{custom=true})
            }
            AppearanceCard(title:"Game themes",symbol:"map.fill") {
                Text("Choose a palette, then fine-tune it with your own colors.").font(.callout).foregroundStyle(.secondary)
                LazyVGrid(columns:[GridItem(.adaptive(minimum:170))],spacing:10) {
                    ForEach(GameTheme.all) { theme in
                        Button { selected=theme.id; matchGame=false; custom=false } label: {
                            VStack(alignment:.leading,spacing:8) {
                                HStack {
                                    PokemonSprite(name:theme.mascot).frame(width:42,height:32)
                                    Spacer()
                                    Image(systemName:activeTheme == theme.id ? "checkmark.circle.fill" : theme.symbol).foregroundStyle(theme.accent)
                                }
                                Text(theme.name).font(.system(size:12,weight:.semibold)).foregroundStyle(.primary)
                                Text(theme.region).font(.caption2).foregroundStyle(.secondary).lineLimit(1)
                                HStack(spacing:4) { Capsule().fill(theme.accent); Capsule().fill(theme.companion) }.frame(height:4)
                            }.padding(12).frame(maxWidth:.infinity,alignment:.leading)
                                .background(theme.accent.opacity(0.08),in:RoundedRectangle(cornerRadius:12))
                                .overlay(RoundedRectangle(cornerRadius:12).strokeBorder(activeTheme == theme.id ? theme.accent : .clear,lineWidth:2))
                        }.buttonStyle(.plain).accessibilityLabel(theme.name + (activeTheme == theme.id ? ", selected" : " theme"))
                    }
                }
            }
        }
    }
    private var display:some View {
        VStack(spacing:18) {
            AppearanceCard(title:"Pokémon artwork",symbol:"photo") {
                Picker("Artwork style",selection:$artworkStyle) {Text("Pixel sprites").tag("Sprites");Text("HOME portraits").tag("HD Portraits");Text("Game portraits").tag("Game Portraits")}.pickerStyle(.segmented)
                Text("Pixel sprites stay consistent across the app. HOME uses high-resolution portraits. Game portraits use Scarlet/Violet artwork, or Arceus portraits for an Arceus save. Missing game variants—including shiny Scarlet/Violet portraits—use matching HOME artwork, then a pixel sprite.").font(.callout).foregroundStyle(.secondary)
            }
            AppearanceCard(title:"Individual box slots",symbol:"square.grid.3x3.fill") {
                HStack {Text("Background opacity");Spacer();Text(slotOpacity,format:.percent.precision(.fractionLength(0))).monospacedDigit().foregroundStyle(.secondary)}
                Slider(value:$slotOpacity,in:0...1).accessibilityLabel("Individual Pokémon slot background opacity")
                HStack {Text("Transparent");Spacer();Text("Solid")}.font(.caption).foregroundStyle(.secondary)
                Toggle("Show slot outlines",isOn:$slotBorders).toggleStyle(.switch)
                Text("Changes each storage slot’s fill. Wallpaper and Pokémon artwork keep their own opacity. The selected slot always has a colored outline.").font(.callout).foregroundStyle(.secondary)
                Button("Reset slot appearance") {slotOpacity=0.82;slotBorders=true}
            }
            AppearanceCard(title:"Finishing touches",symbol:"sparkles") {
                Toggle("Decorative background motifs",isOn:$decorations).toggleStyle(.switch)
                HStack {Text("Background color tint");Spacer();Text(tintStrength,format:.percent.precision(.fractionLength(0))).monospacedDigit().foregroundStyle(.secondary)}
                Slider(value:$tintStrength,in:0...1).accessibilityLabel("Background color tint")
            }
        }
    }
    private func colorBinding(_ storage:Binding<String>)->Binding<Color> {
        Binding(get:{ Color(hex:storage.wrappedValue) },set:{ value in storage.wrappedValue=value.rgbHex;custom=true })
    }
}
struct AppearanceCard<Content:View>:View {
    let title:String, symbol:String
    @ViewBuilder let content:Content
    var body:some View {
        VStack(alignment:.leading,spacing:14) {
            Label(title,systemImage:symbol).font(.headline)
            content
        }.padding(18).frame(maxWidth:.infinity,alignment:.leading)
            .background(Color(nsColor:.controlBackgroundColor).opacity(0.65),in:RoundedRectangle(cornerRadius:16))
            .overlay(RoundedRectangle(cornerRadius:16).strokeBorder(.primary.opacity(0.06)))
    }
}
struct AppearancePreview:View {
    @Environment(\.gameTheme) private var theme
    var body:some View {
        HStack(spacing:24) {
            VStack(alignment:.leading,spacing:7) {
                Label("Appearance preview",systemImage:"book.closed.fill").font(.headline)
                Text("A live look at your colors, artwork and box slots.").font(.callout).foregroundStyle(.secondary)
            }.frame(maxWidth:.infinity,alignment:.leading)
            HStack(spacing:8) {
                preview("b_25","Pikachu",selected:true)
                preview("b_133","Eevee",selected:false)
                VStack(spacing:5) {Image(systemName:"circle.dashed").frame(height:28);Text("3").font(.system(size:9))}
                    .foregroundStyle(.secondary).frame(width:64,height:60).modifier(BoxSlotSurface(selected:false,empty:true,party:false))
            }
        }.padding(20)
            .background(LinearGradient(colors:[theme.accent.opacity(0.22),theme.companion.opacity(0.18)],startPoint:.topLeading,endPoint:.bottomTrailing),in:RoundedRectangle(cornerRadius:18))
    }
    private func preview(_ sprite:String,_ name:String,selected:Bool)->some View {
        VStack(spacing:5) {PokemonSprite(name:sprite).frame(width:36,height:28);Text(name).font(.system(size:9,weight:.medium))}
            .frame(width:64,height:60).modifier(BoxSlotSurface(selected:selected,empty:false,party:false))
    }
}
struct ThemeBackdrop:View {
    @AppStorage("themeTintStrength") private var tintStrength=1.0
    @Environment(\.gameTheme) private var theme
    @AppStorage("themeDecorations") private var decorations=true
    var body:some View {
        ZStack(alignment:.bottomTrailing) {
            LinearGradient(colors:[theme.accent.opacity(0.14 * tintStrength),theme.companion.opacity(0.08 * tintStrength),.clear],startPoint:.topLeading,endPoint:.bottomTrailing)
            if decorations {
                Image(systemName:theme.symbol).font(.system(size:170,weight:.ultraLight)).foregroundStyle(theme.accent.opacity(0.045)).padding(28).rotationEffect(.degrees(-12))
            }
        }.allowsHitTesting(false).accessibilityHidden(true)
    }
}

extension Color {
    init(hex:String) {
        let rgb=UInt64(hex,radix:16) ?? 0x6750A4
        self.init(.sRGB,red:Double((rgb>>16)&255)/255,green:Double((rgb>>8)&255)/255,blue:Double(rgb&255)/255,opacity:1)
    }
    var rgbHex:String {
        guard let c=NSColor(self).usingColorSpace(.sRGB) else{return "6750A4"}
        return String(format:"%02X%02X%02X",Int((min(1,max(0,c.redComponent))*255).rounded()),Int((min(1,max(0,c.greenComponent))*255).rounded()),Int((min(1,max(0,c.blueComponent))*255).rounded()))
    }
}
struct AppearanceSheet:View {
    @Environment(\.dismiss) private var dismiss
    var body:some View {
        VStack(spacing:0) {
            HStack {Label("Appearance",systemImage:"paintpalette.fill").font(.title2.bold());Spacer();Button("Done"){dismiss()}.keyboardShortcut(.cancelAction)}.padding(22)
            Divider()
            ScrollView {ThemePicker().padding(22)}
        }.frame(width:760,height:650)
    }
}
struct SaveGameLogo:View {
    let version:String, name:String
    private var groups:[String:[String]] {["RB":["RD","BU"],"RBY":["RD","BU","YW"],"GS":["GD","SI"],"GSC":["GD","SI","C"],"RS":["R","S"],"RSE":["R","S","E"],"FRLG":["FR","LG"],"DP":["D","P"],"DPPt":["D","P","Pt"],"HGSS":["HG","SS"],"BW":["B","W"],"B2W2":["B2","W2"],"XY":["X","Y"],"ORAS":["OR","AS"],"ORASDEMO":["OR","AS"],"SM":["SN","MN"],"USUM":["US","UM"],"GG":["GP","GE"],"SWSH":["SW","SH"],"BDSP":["BD","SP"],"SV":["SL","VL"],"CXD":["COLO","XD"]]}
    private func artwork(_ code:String)->NSImage? {NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/GameLogos/"+code+".png"))}
    private var legacyTitle:String? {["RD":"RED VERSION","GN":"BLUE / GREEN","BU":"BLUE VERSION","GD":"GOLD VERSION","SI":"SILVER VERSION","C":"CRYSTAL VERSION","RSBOX":"BOX · RUBY & SAPPHIRE","StadiumJ":"POCKET MONSTERS STADIUM","Stadium2":"STADIUM 2"][version]}
    private var legacyColor:Color {switch version {case "RD":return .red;case "GN":return .teal;case "GD":return .orange;case "SI":return .gray;case "C":return .purple;default:return .blue}}
    var body:some View {
        Group {
            if let title=legacyTitle,let mark=artwork("Pokemon") {VStack(spacing:1){Image(nsImage:mark).resizable().scaledToFit().frame(height:32);Text(title).font(.system(size:9,weight:.black,design:.rounded)).tracking(0.7).foregroundStyle(legacyColor).lineLimit(2).multilineTextAlignment(.center)}.frame(width:120,height:54)}
            else if ["RB","RBY","GS","GSC"].contains(version),let mark=artwork("Pokemon") {VStack(spacing:1){Image(nsImage:mark).resizable().scaledToFit().frame(height:32);Text(["RB":"RED / BLUE","RBY":"RED / BLUE / YELLOW","GS":"GOLD / SILVER","GSC":"GOLD / SILVER / CRYSTAL"][version] ?? name).font(.system(size:9,weight:.black,design:.rounded)).tracking(0.5).foregroundStyle(Color.accentColor).lineLimit(1)}.frame(width:150,height:54)}
            else if let parts=groups[version] {HStack(spacing:3){ForEach(parts,id:\.self){part in if let logo=artwork(part){Image(nsImage:logo).resizable().scaledToFit()}}}.frame(width:150,height:54)}
            else if let logo=artwork(version) {Image(nsImage:logo).resizable().interpolation(.high).scaledToFit().frame(width:120,height:54)}
            else {VStack(spacing:2){if let mark=artwork("Pokemon"){Image(nsImage:mark).resizable().scaledToFit().frame(height:30)};Text(name).font(.system(size:10,weight:.bold,design:.rounded)).multilineTextAlignment(.center)}.frame(width:140,height:54)}
        }.accessibilityLabel("Loaded save: "+name).help("Loaded save: "+name)
    }
}
