import Foundation

// Shared by the native renderer and the resource coverage check.
enum SpriteResourceResolver {
    static func candidates(for name:String)->[String] {
        let name=name.replacingOccurrences(of:"a_",with:"b_")
        let shiny=name.hasSuffix("s")
        let normal=shiny ? String(name.dropLast()) : name
        let base=String(normal.split(separator:"-").first ?? "b_0")
        let suffix=shiny ? "s" : ""
        // Lord forms share the regional form's shiny appearance.
        let lord:[String:String]=["b_59-2":"b_59-1","b_101-2":"b_101-1","b_549-2":"b_549-1","b_713-2":"b_713-1","b_900-1":"b_900"]
        var candidates=[name,name.replacingOccurrences(of:"b_",with:"a_")]
        if let regional=lord[normal] { candidates += [regional+suffix,regional.replacingOccurrences(of:"b_",with:"a_")+suffix] }
        candidates += [base+suffix,base.replacingOccurrences(of:"b_",with:"a_")+suffix]
        // A missing variant can use shiny base artwork, never a regular-color sprite.
        return candidates
    }
    static func resolve(_ name:String,in directory:URL)->URL? {
        candidates(for:name).map{directory.appendingPathComponent($0+".png")}.first{FileManager.default.fileExists(atPath:$0.path)}
    }
}
