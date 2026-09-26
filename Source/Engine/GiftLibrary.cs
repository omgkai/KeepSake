using PKHeX.Core;
using System.Security.Cryptography;
using System.Text.Json;

sealed partial class EditorSession
{
    sealed record LocalGift(DataMysteryGift Gift,string Path,string Hash);
    readonly Dictionary<int,LocalGift> localGifts=new();
    readonly HashSet<string> giftSourcePaths=new(StringComparer.OrdinalIgnoreCase);
    int nextGiftID=-1;
    static readonly HashSet<string> giftExtensions=new(StringComparer.OrdinalIgnoreCase){".pgt",".pcd",".wc4",".pgf",".wc5full",".wc6",".wc6full",".wc7",".wc7full",".wr7",".wb7",".wb7full",".wc8",".wc8full",".wb8",".wa8",".wc9",".wa9"};
    object GiftRow(MysteryGift g,int id,string source="Bundled",string file="")
    {
        var languages=GiftLanguages(g);byte gender=(byte)Math.Clamp(GiftInt(g,"Gender",0),0,2);
        bool shiny=g.IsShiny || g.Shiny.IsShiny();
        var rule=g.Shiny==Shiny.FixedValue ? (shiny ? "Always":"Never") : g.Shiny.IsShiny() ? "Always":g.Shiny.ToString();
        return new {id,card=g.CardID,title=g.CardTitle,species=(int)g.Species,name=g.IsEntity ? Species(g.Species):"Item gift",generation=(int)g.Generation,game=g.Context.ToString(),level=(int)g.Level,entity=g.IsEntity,extension=g.Extension,exportable=g is DataMysteryGift,
            languages,languageKnown=languages.Length>0,origin=GameInfo.GetVersionName(g.Version),originVersions=GameUtil.GameVersions.Where(v=>g.Version.Contains(v)).Select(v=>v.ToString()).ToArray(),
            shinyRule=g.IsEntity ? rule:"Not applicable",egg=g.IsEgg,heldItem=g.IsEntity && g.HeldItem>0 ? MoveLabel(strings.GetItemStrings(g.Context,g.Version),g.HeldItem):"None",moves=g.IsEntity ? new[]{(int)g.Moves.Move1,(int)g.Moves.Move2,(int)g.Moves.Move3,(int)g.Moves.Move4}.Where(x=>x>0).ToArray():Array.Empty<int>(),
            trainer=g.GetType().GetProperty("OriginalTrainerName")?.GetValue(g)?.ToString() ?? "",source,file,
            sprite=g.IsEntity ? SpriteFor(g.Species,g.Form,gender,0,g.Context,shiny):"b_0",portrait=g.IsEntity ? $"{g.Context}:{g.Species}:{g.Form}:{gender}:0:{(shiny?1:0)}":""};
    }
    static int GiftInt(MysteryGift gift,string property,int fallback=-1)
    {
        var value=gift.GetType().GetProperty(property)?.GetValue(gift);
        return value is null ? fallback:Convert.ToInt32(value);
    }
    static int[] GiftLanguages(MysteryGift g)
    {
        if(!g.IsEntity)return [];
        var available=Language.GetAvailableGameLanguages(g.Context).ToArray().Select(x=>(int)x).ToArray();
        Func<int,bool>? allows=g switch {WC8 x=>x.CanHaveLanguage,WC9 x=>x.CanHaveLanguage,WA8 x=>x.CanHaveLanguage,WA9 x=>x.CanHaveLanguage,WB8 x=>x.CanHaveLanguage,WB7 x=>x.CanHaveLanguage,_=>null};
        if(allows!=null)return available.Where(allows).ToArray();
        int fixedLanguage=g switch {PCD x=>x.Gift.PK.Language,PGT x=>x.PK.Language,_=>GiftInt(g,"Language")};
        if(fixedLanguage>0)return [fixedLanguage];
        int restricted=GiftInt(g,"RestrictLanguage",0);
        if(restricted>0)return [restricted];
        return fixedLanguage==0 ? available:[];
    }
    object LoadGiftFolder(JsonElement r)
    {
        var path=Path.GetFullPath(S(r,"path"));if(!Directory.Exists(path))throw new Exception("Choose a gift-card folder.");
        var options=new EnumerationOptions{RecurseSubdirectories=B(r,"recursive"),IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint|FileAttributes.Hidden|FileAttributes.System,MaxRecursionDepth=32};
        var hashes=localGifts.Values.Select(x=>x.Hash).ToHashSet();var additions=new List<LocalGift>();int skipped=0,duplicates=0,visited=0;bool truncated=false;
        foreach(var file in Directory.EnumerateFiles(path,"*",options))
        {
            if(++visited>20000 || localGifts.Count+additions.Count>=5000){truncated=true;break;}
            if(!giftExtensions.Contains(Path.GetExtension(file)))continue;
            try {
                var info=new FileInfo(file);if(info.Length==0 || info.Length>1024*1024){skipped++;continue;}
                var bytes=File.ReadAllBytes(file);var gift=MysteryGift.GetMysteryGift(bytes,info.Extension);
                if(gift==null || (!gift.IsEntity && !gift.IsItem) || (gift.IsEntity && (gift.Species==0 || gift.Species>1025))){skipped++;continue;}
                _=GiftRow(gift,0);giftSourcePaths.Add(Path.GetFullPath(file)); // Reject malformed metadata before adding anything to the live index.
                var hash=info.Extension.ToLowerInvariant()+":"+Convert.ToHexString(SHA256.HashData(bytes));if(!hashes.Add(hash)){duplicates++;continue;}
                additions.Add(new(gift,file,hash));
            }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or IndexOutOfRangeException or InvalidOperationException){skipped++;}
        }
        if(nextGiftID<0)nextGiftID=giftDatabase.Value.Length;
        foreach(var gift in additions)localGifts.Add(nextGiftID++,gift);
        return new {added=additions.Count,skipped,duplicates,truncated,entries=Gifts()};
    }
    object ClearGiftFolders(){localGifts.Clear();giftSourcePaths.Clear();return Gifts();}
}
