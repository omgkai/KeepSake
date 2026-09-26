using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    static Span<byte> TrainerNameBuffer(SaveFile sav,string field) => field == "Rival" && sav is SAV8BS bs ? bs.RivalNameTrash : field == "Rival" && sav is SAV3FRLG fr ? fr.LargeBlock.RivalNameTrash : field == "Rival" && sav is SAV7b lg ? lg.Misc.RivalNameTrash : field == "TrainerCard" && sav is SAV8SWSH sw ? sw.Blocks.TrainerCard.OriginalTrainerTrash : field != "OT" ? throw new Exception("Choose a trainer name field.") : sav switch
    {
        SAV1 s=>s.OriginalTrainerTrash,SAV2 s=>s.OriginalTrainerTrash,SAV3 s=>s.SmallBlock.OriginalTrainerTrash,
        SAV3Colosseum s=>s.OriginalTrainerTrash,SAV3XD s=>s.OriginalTrainerTrash,SAV4 s=>s.OriginalTrainerTrash,
        SAV5 s=>s.PlayerData.OriginalTrainerTrash,SAV6 s=>s.Status.OriginalTrainerTrash,SAV7 s=>s.MyStatus.OriginalTrainerTrash,
        SAV7b s=>s.Status.OriginalTrainerTrash,SAV8SWSH s=>s.MyStatus.OriginalTrainerTrash,SAV8LA s=>s.MyStatus.OriginalTrainerTrash,
        SAV8BS s=>s.MyStatus.OriginalTrainerTrash,SAV9SV s=>s.MyStatus.OriginalTrainerTrash,SAV9ZA s=>s.MyStatus.OriginalTrainerTrash,
        _=>throw new Exception("This game does not expose an original-trainer text buffer.")
    };
    bool TrainerNameSupported() { try { return TrainerNameBuffer(RequireSave(),"OT").Length>0; } catch { return false; } }
    string TrainerNameKey(SaveFile sav,string field)=>sav.GetType().Name+":"+field+":"+Convert.ToHexString(SHA256.HashData(TrainerNameBuffer(sav,field)));
    object TrainerNameInfo(JsonElement r)
    {
        var sav=RequireSave();var field=S(r,"field","OT");var bytes=TrainerNameBuffer(sav,field).ToArray();var key=TrainerNameKey(sav,field);
        if(r.TryGetProperty("hex",out _))
        {
            if(N(r,"revision")!=revision || S(r,"entityKey")!=key)throw new Exception("The trainer changed. Reopen the name editor.");
            bytes=ParseNameHex(S(r,"hex"),bytes.Length);
            switch(S(r,"mode"))
            {
                case "text":var text=S(r,"text");if(text.Length>sav.MaxStringLengthTrainer)throw new Exception("This trainer name is too long for the game.");sav.SetString(bytes,text,text.Length,StringConverterOption.None);break;
                case "clear":
                case "layer":
                    var current=sav.GetString(bytes);var encoded=new byte[bytes.Length];int used=sav.SetString(encoded,current,current.Length,StringConverterOption.None);
                    if(S(r,"mode")=="clear")bytes.AsSpan(used).Clear();
                    else
                    {
                        int species=N(r,"species"),language=N(r,"language"),generation=N(r,"generation");
                        if(species<1||species>=strings.specieslist.Length||language<1||language>10||language==6||generation<1||generation>9)throw new Exception("Choose a supported species, language and generation.");
                        string layer=SpeciesName.GetSpeciesNameGeneration((ushort)species,language,(byte)generation);
                        var temp=new byte[256];int length=sav.SetString(temp,layer,layer.Length,StringConverterOption.None);
                        if(length<=used || length>bytes.Length)throw new Exception("This layer does not fit after the current trainer name.");
                        temp.AsSpan(used,length-used).CopyTo(bytes.AsSpan(used));
                    }
                    break;
                case "hex":break;
                default:throw new Exception("Unknown trainer text operation.");
            }
        }
        Choice[] fields=sav is SAV8SWSH ? [new("OT","Trainer name"),new("TrainerCard","League card name")] : sav is SAV7b or SAV8BS or SAV3FRLG ? [new("OT","Trainer name"),new("Rival","Rival name")] : [new("OT","Trainer name")];
        return new NameBytesPage(revision,key,fields,field,sav.GetString(bytes),Convert.ToHexString(bytes),bytes.Length,sav.MaxStringLengthTrainer,NameCharacters(sav.Context));
    }
    void SetTrainerName(JsonElement r)
    {
        var sav=RequireSave();var field=S(r,"field","OT");
        if(N(r,"revision")!=revision||S(r,"entityKey")!=TrainerNameKey(sav,field))throw new Exception("The trainer changed. Reopen the name editor.");
        var target=TrainerNameBuffer(sav,field);ParseNameHex(S(r,"hex"),target.Length).CopyTo(target);
        if(sav is SAV3Colosseum colo)colo.OT2=colo.OT;
        dirty=true;ParseSettings.InitFromSaveFileData(sav);
    }
}
