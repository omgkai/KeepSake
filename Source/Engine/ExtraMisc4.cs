using PKHeX.Core;
using static System.Buffers.Binary.BinaryPrimitives;

sealed partial class EditorSession
{
    static int[] FlyFlags4(SAV4 s)=>s is SAV4Sinnoh?[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,67,68]:[0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,27,30,33,35];
    static int[] FlyLocations4(SAV4 s)=>s is SAV4Sinnoh?[1,2,3,4,5,82,83,6,7,8,9,10,11,12,13,14,54,81,55,15]:[138,139,140,141,142,143,144,145,146,147,148,126,127,128,129,130,131,132,133,134,135,136,137,229,227,221,225];
    // Layouts follow SAV_Misc4 in the pinned Windows source: values, battle stride, continue flags.
    static int[][] FrontierLayout4(SAV4 s)=>s switch {
        SAV4DP=>[[0x5FCA,4,0x6601]],
        SAV4Pt=>[[0x68E0,4,0x723D],[0x68F4,16,0x7EF8],[0x6924,24,0x7EFC],[0x696C,16,0x7F00],[0x699C,4,0x7F04]],
        SAV4HGSS=>[[0x5264,4,0x5BC1],[0x5278,16,0x687C],[0x52A8,24,0x6880],[0x52F0,16,0x6884],[0x5320,4,0x6888]],_=>throw new Exception("Unsupported Sinnoh/Johto save.")};
    ExtraPage ReadMisc4(ExtraTool tool,string? detailId) {
        // Record getters decrypt their backing data. All reads operate on a clone.
        var s=(SAV4)RequireSave().Clone();var rows=new List<ExtraRow>();void Add(string category,ExtraRow row)=>rows.Add(row with {category=category});
        var overview=new List<ExtraValue>{EP(s,"Coin","Game Corner coins",s.MaxCoins),EP(s,"BP","Battle Points",9999)};
        if(s is SAV4Sinnoh sn)overview.Add(EP(sn,"UG_FlagsCaptured","Underground flags captured",SAV4Sinnoh.UG_MAX));
        if(s is SAV4HGSS hg)overview.Add(EP(hg,"MapUnlockState","Map coverage",choices:ExtraEnums<MapUnlockState4>().Where(c=>c.label!="Invalid").ToArray()));
        Add("Adventure",ER("overview","Currencies & adventure",overview.ToArray()));var flags=FlyFlags4(s);var locations=FlyLocations4(s);
        Add("Adventure",ER("fly","Fly destinations",flags.Select((f,i)=>EV(f.ToString(),strings.Gen4.Met0[locations[i]],s.GetEventFlag(2480+f))).ToArray(),actions:[new("flyAll","Unlock All Destinations")]));
        if(s is SAV4HGSS walker){var fields=new List<ExtraValue>{EP(walker,"PokewalkerWatts","Watts"),EP(walker,"PokewalkerSteps","Steps")};var courses=new bool[SAV4HGSS.PokewalkerCourseFlagCount];walker.GetPokewalkerCoursesUnlocked(courses);for(int i=0;i<courses.Length;i++)fields.Add(EV("Course"+i,i<strings.walkercourses.Length?strings.walkercourses[i]:$"Course {i+1}",courses[i]) with {group="Courses"});Add("Pokéwalker",ER("walker","Walking companion",fields.ToArray(),"Steps, Watts and every course.",actions:[new("coursesAll","Unlock All Courses")]));}
        if(s is SAV4Sinnoh poketch){Add("Pokétch",ER("poketch","Apps & current display",[EV("Current","Current app",poketch.CurrentPoketchApp,choices:strings.poketchapps.Select((n,i)=>new Choice(i.ToString(),n)).ToArray()),..Enumerable.Range(0,(int)PoketchApp.Alarm_Clock+1).Select(i=>EV("App"+i,strings.poketchapps[i],poketch.GetPoketchAppUnlocked((PoketchApp)i)))],actions:[new("appsAll","Give All Apps")]));Add("Pokétch",ER("dotart","Dot Artist",[DotArtField(poketch)],"Draw or import a 24 × 20 picture with four shades."));}
        Add("Seals",ER("seals","Seal case",[],"Individual counts and complete collections.",actions:[new("sealsLegal","Give Released Seals"),new("sealsAll","Include Unreleased Seals"),new("sealsClear","Empty Seals")]));
        for(int i=0;i<(int)Seal4.MAX;i++)Add("Seals",ER("seal:"+i,strings.seals[i],[EV("Count","Owned",s.GetSealCount((Seal4)i),SAV4.SealMaxCount)]));
        Add("Accessories",ER("accessories","Dress-up collection",[],"Individual counts respect each accessory’s stack limit.",actions:[new("accessoriesLegal","Give Released Accessories"),new("accessoriesAll","Include Unreleased Accessories"),new("accessoriesClear","Empty Accessories")]));
        for(int i=0;i<AccessoryInfo.Count;i++)Add("Accessories",ER("accessory:"+i,strings.accessories[i],[EV("Count","Owned",s.GetAccessoryOwnedCount((Accessory4)i),i<=AccessoryInfo.MaxMulti?AccessoryInfo.AccessoryMaxCount:1)]));
        var positions=Enumerable.Repeat((int)Backdrop4.Unset,BackdropInfo.Count).ToArray();for(int i=0;i<BackdropInfo.Count;i++){int pos=s.GetBackdropPosition((Backdrop4)i);if(pos<positions.Length)positions[pos]=i;}
        Add("Backdrops",ER("backdrops","Backdrop album",positions.Select((value,i)=>EV(i.ToString(),$"Position {i+1}",value,choices:Enumerable.Range(0,BackdropInfo.Count).Append((int)Backdrop4.Unset).Select(n=>new Choice(n.ToString(),n==(int)Backdrop4.Unset?"Empty":strings.backdrops[n])).ToArray())).ToArray(),"Empty positions are packed when saved; duplicate backdrops are rejected.",actions:[new("backdropsLegal","Give Released Backdrops"),new("backdropsAll","Include Unreleased Backdrops"),new("backdropsClear","Empty Album")]));
        var records=s.Records;for(int i=0;i<records.Record32;i++)Add("Records",ER("record32:"+i,$"32-bit record {i}",[EV("Value","Value",records.GetRecord32(i))]));for(int i=0;i<Record4.Record16;i++)Add("Records",ER("record16:"+i,$"16-bit record {i}",[EV("Value","Value",records.GetRecord16(i),65535)]));
        var layout=FrontierLayout4(s);string[] facilities=["Tower","Factory","Hall","Castle","Arcade"],modes=["Singles","Doubles","Multi"],towerModes=["Singles","Doubles","Multi · trainer","Multi · friend","Wi-Fi"];
        for(int f=0;f<layout.Length;f++) {
            if(s is not SAV4DP)Add("Frontier",ER("print:"+f,"Battle "+facilities[f]+" print",[EV("Status","Print status",s.GetWork((s is SAV4Pt?79:77)+f),choices:ExtraEnums<BattleFrontierPrintStatus4>())]));
            var info=layout[f];int count=f==0?5:3;
            for(int m=0;m<count;m++)for(int level=0;level<(f==1?2:1);level++) {
                int offset=info[0]+info[1]*m+(level<<3);var fields=new List<ExtraValue>{EV("Continue","Continue challenge",(s.General[info[2]]&(1<<(m+(level<<2))))!=0)};
                string[] stats=f==1?["Record streak","Current streak","Record swaps","Current swaps"]:f==3?["Record streak","Current streak","Current Castle Points","Used Castle Points","Record Castle Points"]:["Record streak","Current streak"];
                for(int k=0;k<stats.Length;k++)if(!(f==3&&k==3))fields.Add(EV("Stat"+k,stats[k],ReadUInt16LittleEndian(s.General[(offset+k*2)..]),9999));
                if(f==0)fields.Add(EV("ContinueCount","Continue count",ReadUInt16LittleEndian(s.General[(info[2]+(s is SAV4DP?3:1)+m*2)..]),65535));
                if(f==3)for(int k=0;k<3;k++)fields.Add(EV("Rank"+k,new[]{"Recovery rank","Item rank","Information rank"}[k],ReadUInt16LittleEndian(s.General[(offset+10+k*2)..]),k==2?2:3,1));
                if(f==2){fields.Add(EV("Species","Current Pokémon",ReadUInt16LittleEndian(s.General[(offset+4)..]),choices:SpeciesChoices(493)));int[] types=[0,9,10,12,11,14,1,3,4,2,13,6,5,7,15,16,8];for(int k=0;k<types.Length;k++)fields.Add(EV("Type"+k,strings.types[types[k]],(s.General[offset+6+(k>>1)*2]>>((k&1)*4))&15,10));}
                Add("Frontier",ER($"frontier:{f}:{m}:{level}","Battle "+facilities[f]+" · "+(f==0?towerModes[m]:modes[m]),fields.ToArray(),f==1?(level==0?"Level 50":"Open Level"):"Battle records"));
            }
        }
        var hall=s is SAV4DP||!s.State.Exportable?null:s.GetHall();if(hall!=null)for(ushort sp=1;sp<=493;sp++)Add("Hall records",ER("hall:"+sp,Species(sp),Enumerable.Range(0,3).Select(i=>EV(i.ToString(),modes[i],hall.GetCount(i,sp),9999)).ToArray(),"Lifetime Battle Hall streaks",SpriteFor(sp,0,0,0,s.Context)));
        return new("misc4",revision,tool,rows.ToArray(),[]);
    }
    static ExtraValue DotArtField(SAV4Sinnoh s){var data=s.GetPoketchDotArtistData();var pixels=new char[480];for(int i=0;i<pixels.Length;i++)pixels[i]=(char)('0'+((data[i>>2]>>((i&3)*2))&3));return new("Pixels","Dot Artist canvas",new string(pixels),"dotart","480","480",[]);}
    void EditMisc4(string id,string mode,Dictionary<string,string> edits) {
        var s=(SAV4)RequireSave();var parts=id.Split(':');int index=parts.Length>1?int.Parse(parts[1]):-1;
        if(id=="dotart"){var pixels=edits["Pixels"];var data=new byte[120];for(int n=0;n<480;n++)data[n>>2]|=(byte)((pixels[n]-'0')<<((n&3)*2));((SAV4Sinnoh)s).SetPoketchDotArtistData(data);return;}
        if(id=="overview"){SetExtraProperties(s,edits);return;}
        if(id=="fly"){if(mode=="flyAll")foreach(int flag in FlyFlags4(s))s.SetEventFlag(2480+flag,true);else foreach(var(k,v) in edits)s.SetEventFlag(2480+int.Parse(k),bool.Parse(v));return;}
        if(id=="walker") {var w=(SAV4HGSS)s;if(mode=="coursesAll"){w.PokewalkerCoursesUnlockAll();return;}var courses=new bool[SAV4HGSS.PokewalkerCourseFlagCount];w.GetPokewalkerCoursesUnlocked(courses);foreach(var(k,v) in edits)if(k.StartsWith("Course"))courses[int.Parse(k[6..])]=bool.Parse(v);else SetProperty(w,k,v);w.SetPokewalkerCoursesUnlocked(courses);return;}
        if(id=="poketch") {var p=(SAV4Sinnoh)s;if(mode=="appsAll")for(int i=0;i<=(int)PoketchApp.Alarm_Clock;i++)p.SetPoketchAppUnlocked((PoketchApp)i,true);else foreach(var(k,v) in edits)if(k=="Current")p.CurrentPoketchApp=sbyte.Parse(v);else p.SetPoketchAppUnlocked((PoketchApp)int.Parse(k[3..]),bool.Parse(v));p.PoketchUnlockedCount=(byte)Enumerable.Range(0,(int)PoketchApp.Alarm_Clock+1).Count(i=>p.GetPoketchAppUnlocked((PoketchApp)i));return;}
        if(parts[0]=="seal"){s.SetSealCount((Seal4)index,byte.Parse(edits["Count"]));return;}
        if(id=="seals"){int count=mode=="sealsLegal"?(int)Seal4.MAXLEGAL:(int)Seal4.MAX;for(int i=0;i<count;i++)s.SetSealCount((Seal4)i,mode=="sealsClear"?(byte)0:SAV4.SealMaxCount);return;}
        if(parts[0]=="accessory"){s.SetAccessoryOwnedCount((Accessory4)index,byte.Parse(edits["Count"]));return;}
        if(id=="accessories"){int count=mode=="accessoriesLegal"?AccessoryInfo.MaxLegal+1:AccessoryInfo.Count;for(int i=0;i<count;i++)s.SetAccessoryOwnedCount((Accessory4)i,(byte)(mode=="accessoriesClear"?0:i<=AccessoryInfo.MaxMulti?AccessoryInfo.AccessoryMaxCount:1));return;}
        if(id=="backdrops") {var values=Enumerable.Repeat((int)Backdrop4.Unset,BackdropInfo.Count).ToArray();if(mode=="edit"){for(int i=0;i<BackdropInfo.Count;i++){int pos=s.GetBackdropPosition((Backdrop4)i);if(pos<values.Length)values[pos]=i;}foreach(var(k,v) in edits)values[int.Parse(k)]=int.Parse(v);}else if(mode!="backdropsClear"){int count=mode=="backdropsLegal"?(int)BackdropInfo.MaxLegal+1:BackdropInfo.Count;for(int i=0;i<count;i++)values[i]=i;}var occupied=values.Where(i=>i!=(int)Backdrop4.Unset).ToArray();if(occupied.Distinct().Count()!=occupied.Length)throw new Exception("Each backdrop can appear only once.");for(int i=0;i<BackdropInfo.Count;i++)s.RemoveBackdrop((Backdrop4)i);for(int i=0;i<occupied.Length;i++)s.SetBackdropPosition((Backdrop4)occupied[i],(byte)i);return;}
        if(parts[0] is "record32" or "record16"){var records=s.Records;try{if(parts[0]=="record32")records.SetRecord32(index,uint.Parse(edits["Value"]));else records.SetRecord16(index,ushort.Parse(edits["Value"]));}finally{records.EndAccess();}return;}
        if(parts[0]=="print"){s.SetWork((s is SAV4Pt?79:77)+index,ushort.Parse(edits["Status"]));return;}
        if(parts[0]=="hall"){var hall=s.GetHall()??throw new Exception("This save has no initialized Hall record block.");foreach(var(k,v) in edits)hall.SetCount(int.Parse(k),(ushort)index,ushort.Parse(v));hall.RefreshChecksum();return;}
        if(parts[0]=="frontier") {int m=int.Parse(parts[2]),level=int.Parse(parts[3]);var info=FrontierLayout4(s)[index];int offset=info[0]+info[1]*m+(level<<3);foreach(var(k,v) in edits){if(k=="Continue"){byte mask=(byte)(1<<(m+(level<<2)));if(bool.Parse(v)){s.General[info[2]]|=mask;if(index==3)s.General[info[2]+1]|=1;}else s.General[info[2]]&=(byte)~mask;}else if(k=="ContinueCount")WriteUInt16LittleEndian(s.General[(info[2]+(s is SAV4DP?3:1)+m*2)..],ushort.Parse(v));else if(k=="Species")WriteUInt16LittleEndian(s.General[(offset+4)..],ushort.Parse(v));else if(k.StartsWith("Type")){int n=int.Parse(k[4..]),o=offset+6+(n>>1)*2,shift=(n&1)*4;s.General[o]=(byte)((s.General[o]&~(15<<shift))|(int.Parse(v)<<shift));}else{int o=k.StartsWith("Rank")?offset+10+int.Parse(k[4..])*2:offset+int.Parse(k[4..])*2;WriteUInt16LittleEndian(s.General[o..],ushort.Parse(v));}}}
    }
}
