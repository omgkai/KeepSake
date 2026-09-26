using PKHeX.Core;

sealed partial class EditorSession {
    ExtraPage ReadContacts(ExtraTool tool) {
        var entries=((SAV4HGSS)RequireSave()).GetPokeGearRoloDex().ToArray();
        var choices=Enum.GetValues<PokegearNumber>().Select(x=>new Choice(((sbyte)x).ToString(),x==PokegearNumber.None?"Empty":Label(x.ToString().Replace('_',' ')))).ToArray();
        return new("contacts",revision,tool,entries.Select((x,i)=>ER(i.ToString(),$"Contact {i+1} · "+(x==PokegearNumber.None?"Empty":Label(x.ToString().Replace('_',' '))),[EV("Caller","Caller",(sbyte)x,choices:choices)])).ToArray(),[new("give","Add All Contacts"),new("nontrainers","Add Non-Trainer Contacts"),new("clear","Clear All Contacts")]);
    }
    void EditContacts(string id,string mode,Dictionary<string,string> edits) {
        var s=(SAV4HGSS)RequireSave();
        if(mode=="give")s.PokeGearUnlockAllCallers();else if(mode=="nontrainers")s.PokeGearUnlockAllCallersNoTrainers();else if(mode=="clear")s.PokeGearClearAllCallers();
        else if(edits.TryGetValue("Caller",out var caller)){var list=s.GetPokeGearRoloDex();list[int.Parse(id)]=(PokegearNumber)sbyte.Parse(caller);}
    }
    ExtraPage ReadUnderground8(ExtraTool tool) {
        var names=Util.GetStringList("ug_item","en");
        var rows=((SAV8BS)RequireSave()).Underground.ReadItems().Where(x=>x.Type!=UgItemType.None).Select(x=>ER(x.Index.ToString(),x.Index<names.Length?names[x.Index]:$"Item {x.Index}",[EV("Count","Owned",x.Count,x.MaxValue),EV("New","Show as new",!x.HideNewFlag),EV("Favorite","Favorite",x.IsFavoriteFlag)],Label(x.Type.ToString()))).ToArray();
        return new("underground8",revision,tool,rows,[new("give","Give All"),new("clear","Empty All")]);
    }
    void EditUnderground8(string id,string mode,Dictionary<string,string> edits) {
        var pocket=((SAV8BS)RequireSave()).Underground;var items=pocket.ReadItems();
        if(mode=="edit") {var item=items.Single(x=>x.Index.ToString()==id);foreach(var (field,value) in edits)switch(field){case "Count":item.Count=int.Parse(value);break;case "New":item.HideNewFlag=!bool.Parse(value);break;case "Favorite":item.IsFavoriteFlag=bool.Parse(value);break;}}
        else foreach(var item in items.Where(x=>x.Type!=UgItemType.None)){item.Count=mode=="give"?item.MaxValue:0;if(mode=="clear"){item.HideNewFlag=false;item.IsFavoriteFlag=false;}}
        pocket.WriteItems(items);
    }
}
