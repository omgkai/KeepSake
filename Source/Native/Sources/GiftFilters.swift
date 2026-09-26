import Foundation

struct GiftEntry:Codable,Identifiable {
    let id:Int,card:Int,title:String,species:Int,name:String,generation:Int,game:String,level:Int,entity:Bool,`extension`:String,exportable:Bool
    let languages:[Int],languageKnown:Bool,origin:String,originVersions:[String],shinyRule:String,egg:Bool,heldItem:String,moves:[Int],trainer:String,source:String,file:String,sprite:String,portrait:String
    static let languageNames:[Int:String]=[1:"Japanese",2:"English",3:"French",4:"Italian",5:"German",7:"Spanish",8:"Korean",9:"Chinese (Simplified)",10:"Chinese (Traditional)",11:"Spanish (Latin America)"]
    var languageText:String {languages.isEmpty ? (entity ? "Language unrecorded":"Not applicable"):languages.count>1 ? "Multiple languages":Self.languageNames[languages[0]] ?? "Language \(languages[0])"}
    var languageDetails:String {languages.map{Self.languageNames[$0] ?? "Language \($0)"}.joined(separator:", ")}
}
struct GiftFilters:Equatable {
    var search="",generation=0,comparison="exact",language=0,origin="",kind="",shiny="",egg="",source="",species="",heldItem="",moves=["","","",""]
    var active:Bool {self != GiftFilters()}
    func matches(_ g:GiftEntry)->Bool {
        if generation>0 {
            if comparison=="before" && g.generation>generation{return false}
            if comparison=="after" && g.generation<generation{return false}
            if comparison=="exact" && g.generation != generation{return false}
        }
        if language>0 && !g.languages.contains(language){return false}
        if language == -1 && (!g.entity || g.languageKnown){return false}
        if language == -2 && g.languages.count<2{return false}
        if !origin.isEmpty && g.origin != origin{return false}
        if kind=="pokemon" && !g.entity{return false}
        if kind=="items" && g.entity{return false}
        if !shiny.isEmpty && g.shinyRule != shiny{return false}
        if egg=="egg" && (!g.entity || !g.egg){return false}
        if egg=="notEgg" && (!g.entity || g.egg){return false}
        if !source.isEmpty && g.source != source{return false}
        if let id=Int(species),g.species != id{return false}
        if !heldItem.isEmpty && g.heldItem != heldItem{return false}
        if !moves.compactMap(Int.init).allSatisfy(g.moves.contains){return false}
        let words=[g.name,g.title,String(g.card),g.game,g.origin,g.trainer,g.file,g.languageDetails].joined(separator:" ")
        return search.isEmpty || words.localizedCaseInsensitiveContains(search)
    }
}
