using PKHeX.Core;

sealed partial class EditorSession
{
    static readonly (int flag,string name)[] FerryFlags3=[(0x864,"Southern Island encounter available"),(0x8B3,"Southern Island reachable"),(0x8D5,"Birth Island reachable"),(0x8D6,"Faraway Island reachable"),(0x8E0,"Navel Rock reachable"),(0x1D0,"Battle Frontier reachable"),(0x1AE,"First Southern Island visit"),(0x1AF,"First Birth Island visit"),(0x1B0,"First Faraway Island visit"),(0x1DB,"First Navel Rock visit")];
    static readonly string[] DecorationCategories3=["Desks","Chairs","Plants","Ornaments","Mats","Posters","Dolls","Cushions"];
    static Span<Decoration3> DecorationPouch3(DecorationInventory3 d,int i)=>i switch{0=>d.Desk,1=>d.Chair,2=>d.Plant,3=>d.Ornament,4=>d.Mat,5=>d.Poster,6=>d.Doll,7=>d.Cushion,_=>throw new ArgumentOutOfRangeException(nameof(i))};
    Choice[] SpeciesChoices(int max)=>Enumerable.Range(0,max+1).Select(i=>new Choice(i.ToString(),Species((ushort)i))).ToArray();
    ExtraPage ReadMisc3(ExtraTool tool)
    {
        var s=(SAV3)RequireSave();var rows=new List<ExtraRow>();var species=SpeciesChoices(386);
        void Add(string category,ExtraRow row)=>rows.Add(row with {category=category});
        Add("Adventure",ER("wallet","Game Corner coins",[EP(s,"Coin","Coins",9999)]));
        if(s is SAV3FRLG fr){var fields=new List<ExtraValue>{ET("RivalName","Rival name",fr.RivalName,s.Japanese?5:7)};for(int i=0;i<6;i++)fields.Add(EV("Member"+i,$"Trainer-card Pokémon {i+1}",SpeciesConverter.GetNational3(s.GetWork(0x43+i)),choices:species));Add("Adventure",ER("card","Rival & trainer card",fields.ToArray()));}
        if(s.SmallBlock is ISaveBlock3SmallExpansion j)Add("Joyful games",ER("joyful","Jump, berries & Berry Powder",[EP(j,"JoyfulJumpInRow","Consecutive jumps",9999),EP(j,"JoyfulJumpScore","Jump score",99990),EP(j,"JoyfulJump5InRow","Five-player jump streak",9999),EP(j,"JoyfulJumpGamesMaxPlayers","Five-player games",9999),EP(j,"JoyfulBerriesInRow","Consecutive berries",9999),EP(j,"JoyfulBerriesScore","Berry score",99990),EP(j,"JoyfulBerries5InRow","Five-player berry streak",9999),EP(j,"BerryPowder","Berry Powder",99999)]));
        foreach(var rec in Record3.GetItems(s)) {
            int i=rec.Value;uint value=s.GetRecord(i);var fields=new List<ExtraValue>{EV("Value","Stored record",value)};
            if(i==1)fields.AddRange([EV("Hours","Hours",value>>16,9999),EV("Minutes","Minutes",(value>>8)&255,59),EV("Seconds","Seconds",value&255,59)]);
            Add("Records",ER("record:"+i,rec.Text,fields.ToArray(),i==1?"First Hall of Fame time. Edit either the packed value or the individual time fields.":"Persistent adventure record."));
        }
        if(s is SAV3E e) {
            Add("Frontier",ER("points","Battle Points & Frontier Pass",[EP(e.SmallBlock,"BP","Battle Points",9999),EP(e.SmallBlock,"BPEarned","Total earned",65535),EV("Pass","Frontier Pass activated",e.GetEventFlag(BattleFrontier3.FrontierPassFlagIndex))]));
            foreach(var f in Enum.GetValues<BattleFrontierFacility3>()) {
                int a=(int)f;Add("Frontier",ER("symbol:"+a,Label(f.ToString())+" symbol",[EV("Silver","Silver symbol",s.GetEventFlag(BattleFrontier3.GetSymbolSilverFlagIndex(f))),EV("Gold","Gold symbol",s.GetEventFlag(BattleFrontier3.GetSymbolGoldFlagIndex(f)))]));
                for(int m=0;m<BattleFrontier3.GetModeCount(f);m++)for(int k=0;k<2;k++) {
                    var mode=(BattleFrontierBattleMode3)m;var record=(BattleFrontierRecordType3)k;var bf=e.SmallBlock.BattleFrontier;var fields=new List<ExtraValue>{EV("Continue","Continue challenge",bf.GetContinueFlag(f,mode,record))};
                    foreach(var stat in BattleFrontier3.GetValidStats(f))fields.Add(EV(stat.ToString(),Label(stat.ToString()),bf.GetStat(f,mode,record,stat),9999));
                    Add("Frontier",ER($"frontier:{a}:{m}:{k}",Label(f.ToString())+" · "+Label(mode.ToString()),fields.ToArray(),Label(record.ToString())));
                }
            }
            Add("Ferry",ER("ferry","Island destinations",FerryFlags3.Select(x=>EV(x.flag.ToString(),x.name,s.GetEventFlag(x.flag))).ToArray(),"Flags for the ferry and island events."));
            Add("Ferry",ER("tickets","Island tickets",[],"Add missing tickets to the Key Items pouch. The Old Sea Map was released only in Japan.",actions:[new("tickets","Give Released Tickets"),new("ticketsAll","Include Old Sea Map")]));
        }
        if(s.LargeBlock is ISaveBlock3LargeHoenn h) {
            Add("Adventure",ER("mirage","Mirage Island",[EV("Value","Island personality value",s.GetWork(0x24),65535)],"Match the first party Pokémon to make Mirage Island appear.",actions:s.PartyCount>0?[new("mirage","Match First Party Pokémon")]:[]));
            string[] labels=Util.GetStringList("decoration3","en");
            for(int c=0;c<8;c++) {
                var pouch=DecorationPouch3(h.Decorations,c);var choices=Enum.GetValues<Decoration3>().Where(d=>d==Decoration3.NONE||(int)d.GetCategory()==c).Select(d=>new Choice(((int)d).ToString(),(int)d<labels.Length?labels[(int)d]:Label(d.ToString()))).ToArray();
                var fields=new List<ExtraValue>();for(int i=0;i<pouch.Length;i++)fields.Add(EV(i.ToString(),$"Slot {i+1}",pouch[i],choices:choices));
                Add("Decorations",ER("decoration:"+c,DecorationCategories3[c],fields.ToArray(),$"{pouch.Length} slots · category-specific choices",actions:[new("giveDecorations","Give Every Type"),new("clearDecorations","Empty Category")]));
            }
            string[] contests=["Cool","Beauty","Cute","Smart","Tough"];
            for(int i=0;i<5;i++) {var p=h.GetPainting(i,s.Japanese);Add("Paintings",ER("painting:"+i,contests[i]+" contest painting",[EV("Enabled","Displayed in museum",s.GetEventFlag(Paintings3.GetFlagIndexContestStat(i))),EV("Species","Pokémon",p.Species,choices:species),EV("Caption","Caption",p.GetCaptionRelative(i),2),EP(p,"PID","Personality ID"),EP(p,"TID","Trainer ID",65535),EP(p,"SID","Secret ID",65535),ET("Nickname","Nickname",p.Nickname,s.Japanese?5:10),ET("OT","Original trainer",p.OT,s.Japanese?5:7)],p.IsShiny?"Shiny portrait":"Museum record",SpriteFor(p.Species,0,0,0,s.Context,p.IsShiny),[new("clearPainting","Clear Painting")]));}
        }
        return new("misc3",revision,tool,rows.ToArray(),[]);
    }
    void EditMisc3(string id,string mode,Dictionary<string,string> edits)
    {
        var s=(SAV3)RequireSave();var p=id.Split(':');int index=p.Length>1?int.Parse(p[1]):-1;
        if(id=="wallet"){SetExtraProperties(s,edits);return;}
        if(id=="card"){var fr=(SAV3FRLG)s;foreach(var(k,v) in edits)if(k=="RivalName")fr.RivalName=v;else s.SetWork(0x43+int.Parse(k[6..]),SpeciesConverter.GetInternal3(ushort.Parse(v)));return;}
        if(id=="joyful"){SetExtraProperties((ISaveBlock3SmallExpansion)s.SmallBlock,edits);return;}
        if(p[0]=="record") {if(edits.ContainsKey("Value")&&edits.Count>1)throw new Exception("Edit either the packed record or the time fields, not both.");uint value=s.GetRecord(index);if(edits.TryGetValue("Value",out var raw))value=uint.Parse(raw);else{if(edits.TryGetValue("Hours",out var h))value=(value&0xFFFF)|(uint.Parse(h)<<16);if(edits.TryGetValue("Minutes",out var m))value=(value&0xFFFF00FF)|(uint.Parse(m)<<8);if(edits.TryGetValue("Seconds",out var sec))value=(value&0xFFFFFF00)|uint.Parse(sec);}s.SetRecord(index,value);return;}
        if(id=="points"){var e=(SAV3E)s;if(edits.Remove("Pass",out var flag))s.SetEventFlag(BattleFrontier3.FrontierPassFlagIndex,bool.Parse(flag));SetExtraProperties(e.SmallBlock,edits);return;}
        if(p[0]=="symbol"){var facility=(BattleFrontierFacility3)index;foreach(var(k,v) in edits)s.SetEventFlag(k=="Silver"?BattleFrontier3.GetSymbolSilverFlagIndex(facility):BattleFrontier3.GetSymbolGoldFlagIndex(facility),bool.Parse(v));return;}
        if(p[0]=="frontier") {var bf=((SAV3E)s).SmallBlock.BattleFrontier;var facility=(BattleFrontierFacility3)index;var battle=(BattleFrontierBattleMode3)int.Parse(p[2]);var record=(BattleFrontierRecordType3)int.Parse(p[3]);foreach(var(k,v) in edits)if(k=="Continue")bf.SetContinueFlag(facility,battle,record,bool.Parse(v));else bf.SetStat(facility,battle,record,Enum.Parse<BattleFrontierStatType3>(k),ushort.Parse(v));return;}
        if(id=="ferry"){foreach(var(k,v) in edits)s.SetEventFlag(int.Parse(k),bool.Parse(v));return;}
        if(id=="tickets") {int[] tickets=mode=="ticketsAll"||s.Japanese?[0x109,0x113,0x172,0x173,0x178]:[0x109,0x113,0x172,0x173];var bag=s.Inventory;var pouch=bag.GetPouch(InventoryType.KeyItems);var missing=tickets.Where(i=>!pouch.HasItem((ushort)i)).ToArray();var empty=pouch.Items.Where(i=>i.Index==0||i.Count==0).ToArray();if(empty.Length<missing.Length)throw new Exception("There is not enough room in the Key Items pouch.");for(int i=0;i<missing.Length;i++){empty[i].Index=missing[i];empty[i].Count=1;}bag.CopyTo(s);return;}
        if(id=="mirage"){if(mode=="mirage"){if(s.PartyCount==0)throw new Exception("Add a Pokémon to the party first.");s.SetWork(0x24,(ushort)s.GetPartySlotAtIndex(0).PID);}else s.SetWork(0x24,ushort.Parse(edits["Value"]));return;}
        var hoenn=(ISaveBlock3LargeHoenn)s.LargeBlock;
        if(p[0]=="decoration") {var pouch=DecorationPouch3(hoenn.Decorations,index);if(mode=="clearDecorations")pouch.Clear();else if(mode=="giveDecorations"){var allowed=Enum.GetValues<Decoration3>().Where(d=>d!=Decoration3.NONE&&(int)d.GetCategory()==index).ToArray();if(allowed.Length>pouch.Length)throw new Exception("This category cannot hold every decoration.");pouch.Clear();allowed.CopyTo(pouch);}else foreach(var(k,v) in edits)pouch[int.Parse(k)]=(Decoration3)byte.Parse(v);return;}
        if(p[0]=="painting") {var paint=hoenn.GetPainting(index,s.Japanese);if(mode=="clearPainting"||edits.GetValueOrDefault("Enabled")=="false"){paint.Clear();s.SetEventFlag(Paintings3.GetFlagIndexContestStat(index),false);}else{if(edits.Remove("Enabled",out var enabled))s.SetEventFlag(Paintings3.GetFlagIndexContestStat(index),bool.Parse(enabled));if(edits.Remove("Caption",out var caption))paint.SetCaptionRelative(index,int.Parse(caption));SetExtraProperties(paint,edits);}hoenn.SetPainting(index,paint);}
    }
}
