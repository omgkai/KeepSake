using PKHeX.Core;
using System.Text.Json;
using System.Security.Cryptography;

sealed partial class EditorSession {
    (int team,int slot,string field) FameNameTarget(JsonElement r) {
        var parts=S(r,"id").Split(':');if(parts.Length!=2 || !int.TryParse(parts[0],out int team) || !int.TryParse(parts[1],out int slot) || slot<0 || slot>=6)throw new Exception("Choose a Hall of Fame Pokémon.");
        int maximum=save is SAV1 or SAV3 {Data.Length:>=0x1E000} ? 50:save is ISaveBlock6Main ? HallOfFame6.Entries:0;
        if(team<0 || team>=maximum)throw new Exception("This Hall of Fame record does not support stored name bytes.");
        var field=S(r,"field","Nickname");if(field!="Nickname" && !(save is ISaveBlock6Main && field=="OriginalTrainerName"))throw new Exception("Choose a stored Hall of Fame name.");
        return (team,slot,field);
    }
    byte[] FameNameBuffer(int team,int slot,string field,byte[]? replacement=null) {
        if(save is SAV1 one){var pk=one.HallOfFame.GetEntity(team,slot);if(replacement!=null)replacement.CopyTo(pk.NicknameTrash);return pk.NicknameTrash.ToArray();}
        if(save is SAV3 three){var entries=HallFame3Entry.GetEntries(three);var pk=entries[team].GetMember(slot);if(replacement!=null){replacement.CopyTo(pk.NicknameTrash);HallFame3Entry.SetEntries(three,entries);}return pk.NicknameTrash.ToArray();}
        var buffer=((ISaveBlock6Main)RequireSave()).HallOfFame.GetEntity(team,slot).Slice(field=="Nickname"?0x18:0x30,24);if(replacement!=null)replacement.CopyTo(buffer);return buffer.ToArray();
    }
    static string FameNameKey(int team,int slot,string field,byte[] bytes)=>$"fame:{team}:{slot}:{field}:"+Convert.ToHexString(SHA256.HashData(bytes));
    object ReadFameNameBytes(JsonElement r) {
        var (team,slot,field)=FameNameTarget(r);var bytes=FameNameBuffer(team,slot,field);var sav=RequireSave();var pk=sav.BlankPKM;pk.Language=sav.Language;
        Choice[] fields=save is ISaveBlock6Main ? [new("Nickname","Nickname"),new("OriginalTrainerName","Original trainer")]:[new("Nickname","Nickname")];
        int limit=sav.Generation==6 ? 12:(sav is SAV1 {Japanese:true} or SAV3 {Japanese:true}) ? 5:10;
        return PreviewNameBytes(r,pk,field,bytes,limit,FameNameKey(team,slot,field,bytes),fields);
    }
    void SetFameNameBytes(JsonElement r) {
        var (team,slot,field)=FameNameTarget(r);var current=FameNameBuffer(team,slot,field);
        if(N(r,"revision")!=revision || S(r,"entityKey")!=FameNameKey(team,slot,field,current))throw new Exception("The Hall of Fame changed. Reopen its name editor.");
        FameNameBuffer(team,slot,field,ParseNameHex(S(r,"hex"),current.Length));dirty=true;
    }
}
