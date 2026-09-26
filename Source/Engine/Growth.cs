using PKHeX.Core;
using System.Text.Json;
sealed partial class EditorSession
{
    static bool SupportsPokerus(PKM pk) => pk.Context.CanOriginatePokerus() || pk is PA8;
    object? GrowthInfo()
    {
        if(entity is not { Species: > 0 } pk) return null;
        byte level=pk.CurrentLevel, growth=pk.PersonalInfo.EXPGrowth;
        uint floor=Experience.GetEXP(level,growth), next=Experience.GetEXP((byte)Math.Min(100,level+1),growth);
        return new { exp=pk.EXP, level, floor, next, friendship=pk.CurrentFriendship, egg=pk.IsEgg, friendshipField=pk.CurrentHandler==1 ? "HandlingTrainerFriendship":"OriginalTrainerFriendship", pokerus=SupportsPokerus(pk), pokerusState=pk.PokerusDays>0 ? "Infected":pk.PokerusStrain>0 ? "Cured":"None" };
    }
    static readonly Dictionary<string,Dictionary<string,string>> abilityDescriptions = LoadAbilityDescriptions();
    static Dictionary<string,Dictionary<string,string>> LoadAbilityDescriptions()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"GameData","abilityDescriptions.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string,Dictionary<string,string>>>(File.ReadAllText(path)) ?? new() : new();
    }
    string AbilityDescription()
    {
        if(entity is not { Format: >=3 } pk || !abilityDescriptions.TryGetValue(pk.Ability.ToString(),out var versions)) return "";
        int group=pk.Version switch {GameVersion.R or GameVersion.S or GameVersion.COLO or GameVersion.XD=>5,GameVersion.E=>6,GameVersion.FR or GameVersion.LG=>7,GameVersion.D or GameVersion.P=>8,GameVersion.Pt=>9,GameVersion.HG or GameVersion.SS=>10,GameVersion.B or GameVersion.W=>11,GameVersion.B2 or GameVersion.W2=>14,GameVersion.X or GameVersion.Y=>15,GameVersion.OR or GameVersion.AS=>16,GameVersion.SN or GameVersion.MN=>17,GameVersion.US or GameVersion.UM=>18,GameVersion.GP or GameVersion.GE=>19,GameVersion.SW or GameVersion.SH=>20,GameVersion.BD or GameVersion.SP=>23,GameVersion.PLA=>24,GameVersion.SL or GameVersion.VL=>25,_=>0};
        if(versions.TryGetValue(group.ToString(),out var text)) return text;
        return versions.OrderByDescending(z=>int.Parse(z.Key)).FirstOrDefault().Value ?? "";
    }
}
