import Foundation

struct PortraitManifest:Decodable {
    let identities:[String:String]
    let sprites:[String:String]
}
enum PortraitResourceResolver {
    static func resolve(sprite:String,identity:String?,manifest:PortraitManifest,in directory:URL)->URL? {
        var file:String?,female=false,shiny=sprite.hasSuffix("s")
        if let identity,!identity.isEmpty {
            let parts=identity.split(separator:":").map(String.init)
            guard parts.count==6,let gender=Int(parts[3]),let isShiny=Int(parts[5]) else{return nil}
            let context=["Gen1","Gen2"].contains(parts[0]) ? "Gen3":parts[0]
            file=manifest.identities[[context,parts[1],parts[2],parts[4]].joined(separator:":")]
            female=gender==1;shiny=isShiny==1
            // Never replace an unrecognized form with another form's portrait.
            guard file != nil else{return nil}
        } else {
            let key=(shiny ? String(sprite.dropLast()):sprite).replacingOccurrences(of:"a_",with:"b_")
            file=manifest.sprites[key]
        }
        guard let file,!file.contains(".."),!file.hasPrefix("/") else{return nil}
        let prefix=shiny ? "shiny/":""
        if female {
            let candidate=directory.appendingPathComponent(prefix+"female/"+file)
            if FileManager.default.fileExists(atPath:candidate.path){return candidate}
        }
        let candidate=directory.appendingPathComponent(prefix+file)
        return FileManager.default.fileExists(atPath:candidate.path) ? candidate:nil
    }
}
