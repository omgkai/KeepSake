using PKHeX.Core;
sealed partial class EditorSession {
    static readonly (uint key,string name,int max)[] HisuiProgress=[
        (SaveBlockAccessor8LA.KMeritCurrent,"Current Merit Points",999999999),
        (SaveBlockAccessor8LA.KMeritEarnedTotal,"Lifetime Merit Points",999999999),
        (SaveBlockAccessor8LA.KExpeditionTeamRank,"Survey Corps rank",999999999),
        (SaveBlockAccessor8LA.KSatchelUpgrades,"Satchel upgrades",999999999)];
    static readonly (uint key,string name,int max)[] GalarProgress=[
        (SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory,"Battle Tower · Singles wins",9999999),
        (SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory,"Battle Tower · Doubles wins",9999999),
        (SaveBlockAccessor8SWSH.KBattleTowerSinglesStreak,"Battle Tower · Singles streak",300),
        (SaveBlockAccessor8SWSH.KBattleTowerDoublesStreak,"Battle Tower · Doubles streak",300)];
    ExtraPage ReadMaison(ExtraTool tool) {
        var s=(ISaveBlock6Main)RequireSave();var rows=Enumerable.Range(0,MaisonBlock.MaisonStatCount).Select(i=>ER(i.ToString(),$"{(BattleStyle6)(i/4)} · {((i&1)!=0?"Super":"Normal")} · {((i&2)!=0?"Best streak":"Current streak")}",[EV("Wins","Wins",s.Maison.GetMaisonStat(i),65535)])).ToArray();return new(tool.id,revision,tool,rows,[]);
    }
    ExtraPage ReadTrainerProgress(ExtraTool tool) {
        var accessor=save is SAV8LA la ? (SCBlockAccessor)la.Blocks:((SAV8SWSH)RequireSave()).Blocks;
        var list=save is SAV8LA?HisuiProgress:GalarProgress;
        return new(tool.id,revision,tool,list.Where(x=>accessor.TryGetBlock(x.key,out var b) && b.Type is SCTypeCode.UInt32 or SCTypeCode.UInt16).Select(x=>ER(x.key.ToString(),x.name,[EV("Value",x.name,accessor.GetBlock(x.key).GetValue(),x.max)])).ToArray(),[]);
    }
    void EditTrainerProgress(string id,Dictionary<string,string> edits) {
        uint key=uint.Parse(id);int value=int.Parse(edits["Value"]);
        var accessor=save is SAV8LA la ? (SCBlockAccessor)la.Blocks:((SAV8SWSH)RequireSave()).Blocks;
        var block=accessor.GetBlock(key);block.SetValue(Convert.ChangeType(value,block.GetValue().GetType()));
        if(save is SAV8SWSH sw){if(key==SaveBlockAccessor8SWSH.KBattleTowerSinglesVictory)sw.SetRecord(RecordLists.G8BattleTowerSingleWin,value);if(key==SaveBlockAccessor8SWSH.KBattleTowerDoublesVictory)sw.SetRecord(RecordLists.G8BattleTowerDoubleWin,value);}
    }
}
