using System.Text.Json;
using PKHeX.Core;
sealed partial class EditorSession {
    record TreatRow(int id,string name,Field[] fields);
    Choice[] TreatCollections() {
        var list=new List<Choice>();
        if(save is SAV3 {LargeBlock:ISaveBlock3LargeHoenn})list.Add(new("blocks3","Pokéblocks"));
        if(save is SAV4Sinnoh)list.Add(new("poffins4","Poffins"));
        if(save is SAV6AO)list.Add(new("blocks6","Pokéblocks"));
        if(save is ISaveBlock6Main)list.Add(new("puffs","Poké Puffs"));
        if(save is SAV7SM or SAV7USUM)list.Add(new("beans","Poké Beans"));
        if(save is SAV8BS)list.Add(new("poffins8","Poffins"));
        return list.ToArray();
    }
    static Field TreatValue(string id,string label,int value,Choice[]? choices=null)=>new(id,label,"Treat",value.ToString(),choices==null?"number":"enum",true,"",null,choices??[]);
    object Treats(JsonElement r) {
        var choices=TreatCollections();string kind=S(r,"kind");if(string.IsNullOrEmpty(kind)||!choices.Any(c=>c.value==kind))kind=choices.FirstOrDefault()?.value??"";var rows=new List<TreatRow>();
        switch(kind) {
            case "blocks3" when save is SAV3 {LargeBlock:ISaveBlock3LargeHoenn h}:var blocks=h.PokeBlocks.Blocks;var names=Util.GetStringList("pokeblock3","en");for(int i=0;i<blocks.Length;i++)rows.Add(new(i,MoveLabel(names,(int)blocks[i].Color),Fields(blocks[i],"Pokéblock").Where(f=>f.editable).ToArray()));break;
            case "poffins4" when save is SAV4Sinnoh s:var poffins=new PoffinCase4(s).Poffins;var labels=Util.GetStringList("poffin4","en");for(int i=0;i<poffins.Length;i++)rows.Add(new(i,MoveLabel(labels,(int)poffins[i].Type),Fields(poffins[i],"Poffin").Where(f=>f.editable).ToArray()));break;
            case "blocks6" when save is SAV6AO s:for(int i=0;i<12;i++)rows.Add(new(i,strings.pokeblocks[94+i],[TreatValue("Count","Count",checked((int)s.Contest.GetBlockCount(i)))]));break;
            case "puffs" when save is ISaveBlock6Main s:var puffs=s.Puff.GetPuffs();var puffChoices=strings.puffs.Take(27).Select((n,i)=>new Choice(i.ToString(),n)).ToArray();for(int i=0;i<puffs.Length;i++)rows.Add(new(i,MoveLabel(strings.puffs,puffs[i]),[TreatValue("Type","Type",puffs[i],puffChoices)]));break;
            case "beans" when save is SAV7 s:var beans=s.ResortSave.GetBeans();var beanNames=ResortSave7.GetBeanIndexNames();for(int i=0;i<beans.Length;i++)rows.Add(new(i,beanNames[i],[TreatValue("Count","Count",beans[i])]));break;
            case "poffins8" when save is SAV8BS s:
                var pn=Util.GetStringList("poffin8b","en");var pc=pn.Select((n,i)=>new Choice(((byte)(i-1)).ToString(),i==0?"None":n)).ToArray();
                for(int i=0;i<PoffinSaveData8b.COUNT_POFFIN;i++){var p=s.Poffins.GetPoffin(i);rows.Add(new(i,p.IsNull?"None":MoveLabel(pn,p.MstID+1),Fields(p,"Poffin").Where(f=>f.editable).Select(f=>f.id=="MstID"?f with{label="Type",kind="enum",choices=pc}:f.id=="Taste"?f with{label="Smoothness"}:f).ToArray()));}break;
        }
        return new {supported=choices.Length>0,revision,kind,collections=choices,entries=rows,cookingCount=save is SAV8BS bs ? (int?)bs.Poffins.CookingCount : null};
    }
    void EditTreats(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Refresh the treat case.");
        string kind=S(r,"kind"),mode=S(r,"mode","edit");if(!TreatCollections().Any(c=>c.value==kind))throw new Exception("This treat case is unavailable for this game.");
        int index=N(r,"id");var sav=RequireSave();
        void Edit(object target) {foreach(var e in r.GetProperty("edits").EnumerateArray()){var id=S(e,"field");if(!Fields(target,"Treat").Any(f=>f.id==id&&f.editable))throw new Exception("Unknown treat field.");if(target is Poffin8b && id=="MstID") {var n=int.Parse(S(e,"value"));if(n!=255&&(n<0||n>=Util.GetStringList("poffin8b","en").Length-1))throw new Exception("Choose a listed Poffin type.");}SetProperty(target,id,S(e,"value"));}}
        int Value(int max,string field) {var edits=r.GetProperty("edits").EnumerateArray().ToArray();if(edits.Length!=1||S(edits[0],"field")!=field)throw new Exception("Choose the case value.");var n=int.Parse(S(edits[0],"value"));if(n<0||n>max)throw new Exception($"Use a value from 0 to {max}.");return n;}
        void Bounds(int count){if(index<0||index>=count)throw new Exception("Invalid treat slot.");}
        if(mode=="cooking") { if(kind!="poffins8" || sav is not SAV8BS cooking)throw new Exception("Cooking records are unavailable for this game.");cooking.Poffins.CookingCount=Value(int.MaxValue,"CookingCount");dirty=true;return; }
        if(mode is not ("edit" or "give" or "clear" or "sort" or "best"))throw new Exception("Unknown treat action.");
        switch(kind) {
            case "blocks3":var h=(ISaveBlock3LargeHoenn)((SAV3)sav).LargeBlock;var c=h.PokeBlocks;if(mode=="give")c.MaximizeAll(true);else if(mode=="clear")c.DeleteAll();else if(mode=="edit"){Bounds(c.Blocks.Length);Edit(c.Blocks[index]);}else throw new Exception("Unsupported Pokéblock action.");h.PokeBlocks=c;break;
            case "poffins4":var pc=new PoffinCase4((SAV4Sinnoh)sav);if(mode=="give")pc.FillCase();else if(mode=="clear")pc.DeleteAll();else if(mode=="edit"){Bounds(pc.Poffins.Length);Edit(pc.Poffins[index]);}else throw new Exception("Unsupported Poffin action.");pc.Save();break;
            case "blocks6":var ao=(SAV6AO)sav;if(mode is "give" or "clear")for(int i=0;i<12;i++)ao.Contest.SetBlockCount(i,(uint)(mode=="give"?999:0));else if(mode=="edit"){Bounds(12);ao.Contest.SetBlockCount(index,(uint)Value(999,"Count"));}else throw new Exception("Unsupported Pokéblock action.");break;
            case "puffs":var puff=((ISaveBlock6Main)sav).Puff;if(mode=="give"||mode=="best")puff.MaxCheat(mode=="best");else if(mode=="clear"){puff.GetPuffs().Clear();puff.PuffCount=0;}else if(mode=="sort")puff.Sort();else {Bounds(puff.GetPuffs().Length);puff.GetPuffs()[index]=(byte)Value(26,"Type");puff.PuffCount=puff.GetPuffs().ToArray().Count(b=>b!=0);}break;
            case "beans":var resort=((SAV7)sav).ResortSave;if(mode=="give")resort.FillBeans();else if(mode=="clear")resort.ClearBeans();else if(mode=="edit"){Bounds(resort.GetBeans().Length);resort.GetBeans()[index]=(byte)Value(255,"Count");}else throw new Exception("Unsupported Bean action.");break;
            case "poffins8":var s=(SAV8BS)sav;if(mode is "give" or "clear"){for(int i=0;i<PoffinSaveData8b.COUNT_POFFIN;i++){var p=s.Poffins.GetPoffin(i);if(mode=="clear")p.ToNull();else{p.MstID=0x1C;p.Level=60;p.Taste=255;p.FlavorSpicy=p.FlavorDry=p.FlavorSweet=p.FlavorBitter=p.FlavorSour=255;}s.Poffins.SetPoffin(i,p);}}else if(mode=="edit"){Bounds(PoffinSaveData8b.COUNT_POFFIN);var p=s.Poffins.GetPoffin(index);Edit(p);s.Poffins.SetPoffin(index,p);}else throw new Exception("Unsupported Poffin action.");break;
        }dirty=true;
    }
}
