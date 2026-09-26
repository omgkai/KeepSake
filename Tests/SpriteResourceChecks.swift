import Foundation
@main struct SpriteResourceChecks {
    static func main() throws {
        let entries=try JSONDecoder().decode([[String:String]].self,from:Data(contentsOf:URL(fileURLWithPath:CommandLine.arguments[1])))
        let assets=URL(fileURLWithPath:CommandLine.arguments[2])
        for row in entries {
            guard let normal=SpriteResourceResolver.resolve(row["normal"]!,in:assets),let shiny=SpriteResourceResolver.resolve(row["shiny"]!,in:assets) else {fatalError("Missing image: \(row)")}
            precondition(normal != shiny,"Same image for regular and shiny: \(row)")
            precondition(shiny.deletingPathExtension().lastPathComponent.hasSuffix("s"),"Shiny resolved to normal art")
        }
        print("PASS actual Swift sprite resolver: \(entries.count) normal/shiny pairs; all shiny keys resolve to shiny assets")
    }
}
