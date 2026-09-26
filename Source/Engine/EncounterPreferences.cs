using PKHeX.Core;
using System.Text.Json;

sealed partial class EditorSession
{
    object EncounterCriteriaInfo(JsonElement r)
    {
        var c=EncounterCriteria.Unrestricted;
        if(B(r,"fromEditor")) {
            var pk=RequireEntity();
            c=EncounterCriteria.GetCriteria(new ShowdownSet(pk),pk.PersonalInfo,EncounterMutation.None);
        }
        return new {nature=(int)c.Nature,gender=(int)c.Gender,ability=(int)c.Ability,shiny=(int)c.Shiny,
            ivs=new int[]{c.IV_HP,c.IV_ATK,c.IV_DEF,c.IV_SPA,c.IV_SPD,c.IV_SPE},
            levelMin=(int)c.LevelMin,levelMax=(int)c.LevelMax,hiddenPower=(int)c.HiddenPowerType,form=(int)c.Form,mutations=(int)c.Mutations};
    }
    static EncounterCriteria ReadEncounterCriteria(JsonElement r)
    {
        int Value(string key,int min,int max,int fallback) {
            int value=N(r,key,fallback);
            if(value<min || value>max)throw new Exception($"Encounter criterion {key} must be between {min} and {max}.");
            return value;
        }
        var nature=(Nature)Value("nature",0,25,25);var gender=(Gender)Value("gender",0,2,2);
        var ability=(AbilityPermission)Value("ability",-1,4,-1);
        if(!Enum.IsDefined(ability))throw new Exception("Choose a valid ability preference.");
        var shiny=(Shiny)Value("shiny",0,4,0);
        if(!Enum.IsDefined(shiny))throw new Exception("Choose a valid shiny preference.");
        int[] ivs=r.TryGetProperty("ivs",out var list)?list.EnumerateArray().Select(x=>x.GetInt32()).ToArray():[-1,-1,-1,-1,-1,-1];
        if(ivs.Length!=6 || ivs.Any(x=>x< -1 || x>31))throw new Exception("Use six IV preferences between −1 (any) and 31.");
        byte min=(byte)Value("levelMin",0,100,0),max=(byte)Value("levelMax",0,100,0);
        if(min>max)throw new Exception("Use 0/0 for any level, or an ordered range from 0 to 100. A minimum of 0 has no lower restriction.");
        int mutations=Value("mutations",0,143,0);
        if((mutations & ~143)!=0)throw new Exception("Unknown encounter mutation flags.");
        return new EncounterCriteria {Nature=nature,Gender=gender,Ability=ability,Shiny=shiny,
            IV_HP=(sbyte)ivs[0],IV_ATK=(sbyte)ivs[1],IV_DEF=(sbyte)ivs[2],IV_SPA=(sbyte)ivs[3],IV_SPD=(sbyte)ivs[4],IV_SPE=(sbyte)ivs[5],
            LevelMin=min,LevelMax=max,HiddenPowerType=(sbyte)Value("hiddenPower",-1,15,-1),Form=(sbyte)Value("form",-1,127,-1),Mutations=(EncounterMutation)mutations};
    }
}
