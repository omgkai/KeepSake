using PKHeX.Core;

sealed partial class EditorSession
{
    record TrainerBadge(int id,string name,bool earned,string group,string artwork,string symbol="seal.fill",bool recorded=true);
    TrainerBadge[] TrainerBadges()
    {
        string[] kanto=["Boulder","Cascade","Thunder","Rainbow","Soul","Marsh","Volcano","Earth"];
        string[] johto=["Zephyr","Hive","Plain","Fog","Storm","Mineral","Glacier","Rising"];
        string[] hoenn=["Stone","Knuckle","Dynamo","Heat","Balance","Feather","Mind","Rain"];
        string[] sinnoh=["Coal","Forest","Cobble","Fen","Relic","Mine","Icicle","Beacon"];
        string[] names=[];int bits=0;string region="";
        switch(save) {
            case SAV1 s:names=kanto;bits=s.Badges;region="kanto";break;
            case SAV2 s:names=[..johto,..kanto];bits=s.Badges;region="johto";break;
            case SAV3 s:names=s is SAV3FRLG ? kanto:hoenn;bits=s.Badges;region=s is SAV3FRLG ? "kanto":"hoenn";break;
            // Badges16 is the second byte (Kanto), not a combined 16-bit value.
            case SAV4HGSS s:names=[..johto,..kanto];bits=s.Badges|(s.Badges16<<8);region="johto";break;
            case SAV4 s:names=sinnoh;bits=s.Badges;region="sinnoh";break;
            case SAV5 s:names=s is SAV5B2W2 ? ["Basic","Toxic","Insect","Bolt","Quake","Jet","Legend","Wave"]:["Trio","Basic","Insect","Bolt","Quake","Jet","Freeze","Legend"];bits=s.Misc.Badges;region="unova";break;
            case SAV6 s:names=s is SAV6AO ? hoenn:["Bug","Cliff","Rumble","Plant","Voltage","Fairy","Psychic","Iceberg"];bits=s.Badges;region=s is SAV6AO ? "hoenn":"kalos";break;
            case SAV7 s:
                return Enum.GetValues<Stamp7>().Select((stamp,i)=>new TrainerBadge(i,StampName(stamp),(s.Misc.Stamps&(1u<<i))!=0,"Passport stamps","",i>=6&&i<=10?"book.closed.fill":i>=11&&i<=13?"trophy.fill":i==14?"camera.fill":"sun.max.fill")).ToArray();
            case SAV8SWSH s:names=["Grass","Water","Fire",s.Version==GameVersion.SW ? "Fighting":"Ghost","Fairy",s.Version==GameVersion.SW ? "Rock":"Ice","Dark","Dragon"];bits=s.Badges;region="galar";break;
            case SAV8BS s:names=sinnoh;region="sinnoh";for(int i=0;i<8;i++) if(s.FlagWork.GetSystemFlag(124+i)) bits|=1<<i;break;
            case SAV8LA s:
                var rankBlock=s.Blocks.GetBlock(SaveBlockAccessor8LA.KExpeditionTeamRank);
                uint rank=rankBlock.Type==SCTypeCode.UInt32 ? (uint)rankBlock.GetValue():0;
                return Enumerable.Range(1,10).Select(i=>new TrainerBadge(i,$"{i} star",rank>=i,"Survey Corps rank","","star.fill",rankBlock.Type==SCTypeCode.UInt32)).ToArray();
            case SAV9SV s:return PaldeaBadges(s);
        }
        return names.Select((name,i)=>{
            var place=region=="johto"&&i>=8?"kanto":region;
            return new TrainerBadge(i,name,(bits&(1<<i))!=0,Label(place)+" badges",place+"-"+name.ToLowerInvariant());
        }).ToArray();
    }
    record TrainerJourney(string title,string value,string symbol);
    TrainerJourney[] JourneyProgress()=>save switch {
        SAV7b s=>[new("Pokémon caught",s.Blocks.Captured.TotalCaptured.ToString("N0"),"scope"),new("Sent to Oak",s.Blocks.Captured.TotalTransferred.ToString("N0"),"leaf.fill")],
        SAV9ZA s when s.Blocks.InfiniteRoyale.Data.Length>=16=>[new("Infinite Royale wins",s.Blocks.InfiniteRoyale.Wins.ToString("N0"),"trophy.fill"),new("Prize medals",s.Blocks.InfiniteRoyale.PrizeMedals.ToString("N0"),"medal.fill")],
        _=>[]
    };
    static string StampName(Stamp7 stamp)=>stamp switch {
        Stamp7.OfficialPokemonTrainer=>"Official trainer",Stamp7.MelemeleTrialCompletion=>"Melemele trials",Stamp7.AkalaTrialCompletion=>"Akala trials",Stamp7.UlaulaTrialCompletion=>"Ulaʻula trials",Stamp7.PoniTrialCompletion=>"Poni trials",Stamp7.IslandChallengeCompletion=>"Island challenge",Stamp7.MelemelePokedexCompletion=>"Melemele Pokédex",Stamp7.AkalaPokedexCompletion=>"Akala Pokédex",Stamp7.UlaulaPokedexCompletion=>"Ulaʻula Pokédex",Stamp7.PoniPokedexCompletion=>"Poni Pokédex",Stamp7.AlolaPokedexCompletion=>"Alola Pokédex",Stamp7.ConsecutiveSingleBattleWins50=>"50 Single wins",Stamp7.ConsecutiveDoubleBattleWins50=>"50 Double wins",Stamp7.ConsecutiveMultiBattleWins50=>"50 Multi wins",_=>"Poké Finder Pro"
    };
    static TrainerBadge[] PaldeaBadges(SAV9SV s) {
        // PKHeX's WEVT_*_CLEAR blocks store receipt order 0–17, not booleans.
        // Duplicate/default ordinals are ambiguous and must never imply earned badges.
        uint[] receipts=[0x89306FE6,0xB4C3AFE6,0x8205ECAD,0xA803FAAD,0xF90EFD79,0xCDA61DED,0x3B819021,0x46B6CB30,0xEC7361B7,0xA6CDE603,0x9C16DA94,0x0D0602DE,0xBDAC74B3,0x9C6FF7DD,0x6C29ACC5,0xE1271327,0x2A3AC89A,0x71DB2CEB];
        uint[] gymFlags=[0x8485AC3A,0x26216312,0x815F4601,0x16452421,0x9457390D,0x1C5C88A5,0xD0249A05,0x0A9299BC];
        long?[] order=receipts.Select(k=>s.Blocks.TryGetBlock(k,out var b)&&b.Type is SCTypeCode.Int32 or SCTypeCode.UInt32 ? (long?)Convert.ToInt64(b.GetValue()):null).ToArray();
        string[] types=["Bug","Grass","Electric","Water","Normal","Ghost","Psychic","Ice","Dragon","Rock","Flying","Steel","Ground","Fire","Dark","Fairy","Fighting","Poison"];
        string[] labels=["Cortondo","Artazon","Levincia","Cascarrafa","Medali","Montenevera","Alfornada","Glaseado","False Dragon","Stony Cliff","Open Sky","Lurking Steel","Quaking Earth","Schedar","Segin","Ruchbah","Caph","Navi"];
        return types.Select((type,i)=>{
            var value=order[i];bool unique=value is >=0 and <18&&order.Count(v=>v==value)==1;
            bool known=value.HasValue&&(unique||value is <0 or >=18),earned=unique;
            // Map markers are a fallback. A hidden/reset marker must not erase
            // an actual badge receipt, including after the story is completed.
            if(i<8&&s.Blocks.TryGetBlock(gymFlags[i],out var flag)&&flag.Type is SCTypeCode.Bool1 or SCTypeCode.Bool2){known=true;earned|=flag.Type==SCTypeCode.Bool2;}
            string group=i<8?"Gym badges":i<13?"Titan badges":"Team Star badges",key=i<8?"gym":i<13?"titan":"star";
            return new TrainerBadge(i,$"{type} · {labels[i]}",earned,group,$"paldea-{key}-{type.ToLowerInvariant()}",recorded:known);
        }).ToArray();
    }
}
