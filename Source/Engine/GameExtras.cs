using System.Globalization;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession {
    record ExtraTool(string id,string name,string description,string icon);
    record ExtraValue(string id,string label,string value,string kind,string min,string max,Choice[] choices,string group="");
    record ExtraRow(string id,string name,string detail,string sprite,ExtraValue[] fields,Choice[] actions,int[]? profile=null,string? fileExtension=null,string? category=null);
    record ExtraPage(string kind,int revision,ExtraTool tool,ExtraRow[] entries,Choice[] actions,bool files=false);
    static string[] ExtraNames(string name)=>JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"GameData",name+".json")))!;
    static Choice[] ExtraEnums<T>() where T:struct,Enum=>Enum.GetValues<T>().Where(e=>e.ToString()!="Count").Select(e=>new Choice(Convert.ToUInt64(e).ToString(),Label(e.ToString()))).ToArray();
    static ExtraValue EV(string id,string label,object value,decimal max=uint.MaxValue,decimal min=0,Choice[]? choices=null) {
        bool boolean=value is bool;return new(id,label,boolean?value.ToString()!.ToLowerInvariant():value is Enum?Convert.ToUInt64(value).ToString():Convert.ToString(value,CultureInfo.InvariantCulture)!,boolean?"bool":choices!=null?"enum":"number",min.ToString(CultureInfo.InvariantCulture),max.ToString(CultureInfo.InvariantCulture),choices??[]);
    }
    static ExtraValue EP(object target,string id,string? label=null,decimal max=uint.MaxValue,decimal min=0,Choice[]? choices=null)=>EV(id,label??Label(id),target.GetType().GetProperty(id)!.GetValue(target)!,max,min,choices);
    static ExtraRow ER(string id,string name,ExtraValue[] fields,string detail="",string sprite="",Choice[]? actions=null,int[]? profile=null)=>new(id,name,detail,sprite,fields,actions??[],profile);
    ExtraTool[] ExtraTools() {
        var list=new List<ExtraTool>();void Add(string id,string name,string description,string icon)=>list.Add(new(id,name,description,icon));
        if(save is ISaveBlock6Main)Add("maison","Battle Maison","Normal and Super records for every battle style.","building.columns.fill");
        if(save is SAV8LA or SAV8SWSH)Add("trainerProgress","Trainer progress","Merit, Survey Corps and satchel records, or Battle Tower wins and streaks.","chart.line.uptrend.xyaxis");
        if(save is SAV_STADIUM)Add("registeredTeams","Registered teams","Browse Stadium teams and view their Pokémon in the editor.","person.3.fill");
        if(save is SAV7SM or SAV7USUM)Add("trainerAlola","Alola trainer adventures","Named fly destinations, map visibility and Battle Tree records.","map.fill");
        if(save is SAV8SWSH)Add("trainerTeams","League card & title teams","Edit the six Pokémon displayed on your league card and title screen, or copy your party.","person.crop.rectangle.stack.fill");
        if(save is SAV7b or SAV8SWSH or SAV9SV or SAV9ZA)Add("trainerUnlocks","Adventure unlocks","Game-specific collectibles, destinations and trainer unlocks, with Undo.","key.fill");
        if(save is ITrainerStatRecord)Add("trainerRecords","Trainer records","Named lifetime adventure statistics and game-specific record limits.","chart.bar.fill");
        if(save is SAV9ZA)Add("events9a","Z-A event records","Quest progress, flags, counters and keyed world records.","list.bullet.rectangle");
        if(save is SAV2 or SAV2Stadium or SAV3 or SAV4 or SAV5)Add("mail","Mailbox","Letters, stationery, authors, message phrases and party mail links.","envelope.fill");
        if(save is SAV5)Add("misc5","Unova adventures","Entrée Forest, Funfest, Battle Subway, Musical props, keys and adventure records.","leaf.arrow.triangle.circlepath");
        if(save is SAV4)Add("misc4","Sinnoh & Johto adventures","Battle Frontier, Pokétch, Pokéwalker, seals, accessories, backdrops and adventure records.","sparkles.rectangle.stack.fill");
        if(save is SAV3)Add("misc3","Hoenn & Kanto adventures","Frontier records, island tickets, decorations, museum paintings and Joyful games.","map.fill");
        if(save is SAV8BS)Add("unlocks8b","Sinnoh adventure unlocks","Legendary encounters, island events, map destinations and route trainers.","key.fill");
        if(save is SAV4BR)Add("gear","Battle Revolution wardrobe","Named clothing, shared badges, special shiny outfits and Give All.","tshirt.fill");
        if(save is SAV4BR)Add("passes","Battle Passes","Trainer cards, appearance, messages, battle records and six-Pokémon teams.","person.text.rectangle");
        if(save is ISaveBlock6Main)Add("link6","Pokémon Link","Delivery source, Pokémon rewards, item gifts, Battle Points and Poké Miles.","link");
        if(save is ISaveBlock6Main)Add("training6","Super Training records","Training bags, stage unlocks and both record holders for every stage.","figure.strengthtraining.traditional");
        if(save is SAV5)Add("globallink5","Global Link","Game Sync records, Dream World items and furniture stored in your save.","globe");
        if(save is SAV5)Add("dlc5","Downloaded content","C-Gear art, Pokédex skins, musicals, movies, battle videos and Memory Link files.","square.and.arrow.down");
        if(save is SAV4Pt or SAV4HGSS)Add("dlc4","Battle videos","View recorded teams and import or export Generation 4 battle videos.","play.rectangle");
        if(save is SAV1)Add("respawn1","Stationary encounters","Restore the Pokémon and gifts that have disappeared from the map.","arrow.counterclockwise");
        if(save is SAV2 {Version:GameVersion.C})Add("gsball","GS Ball event","Enable Crystal's Celebi event, using the same event flags as PKHeX.","sparkles");
        if(save is SAV3){Add("roamer3","Roaming Pokémon","Species, personality, health and IVs for the active roaming Pokémon.","hare.fill");if(save is SAV3 {SmallBlock:ISaveBlock3SmallHoenn})Add("clock3","Hoenn clock","Initial and elapsed clocks, reset and the berry clock fix.","clock.fill");}
        if(save is SAV4HGSS)Add("apricorns","Apricorn box","Named Apricorn quantities, Give All and Empty.","leaf.fill");
        if(save is SAV4Sinnoh)Add("honey","Honey trees","Tree timers, encounters and your trainer's Munchlax trees.","tree.fill");
        if(save is SAV5B2W2)Add("avenue","Join Avenue","Visitors, shops, fans, assistants and the avenue you make your own.","storefront.fill");
        if(save is SAV5B2W2){Add("medals","Medals","Named awards, dates, unread marks, rank and medal-file import/export.","medal.fill");Add("habitats","Habitat list","Grass, water and fishing completion records and tutorials.","map.fill");}
        if(save is SAV6XY)Add("roamer6","Roaming legendary bird","Choose the bird, encounter count and roaming state.","bird.fill");
        if(save is ISaveBlock6Main)Add("opowers","O-Powers","Unlocks, field and battle levels, points, Give All and Clear.","bolt.fill");
        if(save is SAV7SM or SAV7USUM)Add("zygarde","Zygarde collection","Named cell or sticker locations, collection states and totals.","hexagon.fill");
        if(save is SAV7b)Add("captures","Catch records","Per-species captures, transfers, totals and Pokédex-aware bulk changes.","scope");
        if(save is SAV8BS)Add("stickers","Ball stickers","Named stickers, available and lifetime counts, obtained flags and Give All.","seal.fill");
        if(save is SAV9ZA za && za.Donuts.Data.Length>=DonutPocket9a.MaxCount*Donut9a.Size)Add("donuts","Donuts","Recipes, berries, powers, flavor profiles and individual donut files.","birthday.cake.fill");
        if(save is SAV1 or SAV6 or SAV7 || save is SAV3 {Data.Length:>=0x1E000})Add("fame","Hall of Fame","League-winning teams, Pokémon and clear records for this game.","trophy.fill");
        if(save is SAV4)Add("geonet","Globe locations","Countries, regions, visited markers and the trainer’s home location.","globe.europe.africa.fill");
        if(save is SAV5)Add("unity","Unity Tower & globe","Country floors, visited regions and world-map unlocks.","building.2.fill");
        if(save is SAV6XY)Add("berries","Berry field","Inspect the 32 berry plots and their stored values.","leaf.circle.fill");
        if(save is SAV4HGSS)Add("contacts","Pokégear contacts","Named callers, contact order and the original add-all controls.","phone.fill");
        if(save is SAV4Sinnoh)Add("underground4","Underground","Goods, spheres, traps, treasures and your explorer records.","mountain.2.fill");
        if(save is SAV4 or SAV5)Add("chatter","Chatter recording","Chatot’s recorded voice, playback, PCM import and PCM/WAV export.","waveform");
        if(save is SAV8BS)Add("underground8","Grand Underground","Statues, spheres and other underground items, favorites and Give All.","mountain.2.fill");
        if(save is SAV8SWSH)Add("raids8","Max Raid dens","Galar, Isle of Armor and Crown Tundra dens, seeds and encounter settings.","bolt.shield.fill");
        if(save is SAV9SV){Add("raids9","Tera Raid crystals","Daily seeds, crystal locations and settings across Paldea and its expansions.","diamond.fill");Add("sevenstar","Seven-star raid history","Event identifiers and captured or defeated records.","star.circle.fill");}
        if(save is SAV4HGSS)Add("pokeathlon","Pokéathlon","Course teams, solo and linked records, medals, data cards and lifetime progress.","figure.run");
        if(save is SAV3 {LargeBlock:ISaveBlock3LargeHoenn})Add("bases3","Secret Bases","Trainer profiles, battle teams and decorations in Hoenn’s original Secret Bases.","house.fill");
        if(save is SAV6AO)Add("bases6","Super-Secret Bases","Your base, visiting teams, decorations, flags and secret-base files.","house.lodge.fill");
        if(save is SAV7SM or SAV7USUM)Add("festival","Festival Plaza","Facilities, phrases, rank rewards, Festival Coins and the Ultra games’ Battle Agency.","party.popper.fill");
        return list.ToArray();
    }
    ExtraPage ReadExtra(string kind,string? detailId=null) {
        var tool=ExtraTools().FirstOrDefault(t=>t.id==kind)??throw new Exception("This editor is unavailable for the loaded game.");
        var rows=new List<ExtraRow>();Choice[] actions=[];
        var giveClear=new[]{new Choice("give","Give All"),new Choice("clear","Clear All")};
        switch(kind) {
            case "maison":return ReadMaison(tool);
            case "trainerProgress":return ReadTrainerProgress(tool);
            case "registeredTeams":return ReadRegisteredTeams(tool);
            case "trainerAlola":return ReadAlolaTrainer(tool);
            case "trainerTeams":return ReadTrainerTeams(tool);
            case "trainerUnlocks":return ReadTrainerUnlocks(tool);
            case "trainerRecords":return ReadTrainerRecords(tool);
            case "raids8":case "raids9":case "sevenstar":return ReadRaids(tool,detailId);
            case "events9a":return new ExtraPage(tool.id,revision,tool,[],[]);
            case "mail":return ReadMail(tool,detailId);
            case "misc5":return ReadMisc5(tool,detailId);
            case "misc4":return ReadMisc4(tool,detailId);
            case "misc3":return ReadMisc3(tool);
            case "unlocks8b":return ReadUnlocks8b(tool);
            case "gear":return ReadGear(tool);
            case "passes":return ReadPasses(tool,detailId);
            case "link6":return ReadLink6(tool);
            case "training6":return ReadTraining6(tool);
            case "globallink5":return ReadGlobalLink(tool);
            case "dlc4":case "dlc5":return ReadDownloads(tool,detailId);
            case "festival":return ReadFestival(tool,detailId);
            case "bases3":case "bases6":return ReadBases(tool,detailId);
            case "pokeathlon":return ReadSports(tool,detailId);
            case "contacts":return ReadContacts(tool);
            case "underground4":return ReadUnderground4(tool);
            case "chatter":return ReadChatter(tool);
            case "underground8":return ReadUnderground8(tool);
            case "avenue":return ReadAvenue(tool,detailId);
            case "fame":return ReadFame(tool,detailId);
            case "geonet":case "unity":return ReadWorld(tool);
            case "berries":return ReadBerryPlots(tool);
            case "respawn1":foreach(var p in new G1OverworldSpawner((SAV1)save!).GetFlagPairs().OrderBy(p=>p.Name))rows.Add(ER(p.Name,Label(p.Name[G1OverworldSpawner.FlagPropertyPrefix.Length..]),[],p.IsHidden?"Hidden or already encountered":"Available on the map",actions:p.IsHidden?[new("reset","Restore Encounter")]:[]));break;
            case "gsball":rows.Add(ER("event","Celebi's GS Ball event",[],((SAV2)save!).IsEnabledGSBallMobileEvent?"Enabled":"Not enabled",actions:((SAV2)save!).IsEnabledGSBallMobileEvent?[]:[new("enable","Enable Event")]));break;
            case "clock3":
                var small=(ISaveBlock3SmallHoenn)((SAV3)save!).SmallBlock;
                foreach(var (id,c) in new[]{("initial",small.ClockInitial),("elapsed",small.ClockElapsed)})rows.Add(ER(id,id=="initial"?"Initial clock":"Elapsed clock",[EP(c,"Day",max:65535),EP(c,"Hour",max:23),EP(c,"Minute",max:59),EP(c,"Second",max:59)]));
                actions=[new("reset","Reset Both Clocks"),new("berryfix","Apply Berry Clock Fix")];break;
            case "roamer3":var r=new Roamer3(((SAV3)save!).LargeBlock);var fs=new List<ExtraValue>{EP(r,"Species",choices:Enumerable.Range(0,387).Select(i=>new Choice(i.ToString(),Species((ushort)i))).ToArray()),EP(r,"PID","Personality ID",uint.MaxValue),EP(r,"IsActive","Active"),EP(r,"CurrentLevel","Level",100),EP(r,"HP_Current","Current HP",65535)};fs.AddRange(new[]{"HP","ATK","DEF","SPE","SPA","SPD"}.Select(n=>EP(r,"IV_"+n,"IV · "+n,31)));rows.Add(ER("roamer",Species(r.Species),fs.ToArray(),(Roamer3.IsShiny(r.PID,RequireSave())?"Shiny for this trainer. ":"")+(r.IsGlitched?"This game applies the original roaming IV glitch during encounters.":"Emerald preserves the full roaming IVs."),SpriteFor(r.Species,0,0,0,save!.Context)));break;
            case "roamer6":var bird=((SAV6XY)save!).Encount.Roamer;rows.Add(ER("roamer","Legendary bird",[EP(bird,"Species",choices:new[]{0,144,145,146}.Select(i=>new Choice(i.ToString(),i==0?"Not set":Species((ushort)i))).ToArray()),EP(bird,"TimesEncountered","Times encountered"),EP(bird,"RoamStatus","Roaming state",choices:ExtraEnums<Roamer6State>())],"A not-yet-set bird follows the starter choice in the game. Changing the species stores an explicit bird."));break;
            case "apricorns":var hg=(SAV4HGSS)save!;int[] itemIDs=[485,487,486,488,489,490,491];for(int i=0;i<7;i++)rows.Add(ER(i.ToString(),strings.itemlist[itemIDs[i]],[EV("Count","Owned",hg.GetApricornCount(i),255)],sprite:ItemIcon(itemIDs[i],save!.Context)));actions=giveClear;break;
            case "honey":var sinnoh=(SAV4Sinnoh)save!;var names=ExtraNames("honeyTrees");var trees=new byte[4];HoneyTreeUtil.CalculateMunchlaxTrees(sinnoh.ID32,trees);for(int i=0;i<names.Length;i++){var tree=sinnoh.GetHoneyTree(i);ushort species=tree.Group is >=0 and <=3 && tree.Slot is >=0 and <=5 ? sinnoh.GetHoneyTreeSpecies(tree.Group,tree.Slot):(ushort)0;rows.Add(ER(i.ToString(),names[i],[EP(tree,"Time","Minutes remaining",1440),EP(tree,"Group","Encounter group",3),EP(tree,"Slot","Encounter slot",5),EP(tree,"Shake","Shake level",3)],$"{Species(species)} · "+(trees.Contains((byte)i)?"Munchlax tree for this trainer":"Not a Munchlax tree for this trainer"),SpriteFor(species,0,0,0,save!.Context),[new("ready","Make Catchable")]));}break;
            case "opowers":var power=((ISaveBlock6Main)save!).OPower;rows.Add(ER("points","O-Power points",[EP(power,"Points",max:255)]));foreach(var e in Enum.GetValues<OPower6Index>().Where(e=>e.ToString()!="Count"))rows.Add(ER("unlock:"+(int)e,Label(e.ToString()),[EV("State","Unlocked",power.GetState(e)==OPowerFlagState.Unlocked)],"Unlock"));foreach(var e in Enum.GetValues<OPower6FieldType>().Where(e=>e!=OPower6FieldType.Count))rows.Add(ER("field:"+(int)e,Label(e.ToString()),[EV("Level1","Level 1",power.GetLevel1(e),255),EV("Level2","Level 2",power.GetLevel2(e),255)],"Field power"));foreach(var e in Enum.GetValues<OPower6BattleType>().Where(e=>e!=OPower6BattleType.Count))rows.Add(ER("battle:"+(int)e,Label(e.ToString()),[EV("Level1","Level 1",power.GetLevel1(e),255),EV("Level2","Level 2",power.GetLevel2(e),255)],"Battle power"));actions=giveClear;break;
            case "zygarde":var alola=(SAV7)save!;var ew=alola.EventWork;rows.Add(ER("totals","Collection totals",[EP(ew,"ZygardeCellCount","Collected",65535),EP(ew,"ZygardeCellTotal","Total",65535)],alola is SAV7USUM?"Ultra games use these locations for Totem Stickers; saving Collected also updates record 72.":"Totals are independent of individual location states, as in PKHeX."));var locs=ExtraNames(alola is SAV7SM?"locationsSM":"locationsUSUM");for(int i=0;i<ew.TotalZygardeCellCount;i++)rows.Add(ER(i.ToString(),locs[i],[EV("State","Collection state",ew.GetZygardeCell(i),choices:[new("0","None"),new("1","Available"),new("2","Received")])],"Location "+(i+1)));actions=[new("give","Collect All Remaining")];break;
            case "captures":var cap=((SAV7b)save!).Blocks.Captured;rows.Add(ER("totals","Totals",[EP(cap,"TotalCaptured","Captured",999999999),EP(cap,"TotalTransferred","Transferred",999999999)],actions:[new("sum","Sum Species Counts")]));for(ushort i=0;i<=CaptureRecords.MaxIndex;i++){var species=CaptureRecords.GetIndexSpecies(i);rows.Add(ER(i.ToString(),Species(species),[EV("Captured","Captured",cap.GetCapturedCountIndex(i),9999),EV("Transferred","Transferred",cap.GetTransferredCountIndex(i),999999999)],sprite:SpriteFor(species,0,0,0,save!.Context),actions:[new("all","Apply These Counts to Caught Species")]));}break;
            case "stickers":var sn=Util.GetStringList("stickers","en");foreach(var item in ((SAV8BS)save!).SealList.ReadItems().Where(x=>(uint)x.Index<sn.Length&&!string.IsNullOrEmpty(sn[x.Index])))rows.Add(ER(item.Index.ToString(),sn[item.Index],[EP(item,"Count","Available",SealSticker8b.MaxValue),EP(item,"TotalCount","Lifetime obtained",SealSticker8b.MaxValue),EP(item,"IsGet","Obtained")],"Sticker "+item.Index));actions=giveClear;break;
            case "medals":return ReadMedals(tool);
            case "habitats":return ReadHabitats(tool);
            case "donuts":return ReadDonuts(tool,detailId);
        }
        return new(kind,revision,tool,rows.ToArray(),actions);
    }
    Dictionary<string,string> ExtraEdits(JsonElement r,ExtraRow row) {
        var result=new Dictionary<string,string>();foreach(var e in r.GetProperty("edits").EnumerateArray()){
            string id=S(e,"field"),v=S(e,"value");var f=row.fields.FirstOrDefault(f=>f.id==id)??throw new Exception("Unknown editor field.");
            if(!result.TryAdd(id,v))throw new Exception("Duplicate editor field.");
            if(f.kind=="readonly")throw new Exception("This field is read-only.");
            if(f.kind=="dotart"){if(v.Length!=480||v.Any(c=>c<'0'||c>'3'))throw new Exception("The canvas must contain 480 pixels using its four shades.");}
            else if(f.kind=="multiline"){if(v.Length>int.Parse(f.max)||v.Any(c=>char.IsControl(c)&&c!='\n'))throw new Exception($"{f.label} is too long or contains unsupported control characters.");}
            else if(f.kind=="text"){if(v.Length>int.Parse(f.max)||v.Any(char.IsControl))throw new Exception($"{f.label} must use at most {f.max} characters without line breaks.");}
            else if(f.kind=="hex"){if(v.Length==0||v.Length>int.Parse(f.max)||!v.All(Uri.IsHexDigit))throw new Exception($"{f.label} must contain 1–{f.max} hexadecimal digits (0–9, A–F).");}
            else if(f.kind=="float"){if(!float.TryParse(v,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||!float.IsFinite(number)||number<float.Parse(f.min,CultureInfo.InvariantCulture)||number>float.Parse(f.max,CultureInfo.InvariantCulture))throw new Exception($"{f.label} must be a finite number from {f.min} to {f.max}.");}
            else if(f.kind=="bool"){if(!bool.TryParse(v,out _))throw new Exception("Use true or false.");}
            else if(f.kind=="date"){if(v!=""&&(!DateOnly.TryParseExact(v,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||!EncounterDate.IsValidDateNDS(date)))throw new Exception("Use a date from 2000-01-01 through 2099-12-31.");}
            else if(f.kind=="enum"){if(!f.choices.Any(c=>c.value==v)&&v!=f.value)throw new Exception("Choose a listed value.");}
            else if(!decimal.TryParse(v,NumberStyles.Integer,CultureInfo.InvariantCulture,out var n)||n<decimal.Parse(f.min,CultureInfo.InvariantCulture)||n>decimal.Parse(f.max,CultureInfo.InvariantCulture))throw new Exception($"{f.label} must be between {f.min} and {f.max}.");
        }return result;
    }
    void SetExtraProperties(object target,Dictionary<string,string> edits){foreach(var (id,value) in edits)SetProperty(target,id,value);}
    void EditExtra(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Refresh this editor before applying changes.");
        string kind=S(r,"kind"),id=S(r,"id"),mode=S(r,"mode","edit");var page=ReadExtra(kind,id);var row=page.entries.FirstOrDefault(e=>e.id==id);
        if(mode=="edit"&&(row==null||row.fields.Length==0))throw new Exception("Choose an existing entry.");
        bool fileImport=mode=="import"&&page.files;
        if(mode!="edit"&&!fileImport&&!page.actions.Any(a=>a.value==mode)&&row?.actions.Any(a=>a.value==mode)!=true)throw new Exception("This action is not available.");
        var edits=mode=="edit"?ExtraEdits(r,row!):new Dictionary<string,string>();int index=int.TryParse(id,out var parsed)?parsed:-1;
        switch(kind) {
            case "maison":((ISaveBlock6Main)RequireSave()).Maison.SetMaisonStat(int.Parse(id),ushort.Parse(edits["Wins"]));break;
            case "trainerProgress":EditTrainerProgress(id,edits);break;
            case "trainerAlola":EditAlolaTrainer(id,mode,edits);break;
            case "trainerTeams":EditTrainerTeams(id,mode,edits);break;
            case "trainerUnlocks":EditTrainerUnlocks(mode);break;
            case "trainerRecords":((ITrainerStatRecord)RequireSave()).SetRecord(int.Parse(id),int.Parse(edits["Value"]));break;
            case "raids8":case "raids9":case "sevenstar":EditRaids(kind,id,mode,edits);break;
            case "mail":EditMail(id,mode,edits);break;
            case "misc5":EditMisc5(r,id,mode,edits);break;
            case "misc4":EditMisc4(id,mode,edits);break;
            case "misc3":EditMisc3(id,mode,edits);break;
            case "unlocks8b":EditUnlocks8b(mode);break;
            case "gear":EditGear(id,mode,edits);break;
            case "passes":EditPass(r,id,mode,edits);break;
            case "link6":EditLink6(r,id,mode,edits);break;
            case "training6":EditTraining6(id,mode,edits);break;
            case "globallink5":EditGlobalLink(id,edits);break;
            case "dlc4":case "dlc5":EditDownload(r,id,mode,edits);break;
            case "festival":EditFestival(r,id,mode,edits);break;
            case "bases3":case "bases6":EditBases(r,kind,id,mode,edits);break;
            case "pokeathlon":EditSports(id,mode,edits);break;
            case "contacts":EditContacts(id,mode,edits);break;
            case "underground4":EditUnderground4(id,mode,edits);break;
            case "chatter":EditChatter(r,id,mode,edits);break;
            case "underground8":EditUnderground8(id,mode,edits);break;
            case "avenue":EditAvenue(r,id,mode,edits);break;
            case "fame":EditFame(id,mode,edits);break;
            case "geonet":case "unity":EditWorld(id,mode,edits);break;
            case "respawn1":var spawner=new G1OverworldSpawner((SAV1)save!);spawner.GetFlagPairs().Single(p=>p.Name==id).Reset();spawner.Save();break;
            case "gsball":((SAV2)save!).EnableGSBallMobileEvent();break;
            case "clock3":var small=(ISaveBlock3SmallHoenn)((SAV3)save!).SmallBlock;var initial=small.ClockInitial;var elapsed=small.ClockElapsed;if(mode=="reset"){initial.Day=elapsed.Day=0;initial.Hour=initial.Minute=initial.Second=elapsed.Hour=elapsed.Minute=elapsed.Second=0;}else if(mode=="berryfix")elapsed.Day=Math.Max((ushort)734,elapsed.Day);else SetExtraProperties(id=="initial"?initial:elapsed,edits);small.ClockInitial=initial;small.ClockElapsed=elapsed;break;
            case "roamer3":SetExtraProperties(new Roamer3(((SAV3)save!).LargeBlock),edits);break;
            case "roamer6":SetExtraProperties(((SAV6XY)save!).Encount.Roamer,edits);break;
            case "apricorns":var hg=(SAV4HGSS)save!;if(mode=="edit")hg.SetApricornCount(index,int.Parse(edits["Count"]));else for(int i=0;i<7;i++)hg.SetApricornCount(i,mode=="give"?99:0);break;
            case "honey":var sinnoh=(SAV4Sinnoh)save!;var tree=sinnoh.GetHoneyTree(index);if(mode=="ready")tree.Time=1080;else SetExtraProperties(tree,edits);sinnoh.SetHoneyTree(tree,index);break;
            case "opowers":var p=((ISaveBlock6Main)save!).OPower;if(mode=="give")p.UnlockAll();else if(mode=="clear")p.ClearAll();else if(id=="points")SetExtraProperties(p,edits);else{var parts=id.Split(':');var i=int.Parse(parts[1]);foreach(var (field,v) in edits){if(parts[0]=="unlock")p.SetState((OPower6Index)i,bool.Parse(v)?OPowerFlagState.Unlocked:OPowerFlagState.Locked);else if(parts[0]=="field"){if(field=="Level1")p.SetLevel1((OPower6FieldType)i,byte.Parse(v));else p.SetLevel2((OPower6FieldType)i,byte.Parse(v));}else{if(field=="Level1")p.SetLevel1((OPower6BattleType)i,byte.Parse(v));else p.SetLevel2((OPower6BattleType)i,byte.Parse(v));}}}break;
            case "zygarde":var alola=(SAV7)save!;var ew=alola.EventWork;if(mode=="give"){int added=Enumerable.Range(0,ew.TotalZygardeCellCount).Count(i=>ew.GetZygardeCell(i)!=2);ew.ZygardeCellCount=checked((ushort)(ew.ZygardeCellCount+added));if(alola is SAV7SM)ew.ZygardeCellTotal=checked((ushort)(ew.ZygardeCellTotal+added));for(int i=0;i<ew.TotalZygardeCellCount;i++)ew.SetZygardeCell(i,2);}else if(id=="totals")SetExtraProperties(ew,edits);else ew.SetZygardeCell(index,ushort.Parse(edits["State"]));if(alola is SAV7USUM&&(mode=="give"||edits.ContainsKey("ZygardeCellCount")))alola.SetRecord(72,ew.ZygardeCellCount);break;
            case "captures":var lg=(SAV7b)save!;var c=lg.Blocks.Captured;if(mode=="sum"){c.TotalCaptured=c.CalculateTotalCaptured();c.TotalTransferred=c.CalculateTotalTransferred();}else if(mode=="all"){c.SetAllCaptured(c.GetCapturedCountIndex(index),lg.Blocks.Zukan);c.SetAllTransferred(c.GetTransferredCountIndex(index),lg.Blocks.Zukan);}else if(id=="totals")SetExtraProperties(c,edits);else{if(edits.TryGetValue("Captured",out var v))c.SetCapturedCountIndex(index,uint.Parse(v));if(edits.TryGetValue("Transferred",out v))c.SetTransferredCountIndex(index,uint.Parse(v));}break;
            case "stickers":var bs=(SAV8BS)save!;var items=bs.SealList.ReadItems();if(mode=="edit")SetExtraProperties(items.Single(x=>x.Index==index),edits);else foreach(var item in items.Where(x=>page.entries.Any(e=>e.id==x.Index.ToString()))){if(mode=="clear"){item.Count=item.TotalCount=0;item.IsGet=false;}else{item.Count=Math.Clamp(item.Count+SealSticker8b.MaxValue-item.TotalCount,0,SealSticker8b.MaxValue);item.TotalCount=SealSticker8b.MaxValue;item.IsGet=true;}}bs.SealList.WriteItems(items);break;
            case "medals":EditMedals(r,id,mode,edits);break;
            case "habitats":EditHabitats(id,mode,edits);break;
            case "donuts":EditDonuts(r,id,mode,edits);break;
        }
        dirty=true;
    }
}
