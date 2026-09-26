import Foundation

// Game artwork is exact-form only. The view falls back to matching HOME artwork,
// including shiny variants, rather than substituting regular-color game icons.
enum GamePortraitResourceResolver {
    static func resolve(sprite:String,identity:String?,game:String,manifest:PortraitManifest,homeDirectory:URL,in directory:URL)->URL? {
        let arceus=game == "PLA" || (game.isEmpty && identity?.hasPrefix("Gen8a:")==true)
        if arceus {
            var key=sprite.replacingOccurrences(of:"a_",with:"c_").replacingOccurrences(of:"b_",with:"c_")
            if let identity,!identity.isEmpty {
                let parts=identity.split(separator:":").map(String.init)
                guard parts.count==6,let species=Int(parts[1]),species>0,let form=Int(parts[2]),form>=0,let gender=Int(parts[3]),let shiny=Int(parts[5]),[0,1].contains(shiny) else{return nil}
                // Gender-specific portraits not present in this collection fall back to HOME.
                if gender==1,let url=PortraitResourceResolver.resolve(sprite:sprite,identity:identity,manifest:manifest,in:homeDirectory),url.deletingLastPathComponent().lastPathComponent=="female" {return nil}

                key="c_\(species)"+(form==0 ? "":"-\(form)")+(shiny==1 ? "s":"")
            }
            guard !key.contains("/"),!key.contains("..") else{return nil}
            let url=directory.appendingPathComponent("PLA/"+key+".png")
            return FileManager.default.fileExists(atPath:url.path) ? url:nil
        }
        return PortraitResourceResolver.resolve(sprite:sprite,identity:identity,manifest:manifest,in:directory.appendingPathComponent("SV"))
    }
}
