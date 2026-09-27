using PKHeX.Core;
using System.Text.Json;
sealed partial class EditorSession
{
    static readonly Dictionary<string,Dictionary<string,Dictionary<string,string>>> itemDescriptions=LoadItemDescriptions();
    static Dictionary<string,Dictionary<string,Dictionary<string,string>>> LoadItemDescriptions()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"GameData","itemDescriptions.json");
        return File.Exists(path)?JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,Dictionary<string,string>>>>(File.ReadAllText(path))??new():new();
    }
    string HeldItemDescription()
    {
        if(entity is not {} pk || pk.HeldItem==0)return "";
        var game=save?.Context==pk.Context?save.Version:pk.Version;
        var names=GameInfo.GetStrings("en").GetItemStrings(pk.Context,game);
        if(pk.HeldItem<0 || pk.HeldItem>=names.Length)return "Description unavailable for this item.";
        Dictionary<string,string>? versions=null;
        if(itemDescriptions.TryGetValue("names",out var catalog))catalog.TryGetValue(names[pk.HeldItem],out versions);
        if(versions==null && pk.Format<=3 && itemDescriptions.TryGetValue("legacy",out var legacy))legacy.TryGetValue($"{pk.Format}:{pk.HeldItem}",out versions);
        if(versions==null || versions.Count==0)return "Description unavailable for this item.";
        int group=game switch {GameVersion.GD or GameVersion.SI=>3,GameVersion.C=>4,GameVersion.R or GameVersion.S or GameVersion.COLO or GameVersion.XD=>5,GameVersion.E=>6,GameVersion.FR or GameVersion.LG=>7,GameVersion.D or GameVersion.P=>8,GameVersion.Pt=>9,GameVersion.HG or GameVersion.SS=>10,GameVersion.B or GameVersion.W=>11,GameVersion.B2 or GameVersion.W2=>14,GameVersion.X or GameVersion.Y=>15,GameVersion.OR or GameVersion.AS=>16,GameVersion.SN or GameVersion.MN=>17,GameVersion.US or GameVersion.UM=>18,GameVersion.GP or GameVersion.GE=>19,GameVersion.SW or GameVersion.SH=>20,GameVersion.BD or GameVersion.SP=>23,GameVersion.PLA=>24,GameVersion.SL or GameVersion.VL=>25,_=>0};
        if(versions.TryGetValue(group.ToString(),out var text))return text;
        return versions.OrderByDescending(x=>int.Parse(x.Key)).First().Value+" (Reference description; wording may differ in this game.)";
    }
    object? NatureInfo()=>entity is {} pk?new {nature=(int)pk.Nature,alignment=(int)pk.StatAlignment,hasEffects=pk.Format>=3,mint=pk.Format>=8}:null;
}
