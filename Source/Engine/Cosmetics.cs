using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    static Choice[] SampleGames()=>new[]{GameVersion.RD,GameVersion.GN,GameVersion.BU,GameVersion.YW,GameVersion.GD,GameVersion.SI,GameVersion.C,GameVersion.R,GameVersion.S,GameVersion.E,GameVersion.FR,GameVersion.LG,GameVersion.D,GameVersion.P,GameVersion.Pt,GameVersion.HG,GameVersion.SS,GameVersion.B,GameVersion.W,GameVersion.B2,GameVersion.W2,GameVersion.X,GameVersion.Y,GameVersion.OR,GameVersion.AS,GameVersion.SN,GameVersion.MN,GameVersion.US,GameVersion.UM,GameVersion.GP,GameVersion.GE,GameVersion.SW,GameVersion.SH,GameVersion.BD,GameVersion.SP,GameVersion.PLA,GameVersion.SL,GameVersion.VL,GameVersion.ZA}.Select(v=>new Choice(v.ToString(),GameInfo.Strings.gamelist[(int)v])).ToArray();
    object? CosmeticInfo() {
        if(entity is not {} pk)return null;
        return new {
            height=pk is IScaledSize a?PokeSizeUtil.GetSizeRating(a.HeightScalar).ToString():"",
            weight=pk is IScaledSize b?PokeSizeUtil.GetSizeRating(b.WeightScalar).ToString():"",
            scale=pk is IScaledSize3 c?(pk is PK9?PokeSizeDetailedUtil.GetSizeRating(c.Scale).ToString():PokeSizeUtil.GetSizeRating(c.Scale).ToString()):"",
            canRecalculate=pk is IScaledSizeValue or ICombatPower,
            origin=pk.Version.ToString(),battle=pk is IBattleVersion v?v.BattleVersion.ToString():""
        };
    }
    void EditCosmetics(JsonElement r) {
        var pk=RequireEntity();string mode=S(r,"mode");
        if(mode=="size") {
            string field=S(r,"field");if(field is not ("HeightScalar" or "WeightScalar" or "Scale"))throw new Exception("Choose a size value.");
            if(!(field=="Scale"?pk is IScaledSize3:pk is IScaledSize))throw new Exception("This format does not store that size value.");
            if(!byte.TryParse(S(r,"value"),out var value))throw new Exception("Size values range from 0 to 255.");
            SetProperty(pk,field,value.ToString());
            if(pk is PA8 pa && field is "Scale" or "HeightScalar")pa.Scale=pa.HeightScalar=value;
            if(B(r,"automatic")){if(pk is IScaledSizeValue sizes){sizes.ResetHeight();sizes.ResetWeight();}if(pk is ICombatPower cp)cp.ResetCP();}
        }
        else if(mode=="recalculate") {if(pk is IScaledSizeValue sizes){sizes.ResetHeight();sizes.ResetWeight();}if(pk is ICombatPower cp)cp.ResetCP();if(pk is not IScaledSizeValue and not ICombatPower)throw new Exception("This format has no calculated size or CP.");}
        else if(mode=="leaves") {if(pk is not G4PKM leaves)throw new Exception("Shiny Leaves are stored only in Generation 4 formats.");int value=N(r,"value");if(value<0||value>63||(value&32)!=0&&(value&31)!=31)throw new Exception("The crown requires all five Shiny Leaves.");leaves.ShinyLeaf=value;}
        else if(mode=="contest") {if(pk is not IContestStats contest)throw new Exception("This format has no contest conditions.");byte value=B(r,"all")?(byte)255:(byte)0;contest.ContestCool=contest.ContestBeauty=contest.ContestCute=contest.ContestSmart=contest.ContestTough=contest.ContestSheen=value;}
        else throw new Exception("Unknown cosmetic action.");
        pk.RefreshChecksum();pending=true;
    }
}
