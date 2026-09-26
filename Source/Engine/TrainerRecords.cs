using PKHeX.Core;

sealed partial class EditorSession
{
    ExtraPage ReadTrainerRecords(ExtraTool tool)
    {
        var sav=RequireSave();var records=sav as ITrainerStatRecord ?? throw new Exception("Trainer records are unavailable.");
        var labels=sav.Generation switch {5=>RecordLists.RecordList_5,6=>RecordLists.RecordList_6,7=>RecordLists.RecordList_7,_=>RecordLists.RecordList_8};
        var rows=Enumerable.Range(0,records.RecordCount).Select(i=>ER(i.ToString(),labels.TryGetValue(i,out var label)?label:$"Record {i}",
            [EV("Value","Recorded value",records.GetRecord(i),Math.Max(records.GetRecord(i),records.GetRecordMax(i)))],$"Record {i} · offset 0x{records.GetRecordOffset(i):X}")).ToArray();
        return new(tool.id,revision,tool,rows,[]);
    }
}
