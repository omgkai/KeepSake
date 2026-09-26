using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    static List<StringInstruction> SearchInstructions(string text) {
        if(text.Length>16000)throw new Exception("Keep advanced filters under 16,000 characters.");
        var lines=text.Split('\n').Select(x=>x.Trim()).Where(x=>x.Length>0).ToArray();
        if(lines.Length>100 || lines.Any(x=>!StringInstruction.IsFilterInstruction(x[0]) || x.IndexOf('=',1)<2))throw new Exception("Use one read-only filter per line, such as =Species=25 or >Level=49. Editing instructions are not allowed here.");
        var filters=StringInstruction.GetFilters(lines);if(filters.Count!=lines.Length)throw new Exception("One or more filter lines could not be read.");
        EntityBatchEditor.ScreenStrings(filters);return filters;
    }
    object FilterGifts(JsonElement r) {
        var filters=SearchInstructions(S(r,"filters"));
        return giftDatabase.Value.Select((g,i)=>(gift:g,id:i)).Concat(localGifts.Select(x=>(gift:(MysteryGift)x.Value.Gift,id:x.Key)))
            .Where(x=>BatchEditingUtil.IsFilterMatch(filters,x.gift)).Select(x=>x.id).ToArray();
    }
    object ExportGiftSelection(JsonElement r) {
        var ids=r.GetProperty("ids").EnumerateArray().Select(x=>x.GetInt32()).Distinct().ToArray();
        if(ids.Length is <1 or >10000)throw new Exception("Choose between 1 and 10,000 gift cards.");
        var cards=ids.Select(id=>(id,gift:Gift(id) as DataMysteryGift ?? throw new Exception("One selected event has no card file."))).ToArray();
        var parent=Path.GetFullPath(S(r,"path"));if(!Directory.Exists(parent))throw new Exception("Choose an existing folder.");
        string destination=Path.Combine(parent,"KeepSake Gifts "+Guid.NewGuid().ToString("N")[..10]),temporary=destination+".tmp";
        try {Directory.CreateDirectory(temporary);foreach(var (id,gift) in cards)File.WriteAllBytes(Path.Combine(temporary,$"{id:D5}-Card-{gift.CardID}.{gift.Extension}"),gift.Write().ToArray());Directory.Move(temporary,destination);}
        finally {if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
        return new {count=cards.Length,path=destination};
    }
    object StorageSearch(JsonElement r) {
        var filters=SearchInstructions(S(r,"filters"));
        var ids=StoredPokemon().Where(x=>BatchEditingUtil.IsFilterMatch(filters,x.pk)).Select(x=>$"{x.party}:{x.b}:{x.s}").ToHashSet();
        return StorageRows().Where(x=>ids.Contains(x.id)).ToArray();
    }
}
