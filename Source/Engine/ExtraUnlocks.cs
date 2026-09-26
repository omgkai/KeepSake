using PKHeX.Core;

sealed partial class EditorSession
{
    ExtraPage ReadUnlocks8b(ExtraTool tool) {
        var s=(SAV8BS)RequireSave();var u=new EventUnlocker8b(s);var rows=new List<ExtraRow>();
        void Add(string id,string name,bool ready,string description,string action)=>rows.Add(ER(id,name,[],description,actions:ready?[new(id,action)]:[]) with {category="Encounters"});
        Add("spiritomb","Spiritomb",u.UnlockReadySpiritomb,"Prepare the Hallowed Tower encounter.","Prepare Encounter");
        Add("darkrai","Darkrai",u.UnlockReadyDarkrai,"Enable the Member Card event and Newmoon Island encounter.","Unlock Event");
        Add("shaymin","Shaymin",u.UnlockReadyShaymin,"Enable Oak’s Letter and the Flower Paradise encounter.","Unlock Event");
        Add("arceus","Arceus",u.UnlockReadyArceus,"Prepare the Azure Flute and Hall of Origin encounter.","Unlock Event");
        Add("legend",s.Version==GameVersion.BD?"Dialga":"Palkia",u.UnlockReadyBoxLegend,"Reset the Spear Pillar legendary encounter.","Restore Encounter");
        Add("roamers","Mesprit & Cresselia",u.ResetReadyRoamerMesprit||u.ResetReadyRoamerCresselia,"Restore the roaming encounters using PKHeX’s reset flags.","Restore Roamers");
        rows.Add(ER("zones","Map destinations",[],"Unlock the original game’s zone flags.",actions:[new("zones","Unlock Destinations")]) with {category="Adventure"});
        rows.Add(ER("trainers","Route trainers",[],"Change all trainer battle-completion flags together.",actions:[..s.BattleTrainer.AnyDefeated?new[]{new Choice("rebattle","Make All Trainers Rechallengeable")}:[],..s.BattleTrainer.AnyUndefeated?new[]{new Choice("defeat","Mark All Trainers Defeated")}:[]]) with {category="Adventure"});
        rows.Add(ER("fashion","Wardrobe",[],"Unlock all outfit flags. Individual outfits are also available in Fashion.",actions:[new("fashion","Give All Outfits")]) with {category="Adventure"});
        return new("unlocks8b",revision,tool,rows.ToArray(),[]);
    }
    void EditUnlocks8b(string mode) {
        var s=(SAV8BS)RequireSave();var u=new EventUnlocker8b(s);
        switch(mode){case "spiritomb":u.UnlockSpiritomb();break;case "darkrai":u.UnlockDarkrai();break;case "shaymin":u.UnlockShaymin();break;case "arceus":u.UnlockArceus();break;case "legend":u.UnlockBoxLegend();break;case "roamers":u.RespawnRoamer();break;case "zones":u.UnlockZones();break;case "fashion":u.UnlockFashion();break;case "rebattle":s.BattleTrainer.RebattleAll();break;case "defeat":s.BattleTrainer.DefeatAll();break;default:throw new Exception("Choose an available unlock action.");}
    }
    ExtraPage ReadGear(ExtraTool tool) {
        var s=(SAV4BR)RequireSave();var names=GameLanguage.GetStrings("gear","en");var rows=new List<ExtraRow>();
        for(ModelBR model=ModelBR.YoungBoy;model<=ModelBR.LittleGirl;model++)for(GearCategory category=0;(int)category<GearUnlock.CategoryCount;category++) {
            var(offset,count)=GearUnlock.GetOffsetCount(model,category);
            for(int i=0;i<count;i++){int index=offset+i;bool shared=category==GearCategory.Badges&&i>0;rows.Add(ER("gear:"+index,index<names.Length?names[index]:$"Gear {index}",[EV("Owned","Unlocked",s.GearUnlock.Get(index))],(shared?"All character styles":Label(model.ToString()))+" · "+Label(category.ToString())) with {category=shared?"Shared badges":Label(model.ToString())});}
        }
        foreach(var name in new[]{"Groudon","Lucario","Electivire","Kyogre","Roserade","Pachirisu"})rows.Add(ER("special:"+name,"Shiny "+name+" outfit",[EP(s,"GearShiny"+name+"Outfit","Unlocked")]) with {category="Special outfits"});
        return new("gear",revision,tool,rows.DistinctBy(r=>r.id).ToArray(),[new("give","Give All Gear"),new("clear","Restore Default Gear")]);
    }
    void EditGear(string id,string mode,Dictionary<string,string> edits) {
        var s=(SAV4BR)RequireSave();if(mode=="give"){s.GearUnlock.UnlockAll();return;}if(mode=="clear"){s.GearUnlock.Clear();return;}
        if(id.StartsWith("special:")){SetExtraProperties(s,edits);return;}s.GearUnlock.Set(int.Parse(id[5..]),bool.Parse(edits["Owned"]));
    }
}
