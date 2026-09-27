using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    static string MoveLabel(IReadOnlyList<string> names,int index) => index>=0 && index<names.Count && !string.IsNullOrWhiteSpace(names[index]) ? names[index] : index.ToString();
    record MoveCheck(int slot, bool relearn, int move, string status, string detail, int type, string typeName);
    MoveCheck[] CheckMoves(LegalityAnalysis? analysis)
    {
        if (entity is not { Species: > 0 } pk) return [];
        var rows = new List<MoveCheck>();
        Span<ushort> moves = stackalloc ushort[4];
        for (int group = 0; group < (pk.Format >= 6 ? 2 : 1); group++)
        {
            if (group == 0) pk.GetMoves(moves); else pk.GetRelearnMoves(moves);
            for (int i = 0; i < 4; i++)
            {
                var check = analysis == null ? default : (group == 0 ? analysis.Info.Moves[i] : analysis.Info.Relearn[i]);
                var status = settings.PKHaXMode ? "unchecked" : analysis?.Parsed != true || !check.IsParsed ? "unknown" : !check.Valid ? "illegal" : moves[i] == 0 ? "empty" : "legal";
                var detail = settings.PKHaXMode ? "PKHaX mode: move legality is not checked." : status == "unknown" ? "PKHeX could not evaluate this move. Check the full legality report." : check.Summary(LegalityLocalizationContext.Create(analysis!));
                var type=MoveInfo.GetType(moves[i],pk.Context);
                rows.Add(new(i + 1, group != 0, moves[i], status, detail,type,MoveLabel(strings.Types,type)));
            }
        }
        return rows.ToArray();
    }
    void ApplyPokemonEdits(PKM pk, JsonElement edits)
    {
        foreach(var edit in edits.EnumerateArray())
        {
            var id=S(edit,"field");var value=S(edit,"value");
            if(id=="AbilityChoice") {SetAbilityChoice(pk,value);continue;}
            if(id=="IsShiny") {SetShinyFlag(pk,bool.Parse(value));continue;}
            if(!EntityFields(pk).Any(f=>f.id==id && f.editable)) throw new Exception("This field is not editable in this format.");
            SetProperty(pk,id,value);
            if(id=="Nickname")pk.SetNickname(value);
            if(id=="Species" && !pk.IsNicknamed)pk.ClearNickname();
            if(id=="Ability")pk.SetAbility(int.Parse(value,System.Globalization.CultureInfo.InvariantCulture));
        }
    }
    record MoveOption(int id,string name,int type,string typeName,int pp,string status,string source);
    object MoveChoices(JsonElement r)
    {
        var pk=RequireEntity().Clone();
        if(r.TryGetProperty("edits",out var edits))ApplyPokemonEdits(pk,edits);
        var info=new LegalMoveInfo();bool evaluated=false;
        if(pk.Species>0 && !settings.PKHaXMode) {
            try {
                var analysis=new LegalityAnalysis(pk);
                if(analysis.Parsed) {info.ReloadMoves(analysis);evaluated=Enumerable.Range(1,pk.MaxMoveID).Any(i=>info.CanLearn((ushort)i));}
            } catch { /* Unknown is distinct from not learnable. */ }
        }
        var entries=Enumerable.Range(0,Math.Min(pk.MaxMoveID+1,strings.movelist.Length)).Select(i=> {
            var move=(ushort)i;var type=MoveInfo.GetType(move,pk.Context);
            return new MoveOption(i,i==0 ? "No move" : strings.movelist[i],type,MoveLabel(strings.Types,type),MoveInfo.GetPP(pk.Context,move),i==0 ? "empty" : settings.PKHaXMode ? "unchecked" : !evaluated ? "unknown" : info.CanLearn(move) ? "learnable" : "unavailable",info.GetMoveSources(move).ToString());
        }).OrderBy(m=>m.id==0 ? 0 : m.status=="learnable" ? 1 : 2).ThenBy(m=>m.name).ToArray();
        return new {revision,species=Species(pk.Species),entries};
    }
    static bool SameMoveSet(ReadOnlySpan<ushort> a,ReadOnlySpan<ushort> b) => a.ToArray().Order().SequenceEqual(b.ToArray().Order());
    static void InstallSuggestedMoves(PKM pk,ReadOnlySpan<ushort> moves)
    {
        pk.SetMoves(moves);
        if(pk is ITechRecord records) {
            var updated=new LegalityAnalysis(pk);
            records.SetRecordFlags(moves,updated.Info.EvoChainsAllGens.Get(pk.Context));
        }
        pk.Move1_PPUps=pk.Move2_PPUps=pk.Move3_PPUps=pk.Move4_PPUps=0;
        pk.HealPP();pk.RefreshChecksum();
    }
    object SuggestMoves(JsonElement r)
    {
        var original=RequireEntity();var pk=original.Clone();
        if(r.TryGetProperty("edits",out var edits))ApplyPokemonEdits(pk,edits);
        if(pk.Species==0)throw new Exception("Select a Pokémon first.");
        var mode=S(r,"mode","current");
        if(mode is not ("current" or "relearn" or "level" or "different"))throw new Exception("Unknown move suggestion mode.");
        var analysis=new LegalityAnalysis(pk);
        if(!analysis.Parsed)throw new Exception("PKHeX could not analyze this Pokémon to suggest moves.");
        Span<ushort> moves=stackalloc ushort[4];Span<ushort> current=stackalloc ushort[4];
        string message;
        if(mode=="relearn") {
            if(pk.Format<6)throw new Exception("This format does not store relearn moves.");
            pk.GetRelearnMoves(current);analysis.GetSuggestedRelearnMoves(moves);pk.SetRelearnMoves(moves);
            message=moves.SequenceEqual(current) ? "The relearn list already matches this encounter." : "Relearn moves updated. Review them before Set to Slot.";
        } else {
            pk.GetMoves(current);pk.GetMoveSet(moves);
            if(moves[0]==0)throw new Exception("PKHeX has no suggested current moves for this encounter. Check its relearn moves and legality report.");
            var preferDifferent=mode=="different" || mode=="current" && SameMoveSet(moves,current);
            PKM? candidate=null;
            for(int attempt=0;attempt<32;attempt++) {
                if(preferDifferent || attempt>0)pk.GetMoveSet(moves,true);
                if(preferDifferent && SameMoveSet(moves,current))continue;
                var trial=pk.Clone();InstallSuggestedMoves(trial,moves);
                var check=new LegalityAnalysis(trial);
                if(check.Parsed && MoveResult.AllValid(check.Info.Moves)) {candidate=trial;break;}
            }
            if(candidate!=null) {
                if(!SameMoveSet(moves,current))pk=candidate;
                message=SameMoveSet(moves,current) ? "This Pokémon already has PKHeX's default suggested set." : "Moves updated: "+string.Join(" / ",moves.ToArray().Where(m=>m!=0).Select(m=>MoveLabel(strings.movelist,m)))+". Set to Slot keeps them.";
            } else {
                var existing=new LegalityAnalysis(pk);
                if(!existing.Parsed || !MoveResult.AllValid(existing.Info.Moves))throw new Exception("No valid move set could be generated for this encounter. Review its encounter and relearn data.");
                message="No different valid set was found for this encounter. Your current moves are already legal.";
            }
        }
        pk.RefreshChecksum();
        if(!pk.Data.SequenceEqual(original.Data))Mutate(()=>{entity=pk;pending=true;});
        return State(message);
    }
}
