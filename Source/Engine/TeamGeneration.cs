using System.Diagnostics;
using System.Text.Json;
using PKHeX.Core;
using PKHeX.Core.AutoMod;

// A separate worker gives expensive encounter searches a hard deadline without
// leaving a timed-out task running against the editor's global PKHeX settings.
static class LegalityWorker
{
    public static void Run()
    {
        try {
            var request=JsonSerializer.Deserialize<GenerationRequest>(Console.ReadLine()!)!;
            ParseSettings.Initialize(request.Settings);
            var pk=EntityFormat.GetFromBytes(Convert.FromBase64String(request.Data),request.Trainer.Context) ?? throw new Exception("Invalid Pokémon template.");
            var info=request.Trainer;
            var trainer=new SimpleTrainerInfo(request.Version){Context=info.Context,Generation=info.Generation,OT=info.OT,ID32=info.ID32,Gender=info.Gender,Language=info.Language,ConsoleRegion=info.ConsoleRegion,Country=info.Country,Region=info.Region};
            Legalizer.EnableEasterEggs=false;
            APILegality.AllowBatchCommands=false;APILegality.AllowTrainerOverride=false;
            APILegality.SetAllLegalRibbons=false;APILegality.UseMarkings=false;APILegality.ForceLevel100for50=false;
            APILegality.UseTrainerData=true;APILegality.Timeout=15;
            TrainerSettings.DefaultOT=trainer.OT;TrainerSettings.DefaultTID16=trainer.TID16;TrainerSettings.DefaultSID16=trainer.SID16;
            TrainerSettings.Register(trainer);
            var set=request.Set is null ? new ShowdownSet(pk):new ShowdownSet(request.Set);
            if(set.InvalidLines.Count>0) throw new Exception("The battle set contains unsupported lines.");
            var candidate=trainer.GetLegalFromTemplate(pk,new RegenTemplate(set,trainer.Generation),out var status);
            var analysis=new LegalityAnalysis(candidate);
            if(status!=LegalizationResult.Regenerated || !analysis.Valid) throw new Exception(status==LegalizationResult.Timeout ? "The encounter search timed out. Try a less restrictive set." : "No legal encounter satisfies these details. " + analysis.Report());
            Console.WriteLine(JsonSerializer.Serialize(new {ok=true,data=EditorSession.PokemonBytes(candidate)}));
        } catch(Exception e) {Console.WriteLine(JsonSerializer.Serialize(new {ok=false,error=e.Message}));}
    }
}
record GenerationTrainer(string OT,uint ID32,byte Gender,int Language,EntityContext Context,byte Generation,byte ConsoleRegion,byte Country,byte Region);
record GenerationRequest(GenerationTrainer Trainer,GameVersion Version,string Data,string? Set,LegalitySettings Settings);
sealed partial class EditorSession
{
    PKM[] generatedTeam=[];
    string generationToken="",generationKind="",generationSource="",generationSettings="";
    int generationRevision=-1;
    public static string PokemonBytes(PKM pk) {pk=pk.Clone();var bytes=new byte[pk.SIZE_PARTY];pk.WriteDecryptedDataParty(bytes);return Convert.ToBase64String(bytes);}
    static PKM ReadJournalPokemon(string data,string extension)
    {
        if(data.Length>4096) throw new Exception("The journal Pokémon snapshot is too large.");
        var pk=EntityFormat.GetFromBytes(Convert.FromBase64String(data),EntityFileExtension.GetContextFromExtension("."+extension)) ?? throw new Exception("The journal Pokémon snapshot could not be read.");
        if(pk.Species==0) throw new Exception("Choose a non-empty Pokémon.");
        return pk;
    }
    PKM GenerateLegal(PKM template,ITrainerInfo trainer,string? set=null)
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!) {RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet") start.ArgumentList.Add(typeof(EditorSession).Assembly.Location);
        start.ArgumentList.Add("--legalize-worker");
        using var worker=Process.Start(start) ?? throw new Exception("Could not start Auto-Legality.");
        var output=worker.StandardOutput.ReadToEndAsync();var errors=worker.StandardError.ReadToEndAsync();
        var info=new SimpleTrainerInfo(trainer);
        var details=new GenerationTrainer(info.OT,info.ID32,info.Gender,info.Language,info.Context,info.Generation,info.ConsoleRegion,info.Country,info.Region);
        worker.StandardInput.WriteLine(JsonSerializer.Serialize(new GenerationRequest(details,trainer.Version,PokemonBytes(template),set,settings.Legality)));worker.StandardInput.Close();
        if(!worker.WaitForExit(25000)) {worker.Kill(true);worker.WaitForExit();throw new Exception("Auto-Legality timed out. Your Pokémon has not been changed.");}
        var line=output.GetAwaiter().GetResult().Trim().Split('\n').LastOrDefault() ?? "";
        using var result=JsonDocument.Parse(line);
        if(!B(result.RootElement,"ok")) throw new Exception(S(result.RootElement,"error","Auto-Legality could not find a result."));
        var pk=ReadJournalPokemon(S(result.RootElement,"data"),template.Extension);
        if(pk.GetType()!=template.GetType() || !new LegalityAnalysis(pk).Valid) throw new Exception("Auto-Legality returned an incompatible result.");
        return pk;
    }
    object GenerationPreview(JsonElement r,bool team)
    {
        generatedTeam=[];generationToken="";
        var results=new List<object>();var candidates=new List<PKM>();
        if(team) {
            var sav=RequireSave();
            if(pending) throw new Exception("Set or discard the current Pokémon edits before preparing a team.");
            var members=r.GetProperty("members").EnumerateArray().ToArray();
            if(members.Length is <1 or >6) throw new Exception("Choose between one and six companions.");
            foreach(var member in members) {
                try {
                    PKM pk;string method;
                    var data=S(member,"data");
                    if(!string.IsNullOrEmpty(data)) {
                        var original=ReadJournalPokemon(data,S(member,"extension"));
                        pk=original.GetType()==sav.PKMType ? original.Clone():EntityConverter.ConvertToType(original,sav.PKMType,out _) ?? throw new Exception("This Pokémon cannot transfer to the loaded game. Older games cannot accept Pokémon from newer formats.");
                        method=original.GetType()==sav.PKMType ? "Saved Pokémon snapshot":"Transferred to "+sav.BlankPKM.Extension;
                    } else {
                        int species=N(member,"species"),form=N(member,"form");
                        if(species<1 || species>sav.MaxSpeciesID || form<0 || form>255 || !sav.Personal.IsPresentInGame((ushort)species,(byte)form)) throw new Exception("This species or form is unavailable in the loaded game.");
                        pk=sav.BlankPKM;pk.Species=(ushort)species;pk.Form=(byte)form;pk.Version=sav.Version;pk.CurrentLevel=100;pk.Language=sav.Language;pk.SetGender(pk.GetSaneGender());
                        string set=new ShowdownSet(pk).Text;
                        // Personal pages specify species/artwork, not a battle build. Let ALM choose legal IVs and moves.
                        set=set.Split('\n')[0]+"\nLevel: 100\n"+(B(member,"shiny") ? "Shiny: Yes\n":"");
                        pk=GenerateLegal(pk,sav,set);method="Generated from a personal page · Lv. 100";
                    }
                    if(!sav.Personal.IsPresentInGame(pk.Species,pk.Form)) throw new Exception("This species or form is unavailable in the loaded game.");
                    sav.AdaptToSaveFile(pk);pk.RefreshChecksum();
                    var analysis=new LegalityAnalysis(pk);
                    if(!analysis.Valid) throw new Exception("The saved Pokémon needs legality review in this game. Open it in the editor and use Auto-Legality. "+analysis.Report());
                    candidates.Add(pk);results.Add(GenerationRow(results.Count,pk,method,[],""));
                } catch(Exception ex) {results.Add(new {id=results.Count,name=S(member,"name","Companion"),sprite="b_0",level=0,extension="",method="Could not prepare",changes=Array.Empty<string>(),error=ex.Message,report=""});}
            }
            if(candidates.Count==members.Length) generatedTeam=candidates.ToArray();
        } else {
            var original=RequireEntity();if(original.Species==0) throw new Exception("Choose a Pokémon first.");
            ITrainerInfo trainer=save ?? (ITrainerInfo)new SimpleTrainerInfo(original.Version){Context=original.Context,Generation=original.Format,OT=original.OriginalTrainerName,Gender=original.OriginalTrainerGender,ID32=original.ID32,Language=original.Language};
            var candidate=new LegalityAnalysis(original).Valid ? original.Clone():GenerateLegal(original.Clone(),trainer);
            var before=EntityFields(original).ToDictionary(f=>f.id,f=>f.value);
            var changes=EntityFields(candidate).Where(f=>before.TryGetValue(f.id,out var value)&&value!=f.value).Select(f=>$"{f.label}: {before[f.id]} → {f.value}").ToArray();
            generatedTeam=[candidate];results.Add(GenerationRow(0,candidate,changes.Length==0 ? "Already legal":"Regenerated from a matching legal encounter",changes,""));
        }
        generationToken=Guid.NewGuid().ToString("N");generationRevision=revision;generationKind=team ? "team":"legality";generationSource=team ? "":PokemonBytes(RequireEntity());generationSettings=JsonSerializer.Serialize(settings);
        return new {token=generationToken,revision,ready=generatedTeam.Length>0,entries=results,game=save==null ? GameInfo.GetVersionName(RequireEntity().Version):GameInfo.GetVersionName(save.Version)};
    }
    object GenerationRow(int id,PKM pk,string method,string[] changes,string error) => new {id,name=pk.IsNicknamed?pk.Nickname:Species(pk.Species),sprite=Sprite(pk),level=pk.CurrentLevel,extension=pk.Extension,method,changes,error,report=new LegalityAnalysis(pk).Report(true)};
    void CheckGeneration(JsonElement r,string kind)
    {
        if(generatedTeam.Length==0 || S(r,"token")!=generationToken || revision!=generationRevision || generationKind!=kind || generationSettings!=JsonSerializer.Serialize(settings) || (kind=="legality" && generationSource!=PokemonBytes(RequireEntity()))) throw new Exception("The workspace changed. Prepare a new preview first.");
    }
    void ApplyLegality(JsonElement r) {CheckGeneration(r,"legality");entity=generatedTeam[0].Clone();pending=true;}
    void PlaceGeneratedTeam(JsonElement r)
    {
        CheckGeneration(r,"team");var sav=RequireSave();int target=N(r,"box",-1);
        if(target<0 || target>=sav.BoxCount) throw new Exception("Choose a destination box.");
        if(pending) throw new Exception("Set or discard Pokémon edits first.");
        var slots=Enumerable.Range(0,sav.BoxSlotCount).Where(i=>target*sav.BoxSlotCount+i<sav.SlotCount && !sav.GetBoxSlotFlags(target,i).IsOverwriteProtected() && sav.GetBoxSlotFlags(target,i).IsParty()<0 && sav.GetBoxSlotAtIndex(target,i).Species==0).Take(generatedTeam.Length).ToArray();
        if(slots.Length<generatedTeam.Length) throw new Exception("This box does not have enough empty, unlocked slots. Choose another box.");
        for(int i=0;i<slots.Length;i++) sav.SetBoxSlotAtIndex(generatedTeam[i].Clone(),target,slots[i]);
        dirty=true;Select(target,slots[0],false);
    }
    object ExportGeneratedTeam(JsonElement r)
    {
        CheckGeneration(r,"team");if(demo) throw new Exception("Open a real save to export a team.");
        string directory=Path.GetFullPath(S(r,"path"));if(!Directory.Exists(directory)) throw new Exception("Choose an existing folder.");
        // Create a new team folder as one rename, so a failed export leaves no partial team.
        string name="KeepSake Team "+DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss")+"-"+Guid.NewGuid().ToString("N")[..6];
        string target=Path.Combine(directory,name),temporary=target+".tmp";
        try {Directory.CreateDirectory(temporary);for(int i=0;i<generatedTeam.Length;i++) {var pk=generatedTeam[i];File.WriteAllBytes(Path.Combine(temporary,$"{i+1:D2}-{pk.Species}.{pk.Extension}"),Convert.FromBase64String(PokemonBytes(pk)));}Directory.Move(temporary,target);}
        finally {if(Directory.Exists(temporary)) Directory.Delete(temporary,true);}
        return new {path=target};
    }
}
