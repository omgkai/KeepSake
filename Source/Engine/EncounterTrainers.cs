using PKHeX.Core;
using System.Text.Json;

sealed partial class EditorSession
{
    object EncounterTrainerInfo(JsonElement r)
    {
        ITrainerInfo trainer=RequireSave();
        if(S(r,"path") is {Length:>0} path) {
            var info=new FileInfo(path);
            if(info.Length is <1 or >33554432)throw new Exception("Choose a save or Pokémon file up to 32 MB.");
            byte[] data=File.ReadAllBytes(path);
            if(SaveUtil.GetSaveFile(data,path) is {} sav)trainer=sav;
            else if(EntityFormat.GetFromBytes(data,EntityFileExtension.GetContextFromExtension(info.Extension)) is {Species:>0} pk) {
                var db=new TrainerDatabase();db.RegisterCopy(pk);
                trainer=db.GetTrainer(pk.Version) ?? throw new Exception("The file has no usable trainer origin.");
            } else throw new Exception("The file is not a supported save or Pokémon.");
        }
        var profile=new SimpleTrainerInfo(trainer);
        if(!profile.Version.IsValidSavedVersion())throw new Exception("This save has an ambiguous game. Import a Pokémon with a specific origin game instead.");
        return new {name=profile.OT,version=profile.Version.ToString(),game=GameInfo.GetVersionName(profile.Version),tid=(int)profile.TID16,sid=(int)profile.SID16,
            gender=(int)profile.Gender,language=profile.Language,consoleRegion=(int)profile.ConsoleRegion,country=(int)profile.Country,region=(int)profile.Region};
    }
    static ITrainerInfo ReadEncounterTrainer(JsonElement r,IEncounterable encounter)
    {
        if(!Enum.TryParse<GameVersion>(S(r,"version"),out var version) || !version.IsValidSavedVersion() || !(encounter.Version==version || encounter.Version.Contains(version)))
            throw new Exception("Choose a remembered trainer whose game matches this encounter's origin.");
        int Number(string key,int max) {int value=N(r,key,-1);if(value<0 || value>max)throw new Exception($"Invalid trainer {key}.");return value;}
        var name=S(r,"name");int language=Number("language",10);
        if(language is 0 or 6)throw new Exception("Choose a supported trainer language.");
        if(name.Length is <1 or >12 || name.Any(char.IsControl))throw new Exception("The remembered trainer name is invalid.");
        return new SimpleTrainerInfo(version){OT=name,TID16=(ushort)Number("tid",65535),SID16=(ushort)Number("sid",65535),Gender=(byte)Number("gender",1),Language=language,
            ConsoleRegion=(byte)Number("consoleRegion",255),Country=(byte)Number("country",255),Region=(byte)Number("region",255)};
    }
}
