import Foundation
@main struct JournalStoreChecks {
    @MainActor static func main() throws {
        let root=URL(fileURLWithPath:CommandLine.arguments[1]);try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        let file=root.appendingPathComponent(UUID().uuidString+".json"), key=String(repeating:"a",count:64), key2=String(repeating:"b",count:64)
        let store=JournalStore(url:file)
        var entry=PokemonJournal(displayName:"Sparky",notes:"Our first adventure 🌿",favorite:true,style:"Meadow",species:"Pikachu",nickname:"Pika",sprite:"b_25s",game:"Scarlet",level:42,portrait:"Gen9:25:0:1:0:1")
        try store.save(entry,for:key)
        let reopened=JournalStore(url:file);assert(reopened.entries[key]?.displayName=="Sparky" && reopened.entries[key]?.notes==entry.notes && reopened.entries[key]?.favorite==true && reopened.entries[key]?.style=="Meadow")
        assert(reopened.entries[key]?.portrait=="Gen9:25:0:1:0:1")
        let snapshot=try Data(contentsOf:file)
        entry.notes=String(repeating:"x",count:20001)
        do {try store.save(entry,for:key);fatalError("oversized note accepted")}catch{}
        assert(try! Data(contentsOf:file)==snapshot)
        var older=reopened.entries[key]!;older.notes="Old";older.updated=Date.distantPast
        var newer=older;newer.notes="New memory";newer.updated=Date.distantFuture
        try store.merge(JSONEncoder().encode(JournalDocument(entries:[key:older,key2:newer])))
        assert(store.entries[key]?.notes != "Old" && store.entries[key2]?.notes=="New memory")
        try store.merge(JSONEncoder().encode(JournalDocument(entries:[key:newer])))
        assert(store.entries[key]?.notes=="New memory" && store.entries.count==2)
        let exported=try store.exportData();assert(try! JournalStore.decode(exported)==store.entries)
        let snapshot2=try Data(contentsOf:file)
        for bad in [Data("bad".utf8),try JSONEncoder().encode(JournalDocument(version:99,entries:[:])),try JSONEncoder().encode(JournalDocument(entries:["invalid":newer]))] {
            do{try store.merge(bad);fatalError("invalid backup accepted")}catch{}
            assert(try! Data(contentsOf:file)==snapshot2)
        }
        // Version-1 migration is read-only until the first explicit save.
        let legacy=root.appendingPathComponent(UUID().uuidString+".json")
        let oldData=try JSONEncoder().encode(JournalDocument(version:1,entries:[key:newer]))
        try oldData.write(to:legacy);let upgraded=root.appendingPathComponent(UUID().uuidString+".json");let migrated=JournalStore(url:upgraded,legacyURL:legacy)
        assert(migrated.entries[key]?.notes=="New memory" && migrated.teams.isEmpty)
        assert(try! Data(contentsOf:legacy)==oldData)
        var team=JournalTeam(name:"Our first six",notes:"Route 1 together 🌿",style:"Aurora",emblem:"leaf.fill",members:[key,key2])
        try store.saveTeam(team)
        let teamBytes=try Data(contentsOf:file)
        for badMembers in [[key,key],[String(repeating:"c",count:64)],Array(repeating:key,count:7)] {
            team.members=badMembers
            do{try store.saveTeam(team);fatalError("invalid lineup accepted")}catch{}
            assert(try! Data(contentsOf:file)==teamBytes)
        }
        team.members=[key2,key];try store.saveTeam(team)
        var cover=JournalCover(title:"Kanto memories",subtitle:"Where it all began",style:"Sunset",emblem:"heart.fill")
        try store.saveCover(cover)
        let all=JournalStore(url:file)
        assert(all.teams[team.id]?.members==[key2,key] && all.teams[team.id]?.notes==team.notes && all.cover.title==cover.title)
        let teamBackup=try all.exportData();let imported=JournalStore(url:root.appendingPathComponent(UUID().uuidString+".json"));try imported.merge(teamBackup)
        assert(imported.teams==all.teams && imported.entries==all.entries && imported.cover==all.cover)
        let protectedBytes=try Data(contentsOf:file);cover.title=" "
        do{try store.saveCover(cover);fatalError("empty title accepted")}catch{}
        assert(try! Data(contentsOf:file)==protectedBytes)
        try migrated.merge(teamBackup);assert(migrated.teams[team.id] != nil && migrated.entries.count==2)
        assert(try! Data(contentsOf:legacy)==oldData);assert(FileManager.default.fileExists(atPath:upgraded.path))
        let limitStore=JournalStore(url:root.appendingPathComponent(UUID().uuidString+".json"))
        let sixKeys=(1...7).map{String(repeating:String($0),count:64)}
        for k in sixKeys{try limitStore.save(newer,for:k)}
        var fullTeam=JournalTeam(members:Array(sixKeys.prefix(6)));try limitStore.saveTeam(fullTeam)
        assert(limitStore.teams[fullTeam.id]?.members.count==6)
        fullTeam.members=sixKeys
        do{try limitStore.saveTeam(fullTeam);fatalError("seventh companion accepted")}catch{}
        assert(limitStore.teams[fullTeam.id]?.members.count==6)
        var custom=PokemonJournal(species:"Eevee",sprite:"b_133",pokemonData:Data([1,2,3]).base64EncodedString(),pokemonExtension:"pk9",accentHex:"184A62",companionHex:"8B4279")
        try limitStore.save(custom,for:sixKeys[0]);let customBackup=try limitStore.exportData();let customReload=try JournalStore.decode(customBackup);assert(customReload[sixKeys[0]]?.pokemonData==custom.pokemonData && customReload[sixKeys[0]]?.accentHex=="184A62")
        custom.accentHex="not a color";do{try limitStore.save(custom,for:sixKeys[0]);fatalError("invalid color accepted")}catch{}
        custom.accentHex="FFFFFF";custom.pokemonData="not base64";do{try limitStore.save(custom,for:sixKeys[0]);fatalError("invalid snapshot accepted")}catch{}
        let v2=try JSONEncoder().encode(JournalDocument(version:2,entries:all.entries,teams:all.teams,cover:all.cover));let v2URL=root.appendingPathComponent(UUID().uuidString+".json");try v2.write(to:v2URL)
        let v3URL=root.appendingPathComponent(UUID().uuidString+".json");let v3=JournalStore(url:v3URL,legacyURL:v2URL);assert(v3.entries==all.entries && v3.teams==all.teams);try v3.saveCover(all.cover);assert(try! Data(contentsOf:v2URL)==v2);assert(try! JournalStore.decodeDocument(v3.exportData()).version==3)
        print("PASS v2 → v3 migration, snapshot/custom-color backup preservation and invalid-data rejection")
        print("PASS journal version-1 migration, team membership/order, six-member bounds, cover customization and complete backup roundtrip")
        // Party import is a single transaction; clones occupy separate pages without
        // overwriting existing memories or the Pokémon snapshots in older teams.
        let partyStore=JournalStore(url:root.appendingPathComponent(UUID().uuidString+".json"))
        let remembered=PokemonJournal(displayName:"Our spark",notes:"Keep this memory",favorite:true,style:"Aurora",species:"Pikachu",sprite:"b_25",game:"Violet",level:20,pokemonData:Data([1,2,3]).base64EncodedString(),pokemonExtension:"pk9")
        try partyStore.save(remembered,for:key)
        let original=partyStore.entries[key]!
        let caught=PokemonJournal(species:"Pikachu",nickname:"Pika",sprite:"b_25s",game:"Violet",level:50,pokemonData:Data([4,5,6]).base64EncodedString(),pokemonExtension:"pk9")
        let clones=[JournalPartyMember(sourceKey:key,entry:caught),JournalPartyMember(sourceKey:key,entry:caught)]
        let savedParty=try partyStore.saveParty(clones,to:JournalTeam(name:"Our Violet journey"))
        assert(savedParty.members.count==2 && Set(savedParty.members).count==2)
        assert(partyStore.entries[key]==original)
        for member in savedParty.members {let page=partyStore.entries[member]!;assert(page.notes==original.notes && page.displayName==original.displayName && page.favorite && page.style=="Aurora" && page.pokemonData==caught.pokemonData && page.level==50)}
        let beforeParty=try partyStore.exportData()
        var missing=caught;missing.pokemonData=nil
        for invalid in [[],[clones[0],JournalPartyMember(sourceKey:key,entry:missing)],Array(repeating:clones[0],count:7)] {
            do{try partyStore.saveParty(invalid,to:JournalTeam());fatalError("Invalid party accepted")}catch{}
            assert(try! JournalStore.decodeDocument(partyStore.exportData()).entries==JournalStore.decodeDocument(beforeParty).entries)
            assert(partyStore.teams.count==1)
        }
        let appended=try partyStore.saveParty(clones,to:savedParty)
        assert(appended.members.count==4 && Array(appended.members.prefix(2))==savedParty.members)
        do{try partyStore.saveParty(clones,to:savedParty);fatalError("Stale destination accepted")}catch{}
        let full=try partyStore.saveParty(clones,to:appended);assert(full.members.count==6)
        do{try partyStore.saveParty([clones[0]],to:full);fatalError("Full team accepted")}catch{}
        let roundtripParty=try JournalStore.decodeDocument(partyStore.exportData())
        assert(roundtripParty.teams?[full.id]==full && roundtripParty.entries[key]==original)
        print("PASS party capture atomicity, clone identities, preserved memories/snapshots, append order, stale destination, six-member limit and backup roundtrip")
        let corrupt=root.appendingPathComponent(UUID().uuidString+".json");try Data("broken".utf8).write(to:corrupt)
        let protected=JournalStore(url:corrupt);assert(protected.error != nil)
        do{try protected.save(newer,for:key);fatalError("corrupt file overwritten")}catch{}
        assert(try! String(contentsOf:corrupt,encoding:.utf8)=="broken")
        print("PASS journal persistence, all four options, Unicode, validation, backup merge, relaunch, atomic rejection and corrupt-file preservation")
    }
}
