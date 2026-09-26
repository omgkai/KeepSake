using PKHeX.Core;
using System.Text.Json;
using System.Text.RegularExpressions;

sealed partial class EditorSession {
    object ShowdownTeamPreview(JsonElement r) {
        generatedTeam=[];generationToken="";var sav=RequireSave();
        if(pending)throw new Exception("Set or discard the current Pokémon edits before importing a team.");
        var text=S(r,"text").Trim();if(text.Length>32000)throw new Exception("Keep team text under 32,000 characters.");
        var chunks=Regex.Split(text,@"\r?\n\s*\r?\n").Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();
        if(chunks.Length is <1 or >6)throw new Exception("Paste between one and six sets, separated by blank lines.");
        var sets=chunks.Select(x=>new ShowdownSet(x)).ToArray();
        for(int i=0;i<sets.Length;i++) {var set=sets[i];if(set.Species==0 || !sav.Personal.IsPresentInGame(set.Species,set.Form))throw new Exception($"Set {i+1}: species or form is unavailable in this game.");if(set.InvalidLines.Count>0)throw new Exception($"Set {i+1}: unrecognized lines: "+string.Join("; ",set.InvalidLines));}
        var candidates=new List<PKM>();var results=new List<object>();
        foreach(var set in sets) {
            try {var template=sav.BlankPKM;template.Species=set.Species;template.Form=set.Form;template.Version=sav.Version;template.Language=sav.Language;template.ApplySetDetails(set);
                var pk=GenerateLegal(template,sav,set.Text);sav.AdaptToSaveFile(pk);pk.RefreshChecksum();if(!new LegalityAnalysis(pk).Valid)throw new Exception("The adapted Pokémon needs legality review.");
                candidates.Add(pk);results.Add(GenerationRow(results.Count,pk,"Generated from Showdown text",[],""));
            }catch(Exception ex){results.Add(new {id=results.Count,name=Species(set.Species),sprite="b_"+set.Species,level=0,extension="",method="Could not prepare",changes=Array.Empty<string>(),error=ex.Message,report=""});}
        }
        if(candidates.Count==sets.Length)generatedTeam=candidates.ToArray();
        generationKind="team";generationRevision=revision;generationToken=Guid.NewGuid().ToString("N");generationSettings=JsonSerializer.Serialize(settings);
        return new {token=generationToken,revision,ready=generatedTeam.Length>0,entries=results,game=GameInfo.GetVersionName(sav.Version)};
    }
}
