import Foundation
import Combine

let journalStyles = ["Midnight","Meadow","Sunset","Aurora"]
let journalEmblems = ["book.closed.fill","sparkles","leaf.fill","moon.stars.fill","heart.fill","flag.fill"]
struct PokemonJournal:Codable,Equatable {
    var displayName="",notes="",favorite=false,style="Midnight"
    var species="",nickname="",sprite="b_0",game="",level=0
    var pokemonData:String?=nil,pokemonExtension:String?=nil
    var accentHex:String?=nil,companionHex:String?=nil
    var portrait:String?=nil
    var created=Date(),updated=Date()
    var title:String {displayName.isEmpty ? (nickname.isEmpty ? species : nickname) : displayName}
}
struct JournalPartyMember { let sourceKey:String; let entry:PokemonJournal }
struct JournalTeam:Codable,Equatable,Identifiable {
    var id=UUID().uuidString,name="My adventure team",notes="",style="Midnight",emblem="flag.fill"
    var accentHex:String?=nil,companionHex:String?=nil
    var members:[String]=[]
    var updated=Date()
}
struct JournalCover:Codable,Equatable {
    var title="My Pokémon journal",subtitle="",style="Midnight",emblem="book.closed.fill"
    var accentHex:String?=nil,companionHex:String?=nil
    var updated=Date.distantPast
}
// Optional additions keep version-1 backups readable without rewriting the original on load.
struct JournalDocument:Codable {var version=3;var entries:[String:PokemonJournal];var teams:[String:JournalTeam]?=nil;var cover:JournalCover?=nil}
@MainActor final class JournalStore:ObservableObject {
    @Published private(set) var entries:[String:PokemonJournal]=[:]
    @Published private(set) var teams:[String:JournalTeam]=[:]
    @Published private(set) var cover=JournalCover()
    @Published var error:String?
    private let url:URL
    private var loadFailed=false
    init(url:URL?=nil,legacyURL:URL?=nil) {
        let directory=FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("KeepSake")
        let override=ProcessInfo.processInfo.environment["KEEPSAKE_JOURNAL_PATH"].map { URL(fileURLWithPath:$0) }
        self.url=url ?? override ?? directory.appendingPathComponent("journals-v3.json")
        let legacy=[directory.appendingPathComponent("journals-v2.json"),directory.appendingPathComponent("journals-v1.json")].first{FileManager.default.fileExists(atPath:$0.path)}
        let fallback=legacyURL ?? (url==nil && override==nil ? legacy:nil)
        let source=FileManager.default.fileExists(atPath:self.url.path) ? self.url:(fallback ?? self.url)
        guard FileManager.default.fileExists(atPath:source.path) else{return}
        do {let d=try Self.decodeDocument(Data(contentsOf:source));entries=d.entries;teams=d.teams ?? [:];cover=d.cover ?? JournalCover()}catch{loadFailed=true;self.error="Your journal could not be read. It has been preserved: "+error.localizedDescription}
    }
    static func decode(_ data:Data)throws->[String:PokemonJournal] {try decodeDocument(data).entries}
    static func decodeDocument(_ data:Data)throws->JournalDocument {
        guard data.count<=16*1024*1024 else{throw JournalError("The journal backup is too large.")}
        let document=try JSONDecoder().decode(JournalDocument.self,from:data)
        guard [1,2,3].contains(document.version),document.entries.count<=20000,(document.teams?.count ?? 0)<=2000 else{throw JournalError("Unsupported journal backup.")}
        func validColors(_ a:String?,_ b:String?)->Bool { [a,b].allSatisfy{value in value==nil || (value!.count==6 && value!.allSatisfy{$0.isHexDigit})} }
        for (key,entry) in document.entries {
            guard (entry.portrait?.count ?? 0)<=100,validColors(entry.accentHex,entry.companionHex),(entry.pokemonData?.count ?? 0)<=4096,(entry.pokemonData==nil || Data(base64Encoded:entry.pokemonData!) != nil),(entry.pokemonExtension==nil || entry.pokemonExtension!.range(of:"^[a-z]{2}[1-9]$",options:.regularExpression) != nil) else{throw JournalError("Invalid companion colors or Pokémon snapshot.")}
            guard key.count==64,key.allSatisfy({$0.isHexDigit}),entry.displayName.count<=80,entry.notes.count<=20000,entry.species.count<=100,entry.nickname.count<=100,entry.game.count<=100,(0...100).contains(entry.level),journalStyles.contains(entry.style),entry.sprite.range(of:"^[ab]_[0-9]+(?:-[0-9]+[cp]?)*f?s?$",options:.regularExpression) != nil else {throw JournalError("The backup contains an invalid journal entry.")}
        }
        for (key,team) in document.teams ?? [:] {
            guard validColors(team.accentHex,team.companionHex),key==team.id,UUID(uuidString:key) != nil,!team.name.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty,team.name.count<=80,team.notes.count<=20000,journalStyles.contains(team.style),journalEmblems.contains(team.emblem),team.members.count<=6,Set(team.members).count==team.members.count,team.members.allSatisfy({document.entries[$0] != nil}) else {throw JournalError("Use a team name up to 80 characters, a story up to 20,000 characters, and at most six different journal companions.")}
        }
        if let c=document.cover {guard validColors(c.accentHex,c.companionHex),!c.title.trimmingCharacters(in:.whitespacesAndNewlines).isEmpty,c.title.count<=80,c.subtitle.count<=200,journalStyles.contains(c.style),journalEmblems.contains(c.emblem) else{throw JournalError("Choose a journal title, cover style and emblem.")}}
        return document
    }
    func save(_ entry:PokemonJournal,for key:String)throws {var next=entries;var value=entry;value.updated=Date();next[key]=value;try persist(next,teams,cover)}
    func saveTeam(_ team:JournalTeam)throws {var next=teams;var value=team;value.updated=Date();next[team.id]=value;try persist(entries,next,cover)}
    /// Persist the complete lineup and its independent snapshots in a single atomic write.
    /// Duplicate Pokémon identities still get separate pages; older teams are never refreshed implicitly.
    @discardableResult func saveParty(_ party:[JournalPartyMember],to team:JournalTeam)throws -> JournalTeam {
        guard !party.isEmpty,party.count<=6,team.members.count+party.count<=6 else {
            throw JournalError("Choose one to six party Pokémon and a team with enough free places.")
        }
        if let existing=teams[team.id],existing != team { throw JournalError("This team changed. Reopen it before adding the party.") }
        var next=entries;var nextTeams=teams;var saved=team
        for member in party {
            guard member.sourceKey.count==64,member.sourceKey.allSatisfy({$0.isHexDigit}),
                  let snapshot=member.entry.pokemonData,!snapshot.isEmpty,
                  member.entry.pokemonExtension != nil else { throw JournalError("A party Pokémon is missing its file snapshot. Reopen the save and try again.") }
            var value=entries[member.sourceKey] ?? member.entry
            value.species=member.entry.species;value.nickname=member.entry.nickname
            value.sprite=member.entry.sprite;value.portrait=member.entry.portrait;value.game=member.entry.game;value.level=member.entry.level
            value.pokemonData=snapshot;value.pokemonExtension=member.entry.pokemonExtension
            value.created=Date();value.updated=value.created
            let key=(UUID().uuidString+UUID().uuidString).replacingOccurrences(of:"-",with:"").lowercased()
            next[key]=value;saved.members.append(key)
        }
        saved.updated=Date();nextTeams[saved.id]=saved
        try persist(next,nextTeams,cover)
        return saved
    }
    func saveCover(_ value:JournalCover)throws {var next=value;next.updated=Date();try persist(entries,teams,next)}
    func merge(_ data:Data)throws {
        let incoming=try Self.decodeDocument(data);var next=entries;var nextTeams=teams
        for (key,entry) in incoming.entries where next[key]==nil || next[key]!.updated<entry.updated {next[key]=entry}
        for (key,team) in incoming.teams ?? [:] where nextTeams[key]==nil || nextTeams[key]!.updated<team.updated {nextTeams[key]=team}
        let nextCover=incoming.cover.map{$0.updated>cover.updated ? $0:cover} ?? cover
        try persist(next,nextTeams,nextCover)
    }
    func exportData()throws->Data {try JSONEncoder().encode(JournalDocument(entries:entries,teams:teams,cover:cover))}
    private func persist(_ next:[String:PokemonJournal],_ nextTeams:[String:JournalTeam],_ nextCover:JournalCover)throws {
        guard !loadFailed else{throw JournalError("The existing journal could not be read. Restore that file before saving changes.")}
        let encoder=JSONEncoder();encoder.outputFormatting=[.prettyPrinted,.sortedKeys];let data=try encoder.encode(JournalDocument(entries:next,teams:nextTeams,cover:nextCover));_ = try Self.decodeDocument(data)
        try FileManager.default.createDirectory(at:url.deletingLastPathComponent(),withIntermediateDirectories:true)
        try data.write(to:url,options:.atomic);entries=next;teams=nextTeams;cover=nextCover
    }
}
struct JournalError:LocalizedError {let message:String;init(_ message:String){self.message=message};var errorDescription:String?{message}}
