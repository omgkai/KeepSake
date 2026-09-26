using System.Text.Json;
using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

sealed partial class EditorSession {
    static readonly string[] FestaMessages=["Greeting","Goodbye","Moving","Disappointed"];
    static readonly int[][] FestaColors=[[0,1,2,3],[4,0,5,3],[1,0,5,3],[6,7,0,3],[4,5,8,3],[0,1,2,3],[0,7,8,4,5,1,9,10],[11,1,5,3]];
    static (Choice choice,int group)[] FacilityChoices(bool ultra) {
        string[][] names=[["Big Dream","Gold Rush","Treasure Hunt"],["Ghost's Den","Trick Room","Confuse Ray"],["Ball Shop","General Store","Battle Store","Soft Drink Parlor","Pharmacy"],["Rare Kitchen","Battle Table","Friendship Café","Friendship Parlor"],["Thump-Bump Park","Clink-Clunk Land","Stomp-Stomp House"],["Kanto Tent","Johto Tent","Hoenn Tent","Sinnoh Tent","Unova Tent","Kalos Tent","Pokémon House"],["Red Dye","Yellow Dye","Green Dye","Blue Dye","Orange Dye","Navy Blue Dye","Purple Dye","Pink Dye"],["Switcheroo"]];
        int[][] counts=[[5,5,5],[5,5,5],[3,5,3,3,3],[5,4,5,5],[5,5,5],[4,4,4,4,4,4,4],[4,4,4,4,4,4,4,4],[3]];
        var result=new List<(Choice,int)>();for(int g=0;g<(ultra?8:7);g++)for(int n=0;n<names[g].Length;n++)foreach(int stars in counts[g][n]==4?new[]{1,3,5}:Enumerable.Range(1,counts[g][n]).ToArray())result.Add((new(result.Count.ToString(),$"{names[g][n]} · {stars}★"),g));return result.ToArray();
    }
    ExtraPage ReadFestival(ExtraTool tool,string? detailId) {
        var sav=(SAV7)save!;var p=sav.Festa;bool ultra=sav is SAV7USUM;var rows=new List<ExtraRow>();
        var fs=new List<ExtraValue>{ET("FestivalPlazaName","Plaza name",p.FestivalPlazaName,20),EP(p,"FestaRank","Rank",999,1),EP(p,"FestaCoins","Current coins",9999999),EV("UsedCoins","Spent coins",sav.GetRecord(38),9999999)};
        for(int i=0;i<4;i++)fs.Add(EV("message:"+i,FestaMessages[i]+" message",p.GetFestaMessage(i),9999) with {group="Messages"});
        var date=p.FestaDate;fs.Add(new("Date","Plaza start date",date?.ToString("yyyy-MM-dd")??"","date","0","0",[],"Date"));fs.Add(EV("Hour","Hour",date?.Hour??0,23) with {group="Date"});fs.Add(EV("Minute","Minute",date?.Minute??0,59) with {group="Date"});fs.Add(EV("Second","Second",date?.Second??0,59) with {group="Date"});
        rows.Add(ER("plaza","Your Festival Plaza",fs.ToArray(),$"Rank {p.FestaRank} · {p.TotalFestaCoins:N0} lifetime coins"));
        var facilities=FacilityChoices(ultra);
        for(int i=0;i<7;i++) {var f=p.GetFestaFacility(i);var values=new List<ExtraValue>();string id="facility:"+i;
            if(detailId==id){int group=f.Type>=0&&f.Type<facilities.Length?facilities[f.Type].group:0;
                values.AddRange([EP(f,"Type","Facility",choices:facilities.Select(x=>x.choice).ToArray()),EP(f,"Color","Color",choices:Enumerable.Range(0,FestaColors[group].Length).Select(n=>new Choice(n.ToString(),Label(((FestivalPlazaFacilityColor)FestaColors[group][n]).ToString()))).ToArray()),ET("OriginalTrainerName","Owner",f.OriginalTrainerName,12),EP(f,"Gender",choices:[new("0","Boy ♂"),new("1","Girl ♀")]),EP(f,"NPC","Host",choices:new[]{"Ace Trainer ♀","Ace Trainer ♂","Veteran ♀","Veteran ♂","Office Worker ♂","Office Worker ♀","Punk Guy","Punk Girl","Breeder ♂","Breeder ♀","Youngster","Lass"}.Select((s,n)=>new Choice(n.ToString(),s)).ToArray()),EP(f,"IsIntroduced","Introduced by another player")]);
                for(int n=0;n<4;n++)values.Add(EV("message:"+n,FestaMessages[n]+" message",f.GetMessage(n),9999) with {group="Messages"});
                values.AddRange([EP(f,"ExchangeLeftCount","Exchanges remaining",255),EP(f,"UsedLuckyRank","Fortune strength",choices:[new("0","None"),new("1","A bit"),new("2","A whole lot"),new("3","A whole ton")]),EP(f,"UsedLuckyPlace","Fortune activity",choices:new[]{"None","GTS","Wonder Trade","Battle Spot","Festival Plaza","Mission","Lottery","Haunted house"}.Select((s,n)=>new Choice(n.ToString(),s)).ToArray()),EP(f,"UsedFlags","Used flags") with {group="Advanced"},EP(f,"UsedRandStat","Random state") with {group="Advanced"},new("TrainerFesID","Festival trainer identifier",Convert.ToHexString(f.TrainerFesID),"hex","0","24",[],"Advanced")]);
            }
            rows.Add(ER(id,$"Facility {i+1} · {(f.Type<facilities.Length?facilities[f.Type].choice.label:"Stored type "+f.Type)}",values.ToArray(),f.OriginalTrainerName,actions:[new("cleartrainer","Clear Festival Trainer ID")]));
        }
        var phrases=ExtraNames("festivalPhrases");for(int i=0;i<107;i++)rows.Add(ER("phrase:"+i,phrases[i],[EV("Unlocked","Unlocked",p.GetFestaPhraseUnlocked(i))],"Plaza phrase"));
        string[] rewards=["Rank 4 · Missions","Rank 8 · Facility","Rank 10 · Fashion","Rank 20 · Rename","Rank 30 · Special menu","Rank 40 · Music","Rank 50 · Glitz theme","Rank 60 · Fairy theme","Rank 70 · Tone theme","Rank 100 · Phrase","Current rank"];
        for(int i=0;i<11;i++)rows.Add(ER("reward:"+i,rewards[i],[EV("State","Reward",p.GetFestPrizeReceived(i),choices:[new("0","Locked"),new("1","Ready to receive"),new("2","Received")])],"Rank reward"));
        if(ultra){var d=sav.Data;ushort packed=ReadUInt16LittleEndian(d[0x6C55C..]);var agency=new List<ExtraValue>{EV("Grade","Grade",(packed>>6)&63,50),EV("Defeated","Trainers defeated",packed>>12,14),EV("DefeatedPokemon","Pokémon defeated",ReadUInt16LittleEndian(d[0x6C558..]),65535),EV("Chosen","Team chosen",sav.GetFlag(0x6C55E,1)),EV("Invited","Guest trainers invited",(ReadUInt16LittleEndian(d[0x6C3EE..])&0x7DFF)==0x7DFF&&(ReadUInt16LittleEndian(d[0x6C526..])&0x7DFF)==0x7DFF)};for(int i=0;i<3;i++)agency.Add(EV("trainer:"+i,$"Trainer {i+1}",ReadUInt16LittleEndian(d[(0x6C56C+0x14*i)..]),210));rows.Add(ER("agency","Battle Agency",agency.ToArray(),"Grade, results and guest trainers",actions:[new("glasses","Give Agent Sunglasses")]));
            for(int i=0;i<3;i++){var pk=AgencyPokemon(i);rows.Add(ER("agent:"+i,$"Agency Pokémon {i+1} · {Species(pk.Species)}",[],$"Level {pk.CurrentLevel}",Sprite(pk),actions:[new("party","Use Corresponding Party Pokémon")]) with {fileExtension="pk7"});}
        }
        return new(tool.id,revision,tool,rows.ToArray(),[new("phrases","Unlock All Phrases"),new("readyrewards","Make All Rewards Ready"),new("receivedrewards","Mark All Rewards Received")],ultra);
    }
    PKM AgencyPokemon(int i){var sav=(SAV7)save!;return i==0?sav.GetStoredSlot(sav.Data[0x6C200..]):sav.GetPartySlot(sav.Data[(i==1?0x6C2E8:0x6C420)..]);}
    void SetAgencyPokemon(int i,PKM pk){var sav=(SAV7)save!;if(pk is not PK7||pk.Species==0)throw new Exception("Choose a Generation 7 Pokémon.");if(i==0)pk.WriteEncryptedDataStored(sav.Data[0x6C200..]);else pk.WriteEncryptedDataParty(sav.Data[(i==1?0x6C2E8:0x6C420)..]);}
    void EditFestival(JsonElement r,string id,string mode,Dictionary<string,string> edits) {
        var sav=(SAV7)save!;var p=sav.Festa;
        if(mode=="import"&&!id.StartsWith("agent:"))throw new Exception("Select a Battle Agency Pokémon to import.");
        if(mode=="phrases"){for(int n=0;n<107;n++)p.SetFestaPhraseUnlocked(n,true);return;}
        if(mode is "readyrewards" or "receivedrewards"){for(int n=0;n<11;n++)p.SetFestaPrizeReceived(n,mode=="readyrewards"?(byte)1:(byte)2);return;}
        int i=id.Contains(':')?int.Parse(id.Split(':')[1]):0;
        if(id.StartsWith("phrase:")){p.SetFestaPhraseUnlocked(i,bool.Parse(edits["Unlocked"]));return;}
        if(id.StartsWith("reward:")){p.SetFestaPrizeReceived(i,byte.Parse(edits["State"]));return;}
        if(id.StartsWith("agent:")){if(mode=="party"){if(i>=sav.PartyCount)throw new Exception("There is no Pokémon in that party slot.");SetAgencyPokemon(i,sav.GetPartySlotAtIndex(i));}else if(mode=="import"){string path=S(r,"path");long size=new FileInfo(path).Length;if(!Path.GetExtension(path).Equals(".pk7",StringComparison.OrdinalIgnoreCase)||size!=new PK7().SIZE_STORED&&size!=new PK7().SIZE_PARTY)throw new Exception("Choose a Generation 7 .pk7 file.");var bytes=ReadExtraBytes(path,(int)size);var pk=new PK7(bytes);if(!pk.ChecksumValid||pk.Species>sav.MaxSpeciesID)throw new Exception("The Pokémon file has an invalid checksum or species.");SetAgencyPokemon(i,pk);}return;}
        if(id=="agency") {if(mode=="glasses"){sav.Fashion.Data[0xD0]|=1;return;}ushort old=ReadUInt16LittleEndian(sav.Data[0x6C55C..]);int grade=edits.TryGetValue("Grade",out var g)?int.Parse(g):(old>>6)&63,defeated=edits.TryGetValue("Defeated",out var v)?int.Parse(v):old>>12;if(defeated>(Math.Min(49,grade)/10*3)+2)throw new Exception("That defeated-trainer count exceeds this grade's round length.");foreach(var (k,value) in edits){if(k=="Chosen")sav.SetFlag(0x6C55E,1,bool.Parse(value));else if(k=="Invited"){foreach(int off in new[]{0x6C3EE,0x6C526})WriteUInt16LittleEndian(sav.Data[off..],(ushort)(bool.Parse(value)?ReadUInt16LittleEndian(sav.Data[off..])|0x7DFF:0));}else if(k=="DefeatedPokemon")WriteUInt16LittleEndian(sav.Data[0x6C558..],ushort.Parse(value));else if(k.StartsWith("trainer:"))WriteUInt16LittleEndian(sav.Data[(0x6C56C+0x14*int.Parse(k[8..]))..],ushort.Parse(value));}WriteUInt16LittleEndian(sav.Data[0x6C55C..],(ushort)((old&63)|(grade<<6)|(defeated<<12)));return;}
        if(id=="plaza") {
            if(edits.Remove("UsedCoins",out var used))sav.SetRecord(38,int.Parse(used));
            int current=edits.TryGetValue("FestaCoins",out var coins)?int.Parse(coins):p.FestaCoins;if((long)current+sav.GetRecord(38)>9999999)throw new Exception("Current plus spent coins cannot exceed 9,999,999.");
            bool changedDate=edits.Keys.Any(k=>k is "Date" or "Hour" or "Minute" or "Second");var date=p.FestaDate??new DateTime(2000,1,1);
            if(changedDate){if(edits.Remove("Date",out var dateText)){if(dateText.Length==0)throw new Exception("Choose a valid plaza start date.");date=DateTime.ParseExact(dateText,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture).Add(date.TimeOfDay);}int hour=edits.Remove("Hour",out var h)?int.Parse(h):date.Hour,minute=edits.Remove("Minute",out var m)?int.Parse(m):date.Minute,second=edits.Remove("Second",out var s)?int.Parse(s):date.Second;p.FestaDate=new DateTime(date.Year,date.Month,date.Day,hour,minute,second);}
            foreach(var (k,value) in edits)if(k.StartsWith("message:"))p.SetFestaMessage(int.Parse(k[8..]),ushort.Parse(value));else SetProperty(p,k,value);p.FestaCoins=current;return;
        }
        var f=p.GetFestaFacility(i);if(mode=="cleartrainer"){f.ClearTrainerFesID();return;}
        int type=edits.TryGetValue("Type",out var t)?int.Parse(t):f.Type;var choices=FacilityChoices(sav is SAV7USUM);int color=edits.TryGetValue("Color",out var c)?int.Parse(c):f.Color;
        if(type>=choices.Length||color>=FestaColors[choices[type].group].Length)throw new Exception("Choose a color available for the selected facility. Save the color first when changing facility categories.");
        foreach(var (k,value) in edits)if(k.StartsWith("message:"))f.SetMessage(int.Parse(k[8..]),ushort.Parse(value));else if(k=="TrainerFesID"){if(value.Length!=24)throw new Exception("Festival trainer identifiers require exactly 24 hexadecimal digits.");Convert.FromHexString(value).CopyTo(f.TrainerFesID);}else SetProperty(f,k,value);
    }
    byte[] ExportAgency(string id){if(!id.StartsWith("agent:")||!int.TryParse(id[6..],out int i)||i<0||i>2||save is not SAV7USUM)throw new Exception("Select a Battle Agency Pokémon.");var pk=AgencyPokemon(i);var bytes=new byte[pk.SIZE_STORED];pk.WriteDecryptedDataStored(bytes);return bytes;}
}
