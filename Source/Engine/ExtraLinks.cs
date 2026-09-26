using PKHeX.Core;
using System.Text.Json;

sealed partial class EditorSession {
    ExtraPage ReadLink6(ExtraTool tool) {
        var g=((ISaveBlock6Main)RequireSave()).Link.Gifts;
        var rows=new List<ExtraRow>{ER("settings","Pokémon Link delivery",[ET("Origin","Delivery source",g.Origin,54),EP(g,"Enabled","Ready to receive"),EP(g,"BattlePoints","Battle Points",9999),EP(g,"Pokemiles","Poké Miles",65535)],"Review the rewards waiting to be received in your game") with {fileExtension="pl6"}};
        var items=Enumerable.Range(0,save!.MaxItemID+1).Select(i=>new Choice(i.ToString(),strings.itemlist[i])).ToArray();
        for(int i=1;i<=6;i++) {
            ushort item=(ushort)g.GetType().GetProperty("Item"+i)!.GetValue(g)!;
            rows.Add(ER("item:"+i,$"Item reward {i}",[EP(g,"Item"+i,"Item",choices:items),EP(g,"Quantity"+i,"Quantity",65535)],item<strings.itemlist.Length?strings.itemlist[item]:$"Item {item}","bitem_"+item));
            var pk=(LinkEntity6)g.GetType().GetProperty("Entity"+i)!.GetValue(g)!;
            rows.Add(ER("pokemon:"+i,$"Pokémon reward {i} · {Species(pk.Species)}",[],"Pokémon templates are included in the .pl6 delivery file",SpriteFor(pk.Species,pk.Form,(byte)pk.Gender,0,save.Context)));
        }
        return new(tool.id,revision,tool,rows.ToArray(),[],true);
    }
    void EditLink6(JsonElement r,string id,string mode,Dictionary<string,string> edits) {
        var link=((ISaveBlock6Main)RequireSave()).Link;
        if(mode=="import") {if(id!="settings")throw new Exception("Choose the Pokémon Link delivery to import.");ReadExtraBytes(S(r,"path"),PL6.Size).CopyTo(link.Gifts.Data);}
        else SetExtraProperties(link.Gifts,edits);
        link.RefreshChecksum();
    }
    ExtraPage ReadGlobalLink(ExtraTool tool) {
        var g=((SAV5)RequireSave()).GlobalLink;var date=g.UploadDate;
        var rows=new List<ExtraRow>{ER("settings","Game Sync records",[
            new("Date","Last upload date",date.IsValid?date.ToDateOnly().ToString("yyyy-MM-dd"):"","date","0","0",[]),
            EP(g,"UploadCount","Upload count",int.MaxValue,-1),EP(g,"UploadStatus","Upload status",255),EP(g,"IsSlotPresent","Pokémon slot present"),EP(g,"IsRegistered","Game registered"),EP(g,"IsAccountFullAccess","Full account access"),
            EP(g,"Musical","Musical download ID",255),EP(g,"CGearSkin","C-Gear download ID",255),EP(g,"DexSkin","Pokédex download ID",255),
            EP(g,"SelectedFurnitureIndex","Selected furniture",choices:Enumerable.Range(0,5).Select(i=>new Choice(i.ToString(),$"Furniture {i+1}")).Append(new("127","None")).ToArray()),EP(g,"IsFurnitureSynchronized","Furniture synchronized")],"Local saved records; this does not reconnect the retired online service")};
        var items=Enumerable.Range(0,save!.MaxItemID+1).Select(i=>new Choice(i.ToString(),strings.itemlist[i])).ToArray();
        for(int i=0;i<GlobalLink5.CountItems;i++){ushort item=g.GetItem(i);rows.Add(ER("item:"+i,$"Item {i+1}",[EV("Item","Item",item,choices:items),EV("Quantity","Quantity",g.GetItemQuantity(i),255)],item<strings.itemlist.Length?strings.itemlist[item]:$"Item {item}","bitem_"+item));}
        for(int i=0;i<GlobalLink5.CountFurniture;i++){var f=g.GetFurniture(i);rows.Add(ER("furniture:"+i,$"Furniture {i+1}",[EV("Value","Furniture ID",f.Value,65535),ET("Name","Name",f.Name,12)],f.Name));}
        return new(tool.id,revision,tool,rows.ToArray(),[]);
    }
    void EditGlobalLink(string id,Dictionary<string,string> edits) {
        var g=((SAV5)RequireSave()).GlobalLink;
        if(id=="settings"){if(edits.Remove("Date",out var text)){var d=g.UploadDate;if(text=="")d.SetEmpty();else d.FromDateOnly(DateOnly.ParseExact(text,"yyyy-MM-dd"));}SetExtraProperties(g,edits);return;}
        int i=int.Parse(id.Split(':')[1]);
        if(id.StartsWith("item:")){if(edits.TryGetValue("Item",out var item))g.SetItem(i,ushort.Parse(item));if(edits.TryGetValue("Quantity",out var count))g.SetItemQuantity(i,byte.Parse(count));}
        else SetExtraProperties(g.GetFurniture(i),edits);
    }
}
