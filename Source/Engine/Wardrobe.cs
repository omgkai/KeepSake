using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    record WardrobeItem(string id,string category,string name,bool owned,bool editable);
    record WardrobeSection(string name,uint key,string resource);
    static readonly WardrobeSection[] HisuiWardrobe=[new("Hats",0x3ADB8A98,"hats"),new("Tops",0x82D57F17,"tops"),new("Bottoms",0x11B37EC9,"bottoms"),new("Outfits",0x45851092,"uniforms"),new("Shoes",0x636A5ABD,"shoes"),new("Glasses",0x58AB6233,"glasses")];
    static readonly WardrobeSection[] PaldeaWardrobe=[new("Glasses",SaveBlockAccessor9SV.KFashionUnlockedEyewear,""),new("Gloves",SaveBlockAccessor9SV.KFashionUnlockedGloves,""),new("Bags",SaveBlockAccessor9SV.KFashionUnlockedBag,""),new("Shoes",SaveBlockAccessor9SV.KFashionUnlockedFootwear,""),new("Hats",SaveBlockAccessor9SV.KFashionUnlockedHeadwear,""),new("Legwear",SaveBlockAccessor9SV.KFashionUnlockedLegwear,""),new("Outfits",SaveBlockAccessor9SV.KFashionUnlockedClothing,""),new("Phone cases",SaveBlockAccessor9SV.KFashionUnlockedPhoneCase,"")];
    static readonly WardrobeSection[] LumioseWardrobe=[new("Tops",SaveBlockAccessor9ZA.KFashionTops,""),new("Bottoms",SaveBlockAccessor9ZA.KFashionBottoms,""),new("Outfits",SaveBlockAccessor9ZA.KFashionAllInOne,""),new("Hats",SaveBlockAccessor9ZA.KFashionHeadwear,""),new("Glasses",SaveBlockAccessor9ZA.KFashionEyewear,""),new("Gloves",SaveBlockAccessor9ZA.KFashionGloves,""),new("Legwear",SaveBlockAccessor9ZA.KFashionLegwear,""),new("Shoes",SaveBlockAccessor9ZA.KFashionFootwear,""),new("Bags",SaveBlockAccessor9ZA.KFashionSatchels,""),new("Earrings",SaveBlockAccessor9ZA.KFashionEarrings,"")];
    static readonly string[] GalarCategories=["Glasses","Hats","Jackets","Tops","Bags","Gloves","Bottoms","Legwear","Shoes"];
    static readonly string[] GalarResources=["glasses","hats","jackets","tops","bags","gloves","bottoms","socks","shoes"];
    static readonly string[] SinnohOutfits=["Everyday style","Pikachu hoodie","Platinum style","Overalls","Eevee jacket","Gengar jacket","Cyber style","Summer style","Winter style","Spring style","Casual style","Leather jacket"];
    static readonly Dictionary<string,string> PaldeaNames=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fashion","sv_names.json")))!;
    static string[] ClothingNames(string resource)=>File.ReadAllLines(Path.Combine(AppContext.BaseDirectory,"Fashion",resource+"_en.txt"));
    static bool NamedClothing(string name)=>!string.IsNullOrWhiteSpace(name)&&!name.Contains("Unused",StringComparison.OrdinalIgnoreCase);
    static byte[] AlolaCatalog(SAV7 s)=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Fashion",$"fashion_{(s.Gender==0?"m":"f")}_{(s is SAV7USUM?"uu":"sm")}"));
    List<WardrobeItem> WardrobeItems(SaveFile sav) {
        var rows=new List<WardrobeItem>();
        switch(sav) {
            case SAV8LA s:
                foreach(var c in HisuiWardrobe) {
                    var labels=ClothingNames("la_"+c.resource+(c.resource=="hats"?(s.Gender==0?"_male":"_female"):""));var data=s.Blocks.GetBlock(c.key).Data;
                    for(int i=0;i<Math.Min(labels.Length,data.Length);i++)if(NamedClothing(labels[i]))rows.Add(new($"{c.key:X8}:{i}",c.name,labels[i],data[i]==2,true));
                }break;
            case SAV8SWSH s:
                var legal=(SAV8SWSH)s.Clone();legal.Fashion.UnlockAllLegal();
                for(int r=6;r<=14;r++) {
                    var owned=s.Fashion.GetArrayOwnedFlag(r);var allowed=legal.Fashion.GetArrayOwnedFlag(r);var labels=ClothingNames("swsh_"+GalarResources[r-6]+(s.MyStatus.GenderAppearance==0?"_male":"_female"));
                    for(int i=0;i<Math.Min(labels.Length,owned.Length);i++)if(NamedClothing(labels[i]))rows.Add(new($"{r}:{i}",GalarCategories[r-6],labels[i],owned[i],allowed[i]));
                }break;
            case SAV8BS s:
                for(int i=0;i<SinnohOutfits.Length;i++)rows.Add(new((1246+i).ToString(),"Outfits",SinnohOutfits[i],s.FlagWork.GetFlag(1246+i),true));break;
            case SAV9SV s:
                var full=(SAV9SV)s.Clone();PlayerFashionUnlock9.UnlockBase(full.Blocks,full.Gender);
                foreach(var c in PaldeaWardrobe) {
                    var owned=FashionItem9.GetArray(s.Blocks.GetBlock(c.key).Data).Where(x=>x.Value!=FashionItem9.None && x.Value!=0).Select(x=>x.Value).ToHashSet();
                    var allowed=FashionItem9.GetArray(full.Blocks.GetBlock(c.key).Data).Where(x=>x.Value!=FashionItem9.None && x.Value!=0).Select(x=>x.Value).ToHashSet();
                    foreach(var id in owned.Union(allowed).Order())rows.Add(new($"{c.key:X8}:{id}",c.name,PaldeaNames.GetValueOrDefault(id.ToString(),$"{c.name} · item {id}"),owned.Contains(id),allowed.Contains(id)||owned.Contains(id)));
                }break;
            case SAV9ZA s:
                foreach(var c in LumioseWardrobe) {
                    var items=FashionItem9a.GetArray(s.Blocks.GetBlock(c.key).Data);
                    for(int i=0;i<items.Length;i++)if(items[i].Value!=FashionItem9a.None && items[i].Value!=0)rows.Add(new($"{c.key:X8}:{i}",c.name,$"{c.name} · item {items[i].Value}",items[i].IsOwned,true));
                }break;
            case SAV7 s when s is SAV7SM or SAV7USUM:
                var catalog=AlolaCatalog(s);var raw=s.Fashion.Data;
                for(int i=0;i<Math.Min(catalog.Length,raw.Length);i++)if((catalog[i]&1)!=0)rows.Add(new(i.ToString(),"Clothing",$"Wardrobe item {i+1}",(raw[i]&1)!=0,true));break;
        }
        return rows;
    }
    void SetWardrobeItem(WardrobeItem row,bool owned) {
        var parts=row.id.Split(':');int index=int.Parse(parts[^1]);
        switch(RequireSave()) {
            case SAV8LA s:s.Blocks.GetBlock(Convert.ToUInt32(parts[0],16)).Data[index]=(byte)(owned?2:1);break;
            case SAV8SWSH s:
                int region=int.Parse(parts[0]);var flags=s.Fashion.GetArrayOwnedFlag(region);flags[index]=owned;s.Fashion.SetArrayOwnedFlag(region,flags);break;
            case SAV8BS s:s.FlagWork.SetFlag(index,owned);break;
            case SAV9SV s:
                var block=s.Blocks.GetBlock(Convert.ToUInt32(parts[0],16));var entries=FashionItem9.GetArray(block.Data);int found=Array.FindIndex(entries,x=>x.Value==(uint)index);
                if(owned && found<0) {found=Array.FindIndex(entries,x=>x.Value==FashionItem9.None);if(found<0)throw new Exception("This clothing category is full. Nothing was changed.");entries[found]=new FashionItem9{Value=(uint)index,IsNew=true};}
                else if(!owned && found>=0)entries[found].Clear();
                FashionItem9.SetArray(entries,block.Data);break;
            case SAV9ZA s:
                var b=s.Blocks.GetBlock(Convert.ToUInt32(parts[0],16));var item=FashionItem9a.Read(b.Data.Slice(index*8,8));item.IsOwned=owned;item.Write(b.Data.Slice(index*8,8));break;
            case SAV7 s when s is SAV7SM or SAV7USUM:s.Fashion.Data[index]=(byte)(owned?s.Fashion.Data[index]|1:s.Fashion.Data[index]&~1);break;
            default:throw new Exception("Individual clothing editing is unavailable in this game.");
        }
    }
    void ChangeWardrobe(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Refresh the wardrobe before editing it.");
        var rows=WardrobeItems(RequireSave());var mode=S(r,"mode","item");
        if(mode=="item") {var row=rows.FirstOrDefault(x=>x.id==S(r,"id")&&x.editable)??throw new Exception("Choose an available clothing item.");SetWardrobeItem(row,B(r,"owned"));}
        else if(mode=="give") {
            var category=S(r,"category");var chosen=rows.Where(x=>x.editable&&(category==""||x.category==category)).ToArray();
            if(chosen.Length==0)throw new Exception("This category has no available clothing items.");
            foreach(var row in chosen.Where(x=>!x.owned))SetWardrobeItem(row,true);
        } else throw new Exception("Unknown wardrobe action.");
        dirty=true;
    }
}
