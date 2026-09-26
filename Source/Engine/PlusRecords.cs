using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    object PlusRecords()
    {
        var pk=RequireEntity();
        if (pk is not IPlusRecord plus || pk.PersonalInfo is not IPermitPlus permit) throw new Exception("Move-plus records are available for Legends: Z-A Pokémon.");
        var legal=new LegalityAnalysis(pk);var possible=new bool[pk.MaxMoveID+1];
        LearnPossible.Get(pk,legal.EncounterMatch,legal.Info.EvoChainsAllGens,possible);
        var current=new ushort[]{pk.Move1,pk.Move2,pk.Move3,pk.Move4};
        return new {shop=false,entries=permit.PlusMoveIndexes.ToArray().Select((move,i)=>new {
            id=i,move,name=strings.movelist[move],type=strings.Types[MoveInfo.GetType(move,pk.Context)],
            permitted=possible[move] || current.Contains(move),learned=plus.GetMovePlusFlag(i),mastered=false
        }).ToArray()};
    }
    void EditPlusRecords(JsonElement r)
    {
        var pk=RequireEntity();
        if (pk is not IPlusRecord plus || pk.PersonalInfo is not IPermitPlus permit) throw new Exception("This format has no move-plus records.");
        var mode=S(r,"mode","one");
        if (mode=="one") {
            int i=N(r,"index");if (i<0 || i>=permit.PlusMoveIndexes.Length) throw new Exception("Choose a valid move-plus record.");
            plus.SetMovePlusFlag(i,B(r,"learned"));
        } else {
            var option=mode switch {
                "clear"=>PlusRecordApplicatorOption.None,
                "current"=>PlusRecordApplicatorOption.LegalCurrent,
                "tm"=>PlusRecordApplicatorOption.LegalCurrentTM,
                "seed"=>PlusRecordApplicatorOption.LegalSeedTM,
                _=>throw new Exception("Choose a valid move-plus action.")
            };
            plus.SetPlusFlags(pk,permit,option);
        }
        pk.RefreshChecksum();pending=true;
    }
}
