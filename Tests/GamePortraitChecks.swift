import Foundation
@main struct GamePortraitChecks {
    static func main() throws {
        let assets=URL(fileURLWithPath:CommandLine.arguments[1]),home=assets.appendingPathComponent("Portraits"),games=assets.appendingPathComponent("GamePortraits"),sprites=assets.appendingPathComponent("Sprites")
        let manifest=try JSONDecoder().decode(PortraitManifest.self,from:Data(contentsOf:home.appendingPathComponent("manifest.json")))
        var count=0
        func expect(_ value:Bool,_ label:String) {count+=1;precondition(value,label)}
        func game(_ sprite:String,_ identity:String?=nil,_ version:String="SL")->URL? {GamePortraitResourceResolver.resolve(sprite:sprite,identity:identity,game:version,manifest:manifest,homeDirectory:home,in:games)}
        for species in [25,133,1,155,258,252,393,501,653,722,813,152,909,912,906] {
            expect(SpriteResourceResolver.resolve("a_\(species)",in:sprites)?.lastPathComponent=="b_\(species).png","Pixel preference applies to artwork aliases")
            expect(SpriteResourceResolver.resolve("a_\(species)s",in:sprites)?.lastPathComponent=="b_\(species)s.png","Shiny alias uses pixel sprite")
        }
        expect(game("b_25")?.path.hasSuffix("SV/25.png")==true,"Scarlet/Violet portrait")
        expect(game("a_25") == game("b_25"),"Preview aliases use same game portrait")
        expect(game("b_25s")==nil,"Missing shiny game portrait must fall back to HOME, never normal game art")
        expect(PortraitResourceResolver.resolve(sprite:"b_25s",identity:nil,manifest:manifest,in:home)?.path.contains("/shiny/")==true,"Shiny fallback exists")
        expect(game("b_25","Gen9:25:250:0:0:0")==nil,"Unknown form not replaced")
        expect(game("b_25","../25")==nil,"Malformed identity rejected")
        expect(game("b_25","Gen8a:25:0:0:0:0","PLA")?.lastPathComponent=="c_25.png","PLA portrait")
        expect(game("b_25s","Gen8a:25:0:0:0:1","PLA")?.lastPathComponent=="c_25s.png","PLA shiny portrait")
        expect(game("b_25","Gen8a:25:0:1:0:0","PLA")==nil,"Female variant falls back to matching HOME")
        expect(game("b_25","Gen8a:25:0:0:0:0","")?.lastPathComponent=="c_25.png","Standalone Arceus Pokémon")
        expect(game("b_25","Gen8a:25:0:0:0:0","SL")?.path.hasSuffix("SV/25.png")==true,"Loaded save controls game artwork")
        expect(game("b_59-1","Gen8a:59:1:0:0:0","PLA")?.lastPathComponent=="c_59-1.png","Hisuian form")
        expect(game("b_25","Gen8a:25:250:0:0:0","PLA")==nil,"Unknown PLA form not replaced")
        expect(game("../25",nil,"PLA")==nil,"No traversal")
        for (sprite,_) in manifest.sprites {
            if let url=game(sprite) {expect(FileManager.default.fileExists(atPath:url.path),"Resolved game resource exists")}
            expect(game(sprite+"s")==nil,"No normal artwork for shiny game icons")
        }
        print("PASS \(count) game portrait, exact-form, shiny fallback and global pixel alias checks")
    }
}
