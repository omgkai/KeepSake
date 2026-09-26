using PKHeX.Core;
using System.Text.Json;
sealed partial class EditorSession {
    Choice[] DonutFlavorPool()=>DonutInfo.Flavors.Select((x,i)=>(x,i)).Where(v=>v.x.Name.Length>=8 && int.TryParse(v.x.Name.AsSpan(6,2),out int type) && type is >=3 and <=21).Select(v=>new Choice(v.x.Hash.ToString(),MoveLabel(strings.donutFlavor,v.i))).ToArray();
    object DonutRangeInfo(){if(RequireSave() is not SAV9ZA)throw new Exception("Donut generation requires Legends: Z-A.");return new {revision,count=DonutPocket9a.MaxCount,flavors=DonutFlavorPool()};}
    void GenerateDonutRange(JsonElement r) {
        var sav=RequireSave() as SAV9ZA ?? throw new Exception("Donut generation requires Legends: Z-A.");
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Reopen the generator.");
        int start=N(r,"start",-1),end=N(r,"end",-1);if(start<0 || start>=end || end>DonutPocket9a.MaxCount)throw new Exception("Choose a non-empty range of donut slots.");
        var allowed=DonutFlavorPool().Select(x=>ulong.Parse(x.value)).ToHashSet();var flavors=r.GetProperty("flavors").EnumerateArray().Select(x=>ulong.Parse(x.GetString()!)).Distinct().ToArray();
        if(flavors.Length==0 || flavors.Any(x=>!allowed.Contains(x)))throw new Exception("Choose at least one supported flavor.");
        sav.Donuts.SetRandomShinyTemplateRange(flavors,start,end);dirty=true;
    }
}
