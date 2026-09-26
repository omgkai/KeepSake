using System.Text.Json;
using PKHeX.Core;
using PKHeX.Core.Searching;

sealed partial class EditorSession
{
    IEncounterable[] encounterResults = [];
    string encounterToken = "";
    int encounterRevision = -1;
    object SearchEncounters(JsonElement r)
    {
        var sav = RequireSave();
        int species = N(r,"species"), form = N(r,"form",-1);
        if (species < 1 || species > sav.MaxSpeciesID) throw new Exception("Choose a species supported by the open save.");
        var pi = sav.Personal.GetFormEntry((ushort)species,0);
        int forms = Math.Max(1,(int)pi.FormCount);
        if (form < -1 || form >= forms) throw new Exception("Choose a valid form, or all forms.");
        var versionText = S(r,"version","Any");
        var version = GameVersion.Any;
        if (versionText != "Any" && (!Enum.TryParse(versionText,out version) || !version.IsValidSavedVersion())) throw new Exception("Choose a valid origin game.");
        var categoryText = S(r,"category","Any");
        EncounterTypeGroup[] categories = categoryText == "Any" ? Enum.GetValues<EncounterTypeGroup>().Where(x=>x!=0).ToArray()
            : Enum.TryParse<EncounterTypeGroup>(categoryText,out var category) && Enum.IsDefined(category) && category!=0 ? [category] : throw new Exception("Choose a valid encounter type.");
        var shiny = S(r,"shiny","Any");
        if (shiny is not ("Any" or "Always" or "Never")) throw new Exception("Choose a valid shiny restriction.");
        var moves = r.TryGetProperty("moves",out var moveList) ? moveList.EnumerateArray().Select(x=>x.GetInt32()).Where(x=>x!=0).Distinct().ToArray() : [];
        if (moves.Length > 4 || moves.Any(x=>x<0 || x>sav.MaxMoveID)) throw new Exception("Choose up to four valid moves.");
        var instructions=SearchInstructions(S(r,"filters"));
        var settings = new SearchSettings {Context=sav.Context,Generation=sav.Generation,Version=version,Species=(ushort)species};
        var versions = settings.GetVersions(sav);
        var found = new List<IEncounterable>();
        var seen = new HashSet<IEncounterable>(ReferenceEqualityComparer.Instance);
        bool truncated = false;
        var pk = sav.BlankPKM;
        try {
            EncounterMovesetGenerator.PriorityList = categories;
            for (int f=0;f<forms && !truncated;f++) {
                if (form>=0 && f!=form || FormInfo.IsBattleOnlyForm((ushort)species,(byte)f,pk.Format)) continue;
                pk.Species=(ushort)species;pk.Form=(byte)f;pk.SetGender(pk.GetSaneGender());
                EncounterMovesetGenerator.OptimizeCriteria(pk,sav);
                foreach (var enc in EncounterMovesetGenerator.GenerateEncounters(pk,moves.Select(x=>(ushort)x).ToArray(),versions)) {
                    if (shiny == "Always" && !enc.IsShiny || shiny == "Never" && enc.Shiny != Shiny.Never) continue;
                    if (!BatchEditingUtil.IsFilterMatch(instructions,enc) || !seen.Add(enc)) continue;
                    if (found.Count == this.settings.EncounterResultLimit) {truncated=true;break;}
                    found.Add(enc);
                }
            }
        } finally {EncounterMovesetGenerator.ResetFilters();}
        encounterResults=found.ToArray();encounterToken=Guid.NewGuid().ToString("N");encounterRevision=revision;
        return new {token=encounterToken,truncated,entries=encounterResults.Select((e,i)=>new {
            id=i,name=Species(e.Species),form=(int)e.Form,formName=EncounterFormName(e),game=GameInfo.GetVersionName(e.Version),kind=e.LongName,
            level=e.LevelMin==e.LevelMax ? e.LevelMin.ToString() : $"{e.LevelMin}–{e.LevelMax}",
            location=e.GetEncounterLocation(e.Generation,e.Version),shiny=e.Shiny.ToString(),egg=e.IsEgg,
            details=string.Join("\n",e.GetTextLines()),sprite=SpriteFor(e.Species,e.Form,0,0,e.Context),portrait=$"{e.Context}:{e.Species}:{e.Form}:0:0:0"
        }).ToArray()};
    }
    string EncounterFormName(IEncounterTemplate enc)
    {
        var names=FormConverter.GetFormList(enc.Species,strings.Types,strings.forms,enc.Context);
        return enc.Form<names.Length && !string.IsNullOrWhiteSpace(names[enc.Form]) ? names[enc.Form] : "Normal";
    }
    void PrepareEncounter(JsonElement r)
    {
        var sav=RequireSave();int id=N(r,"id");
        if (S(r,"token")!=encounterToken || encounterRevision!=revision) throw new Exception("The workspace changed. Search again before preparing an encounter.");
        if (id<0 || id>=encounterResults.Length) throw new Exception("Choose an encounter from the results.");
        var criteria=EncounterCriteria.Unrestricted;
        if(B(r,"useEditorCriteria")) {var current=RequireEntity();if(current.Species!=encounterResults[id].Species)throw new Exception("Editor criteria require the encounter and editor to have the same species.");criteria=EncounterCriteria.GetCriteria(new ShowdownSet(current),current.PersonalInfo,EncounterMutation.None);}
        if(r.TryGetProperty("criteria",out var custom))criteria=ReadEncounterCriteria(custom);
        ITrainerInfo trainer=r.TryGetProperty("trainer",out var profile)?ReadEncounterTrainer(profile,encounterResults[id]):sav;
        var pk=encounterResults[id].ConvertToPKM(trainer,criteria);
        if (pk.GetType()!=sav.PKMType) pk=EntityConverter.ConvertToType(pk,sav.PKMType,out _) ?? throw new Exception("This encounter cannot be transferred into this save format.");
        if (!sav.Personal.IsPresentInGame(pk.Species,pk.Form)) throw new Exception("The encounter's species or form is unavailable in this game.");
        sav.AdaptToSaveFile(pk);pk.RefreshChecksum();entity=pk;entitySourcePath=null;pending=true;
    }
}
