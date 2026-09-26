using PKHeX.Core;
using System.Text.Json;
using System.Security.Cryptography;
sealed partial class EditorSession {
    (SecretBaseManager3 manager,SecretBase3 entry,int index) BaseNameTarget(JsonElement r) {
        if(save is not SAV3 {LargeBlock:ISaveBlock3LargeHoenn})throw new Exception("This save has no Hoenn Secret Bases.");
        var manager=Bases3;var id=S(r,"id");if(!id.StartsWith("base:")||!int.TryParse(id[5..],out int i)||i<0||i>=manager.Count)throw new Exception("Choose a registered Secret Base.");return (manager,manager.Bases[i],i);
    }
    static string BaseNameKey(SecretBase3 b,int i)=>$"base:{i}:{b.Language}:"+Convert.ToHexString(SHA256.HashData(b.OriginalTrainerTrash));
    object ReadBaseName(JsonElement r) {
        var(_,b,i)=BaseNameTarget(r);var pk=new PK3{Language=b.Language};return PreviewNameBytes(r,pk,"OriginalTrainerName",b.OriginalTrainerTrash.ToArray(),pk.MaxStringLengthTrainer,BaseNameKey(b,i),[new("OriginalTrainerName","Base trainer")]);
    }
    void SetBaseName(JsonElement r) {
        var(manager,b,i)=BaseNameTarget(r);if(N(r,"revision")!=revision||S(r,"entityKey")!=BaseNameKey(b,i))throw new Exception("The Secret Base changed. Reopen its name editor.");ParseNameHex(S(r,"hex"),b.OriginalTrainerTrash.Length).CopyTo(b.OriginalTrainerTrash);manager.Save();dirty=true;
    }
}
