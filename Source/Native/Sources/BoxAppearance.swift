import SwiftUI

struct BoxAppearanceView: View {
    @Environment(\.dismiss) private var dismiss
    @AppStorage private var override: String
    let stored: String
    @State private var collection = "bdsp"
    private let collections = [("bdsp","Diamond & Pearl"),("rs","Ruby & Sapphire"),("e","Emerald"),("dp","Sinnoh classics"),("bw","Black & White"),("xy","X & Y"),("swsh","Sword & Shield"),("sv","Scarlet & Violet")]
    init(game: String, box: Int, stored: String) {
        _override = AppStorage(wrappedValue:"", "boxWallpaper.\(game).\(box)")
        self.stored = stored
    }
    private var wallpapers: [String] {
        (Bundle.main.urls(forResourcesWithExtension:"png",subdirectory:"Wallpapers") ?? [])
            .map { $0.deletingPathExtension().lastPathComponent }.filter { $0.hasSuffix(collection) }.sorted()
    }
    var body: some View {
        VStack(alignment:.leading,spacing:16) {
            HStack { Text("Box Wallpaper").font(.title2.bold()); Spacer(); Button("Done"){dismiss()}.keyboardShortcut(.cancelAction) }
            Text("Personalize this box position for this game on your Mac. This appearance is shared by saves of the same game; it doesn’t change their saved wallpapers.").font(.callout).foregroundStyle(.secondary)
            HStack {
                Button("Use Game Wallpaper") { override = "" }.disabled(override.isEmpty)
                Spacer()
                Text(override.isEmpty ? "Following the save" : "Mac appearance override").font(.caption).foregroundStyle(.secondary)
            }
            BoxWallpaperBackground(game:"",box:0,stored:override.isEmpty ? stored : override,usesOverride:false).frame(height:130).clipShape(RoundedRectangle(cornerRadius:12))
            Picker("Collection",selection:$collection) { ForEach(collections,id:\.0) { Text($0.1).tag($0.0) } }
            ScrollView {
                LazyVGrid(columns:[GridItem(.adaptive(minimum:125))],spacing:12) {
                    ForEach(wallpapers,id:\.self) { name in
                        Button { override = name } label: {
                            VStack(spacing:6) {
                                WallpaperImage(name:name).frame(height:82).clipShape(RoundedRectangle(cornerRadius:8))
                                Text("Wallpaper \(Int(name.dropFirst(6).prefix(2)) ?? 1)").font(.caption)
                            }.padding(7).background(override == name ? Color.accentColor.opacity(0.15) : .clear,in:RoundedRectangle(cornerRadius:10))
                                .overlay(RoundedRectangle(cornerRadius:10).stroke(override == name ? Color.accentColor : .clear,lineWidth:2))
                        }.buttonStyle(.plain).accessibilityLabel("Mac wallpaper \(name)")
                    }
                }.padding(3)
            }
            Text("To change the wallpaper stored in a supported game, use Box Layout → Wallpaper. Mac appearance changes take effect immediately.").font(.caption).foregroundStyle(.secondary)
        }.padding(24).frame(width:650,height:650)
    }
}

struct BoxWallpaperBackground: View {
    @AppStorage private var override: String
    let stored: String
    let usesOverride: Bool
    init(game:String,box:Int,stored:String,usesOverride:Bool=true) {
        _override = AppStorage(wrappedValue:"", "boxWallpaper.\(game).\(box)")
        self.stored = stored; self.usesOverride = usesOverride
    }
    var body: some View {
        GeometryReader { geometry in
            let name = usesOverride && !override.isEmpty ? override : stored
            if let image = NSImage(contentsOf:Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/Wallpapers/\(name).png")) {
                Image(nsImage:image).resizable().interpolation(.none).scaledToFill()
                    .frame(width:geometry.size.width,height:geometry.size.height).clipped()
            } else { LinearGradient(colors:[Color.accentColor.opacity(0.25),Color(nsColor:.windowBackgroundColor)],startPoint:.topLeading,endPoint:.bottomTrailing) }
        }.overlay(Color.black.opacity(0.07)).allowsHitTesting(false)
    }
}
