using PKHeX.Core;
sealed partial class EditorSession {
    ExtraPage ReadAlolaTrainer(ExtraTool tool) {
        var s=(SAV7)RequireSave();var rows=new List<ExtraRow>();int offset=s is SAV7USUM?4160:3200;
        var names=GameInfo.GetLocationList(GameVersion.US,EntityContext.Gen7,false);
        var flyNames=(int[])FlyDestNameIndex.Clone();if(s.Version is GameVersion.UM or GameVersion.MN){flyNames[28]=142;flyNames[36]=178;}
        foreach(var kind in new[]{"fly","map"}) {
            var ids=kind=="fly"?flyNames:MapUnmaskNameIndex;var flags=kind=="fly"?FlyDestFlagOfs:MapUnmaskFlagOfs;
            string[] alt=kind=="fly"?["My House","Photo Club (Hau'oli)","Photo Club (Konikoni)"]:["Melemele Sea (East)","Melemele Sea (West)"];
            int count=ids.Length-(s is SAV7USUM?0:kind=="fly"?6:4),a=0;
            for(int i=0;i<count;i++){var label=ids[i]<0?alt[a++]:names.FirstOrDefault(n=>n.Value==ids[i])?.Text??$"Location {ids[i]}";rows.Add(ER($"{kind}:{flags[i]}",label,[EV("Enabled",kind=="fly"?"Fly destination unlocked":"Visible on map",s.EventWork.GetEventFlag(offset+flags[i]))]) with{category=kind=="fly"?"Fly destinations":"Map visibility"});}
        }
        for(int battle=0;battle<3;battle++)foreach(bool super in new[]{false,true})foreach(bool best in new[]{false,true}) {
            string label=$"{(super?"Super ":"")}{new[]{"Singles","Doubles","Multi"}[battle]} · {(best?"Best":"Current")}";
            rows.Add(ER($"tree:{battle}:{(super?1:0)}:{(best?1:0)}",label,[EV("Streak","Wins",s.BattleTree.GetTreeStreak(battle,super,best),65535)]) with{category="Battle Tree"});
        }
        foreach(var flag in new[]{333,334,335})rows.Add(ER($"flag:{flag}","Super "+new[]{"Singles","Doubles","Multi"}[flag-333],[EV("Enabled","Unlocked",s.EventWork.GetEventFlag(flag))]) with{category="Battle Tree"});
        if(s is SAV7SM)foreach(var style in Enum.GetValues<PlayerBattleStyle7>().Where(x=>x!=PlayerBattleStyle7.Nihilist)) {
            int i=(int)style;var fields=new List<ExtraValue>();if(i>=2)fields.Add(EV("Unlocked","Unlocked",s.EventWork.GetEventFlag(292+i)));if(i>=1)fields.Add(EV("Learned","Learned",s.EventWork.GetEventFlag(3479+i)));
            rows.Add(ER($"throw:{i}",Label(style.ToString()),fields.ToArray(),i==0?"The default style is always learned.":"Unlocking and learning are separate game records.") with{category="Throw styles"});
        }
        if(s is SAV7USUM)for(int i=0;i<4;i++)rows.Add(ER($"surf:{i}",new[]{"Melemele","Akala","Ula’ula","Poni"}[i]+" Surf score",[EV("Score","Best score",s.Misc.GetSurfScore(i),int.MaxValue)]) with{category="Mantine Surf"});
        return new(tool.id,revision,tool,rows.ToArray(),[new("flyAll","Unlock All Fly Destinations"),new("mapAll","Reveal All Map Areas")]);
    }
    void EditAlolaTrainer(string id,string mode,Dictionary<string,string> edits) {
        var s=(SAV7)RequireSave();int offset=s is SAV7USUM?4160:3200;
        if(mode is "flyAll" or "mapAll") {var flags=mode=="flyAll"?FlyDestFlagOfs:MapUnmaskFlagOfs;int count=flags.Length-(s is SAV7USUM?0:mode=="flyAll"?6:4);for(int i=0;i<count;i++)s.EventWork.SetEventFlag(offset+flags[i],true);return;}
        var p=id.Split(':');int index=int.Parse(p[1]);if(p[0]=="throw"){foreach(var (key,value) in edits)s.EventWork.SetEventFlag((key=="Unlocked"?292:3479)+index,bool.Parse(value));}else if(p[0]=="surf")s.Misc.SetSurfScore(index,int.Parse(edits["Score"]));else if(p[0]=="tree")s.BattleTree.SetTreeStreak(int.Parse(edits["Streak"]),index,p[2]=="1",p[3]=="1");else s.EventWork.SetEventFlag((p[0]=="flag"?0:offset)+index,bool.Parse(edits["Enabled"]));
    }
    static readonly int[] FlyDestNameIndex = [
            -1,24,34,8,20,38,12,46,40,30,//Melemele
            70,68,78,86,74,104,82,58,90,72,76,92,62,//Akala
            132,136,138,114,118,144,130,154,140,//Ula'ula
            172,184,180,174,176,156,186,//Poni
            188,-1,-1,
            198,202,110,204,//Beach
        ];
    static readonly int[] FlyDestFlagOfs = [
            44,43,45,40,41,49,42,47,46,48,
            50,54,39,57,51,55,59,52,58,53,61,60,56,
            62,66,67,64,65,273,270,37,38,
            69,74,72,71,276,73,70,
            75,332,334,
            331,333,335,336,
        ];
    static readonly int[] MapUnmaskNameIndex = [
            6,8,24,-1,18,-1,20,22,12,10,14,
            70,50,68,52,74,54,56,58,60,72,62,64,
            132,192,106,108,122,112,114,126,116,118,120,154,
            172,158,160,162,164,166,168,170,
            188,
            198,202,110,204,
        ];
    static readonly int[] MapUnmaskFlagOfs = [
            5,76,82,91,79,84,80,81,77,78,83,
            19,10,18,11,21,12,13,14,15,20,16,17,
            33,34,30,31,98,92,93,94,95,96,97,141,
            173,144,145,146,147,148,149,172,
            181,
            409,297,32,296,
        ];
}
