using System.Globalization;
using System.Text;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    record EventRecord(string id, int index, string section, string name, string category, string value, string kind, Choice[] choices, bool named);
    record EventDifference(string id, int index, string section, string name, string before, string after);
    record EventComparison(string token, string before, string after, int unchanged, EventDifference[] entries, string report);
    EventComparison? eventComparison;
    string[] eventComparisonPaths = [];

    static bool SupportsEventResearch(SaveFile s) => EventFlags(s) != null || s is SAV7b or SAV8BS;
    static object EventWorkTarget(SaveFile s) => s switch { IEventFlagProvider37 p => p.EventWork, SAV7b x => x.EventWork, SAV8BS x => x.FlagWork, _ => s };
    static string? EventResource(GameVersion game) => game switch {
        GameVersion.GD or GameVersion.SI or GameVersion.GS => "gs", GameVersion.C => "c",
        GameVersion.R or GameVersion.S or GameVersion.RS => "rs", GameVersion.E => "e", GameVersion.FR or GameVersion.LG or GameVersion.FRLG => "frlg",
        GameVersion.D or GameVersion.P or GameVersion.DP => "dp", GameVersion.Pt or GameVersion.DPPt => "pt", GameVersion.HG or GameVersion.SS or GameVersion.HGSS => "hgss",
        GameVersion.B or GameVersion.W or GameVersion.BW => "bw", GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2 => "b2w2",
        GameVersion.X or GameVersion.Y or GameVersion.XY => "xy", GameVersion.OR or GameVersion.AS or GameVersion.ORAS => "oras",
        GameVersion.SN or GameVersion.MN or GameVersion.SM => "sm", GameVersion.US or GameVersion.UM or GameVersion.USUM => "usum", _ => null
    };
    List<EventRecord> ReadEventRecords(SaveFile sav) {
        var result = new List<EventRecord>();
        var labels = new Dictionary<(string,int),(string name,string category,Choice[] choices)>();
        void Flags(string section,IReadOnlyList<NamedEventValue> list) { foreach(var v in list) labels[(section,v.Index)]=(v.Name,Label(v.Type.ToString()),[]); }
        void Work(IReadOnlyList<NamedEventWork> list) { foreach(var v in list) labels[("Variables",v.Index)]=(v.Name,Label(v.Type.ToString()),v.PredefinedValues.Where(x=>!x.IsCustom).Select(x=>new Choice(x.Value.ToString(CultureInfo.InvariantCulture),x.Name)).ToArray()); }
        void Add(string section,int index,object value) {
            bool named=labels.TryGetValue((section,index),out var label);
            result.Add(new(section+":"+index,index,section,named?label.name:$"{(section=="Variables"?"Variable":section=="System flags"?"System flag":"Flag")} {index}",named?label.category:"Unlabeled",Convert.ToString(value,CultureInfo.InvariantCulture)!.ToLowerInvariant(),value is bool?"bool":value.GetType().Name,named?label.choices:[],named));
        }
        if(sav is SAV8BS bdsp) {
            var t=bdsp.FlagWork;var schema=new EventLabelCollectionSystem("bdsp",t.CountFlag,t.CountSystem,t.CountWork);Flags("Flags",schema.Flag);Flags("System flags",schema.System);Work(schema.Work);
            for(int i=0;i<t.CountFlag;i++)Add("Flags",i,t.GetFlag(i));for(int i=0;i<t.CountSystem;i++)Add("System flags",i,t.GetSystemFlag(i));for(int i=0;i<t.CountWork;i++)Add("Variables",i,t.GetWork(i));
        } else if(sav is SAV7b lgpe) {
            var t=lgpe.EventWork;var schema=new SplitEventEditor<int>(t,GameLanguage.GetStrings("gg","en","const"),GameLanguage.GetStrings("gg","en","flags"));
            foreach(var v in schema.Flag.SelectMany(g=>g.Vars))labels[("Flags",v.RawIndex)]=(v.Name,Label(v.Type.ToString()),[]);
            foreach(var v in schema.Work.SelectMany(g=>g.Vars).Cast<EventWork<int>>())labels[("Variables",v.RawIndex)]=(v.Name,Label(v.Type.ToString()),v.Options.Where(x=>!x.Custom).Select(x=>new Choice(x.Value.ToString(CultureInfo.InvariantCulture),x.Text)).ToArray());
            for(int i=0;i<t.CountFlag;i++)Add("Flags",i,t.GetFlag(i));for(int i=0;i<t.CountWork;i++)Add("Variables",i,t.GetWork(i));
        } else {
            var flags=EventFlags(sav);var work=EventWorkTarget(sav);int count=WorkCount(work);
            if(EventResource(sav.Version) is string resource){var schema=new EventLabelCollection(resource,flags?.EventFlagCount??0,count);Flags("Flags",schema.Flag);Work(schema.Work);}
            for(int i=0;i<(flags?.EventFlagCount??0);i++)Add("Flags",i,flags!.GetEventFlag(i));for(int i=0;i<count;i++)Add("Variables",i,WorkValue(work,i));
        }
        return result;
    }
    object EventResearchRows() => new { revision, supported=SupportsEventResearch(RequireSave()), entries=ReadEventRecords(RequireSave()) };
    void SetEventResearch(JsonElement r) {
        if(N(r,"revision")!=revision)throw new Exception("The save changed. Refresh the event editor.");
        var sav=RequireSave();var row=ReadEventRecords(sav).FirstOrDefault(x=>x.id==S(r,"id"))??throw new Exception("Choose an existing event entry.");string value=S(r,"value");int i=row.index;
        if(row.kind=="bool") {
            bool flag=bool.Parse(value);
            if(sav is SAV8BS bs){if(row.section=="System flags")bs.FlagWork.SetSystemFlag(i,flag);else bs.FlagWork.SetFlag(i,flag);}
            else if(sav is SAV7b gg)gg.EventWork.SetFlag(i,flag);
            else EventFlags(sav)!.SetEventFlag(i,flag);
        } else SetEventWorkValue(EventWorkTarget(sav),i,value);
        if(EventWorkTarget(sav) is EventWork7 alola)alola.UpdateQrConstants();
        dirty=true;
    }
    static void SetEventWorkValue(object target,int id,string value) {
        switch(target) {
            case IEventWorkArray<byte> x:x.SetWork(id,byte.Parse(value,CultureInfo.InvariantCulture));break;
            case IEventWorkArray<ushort> x:x.SetWork(id,ushort.Parse(value,CultureInfo.InvariantCulture));break;
            case IEventWorkArray<int> x:x.SetWork(id,int.Parse(value,CultureInfo.InvariantCulture));break;
            case IEventWorkArray<uint> x:x.SetWork(id,uint.Parse(value,CultureInfo.InvariantCulture));break;
            case IEventWork<int> x:x.SetWork(id,int.Parse(value,CultureInfo.InvariantCulture));break;
            case IEventWork<float> x:var f=float.Parse(value,CultureInfo.InvariantCulture);if(!float.IsFinite(f))throw new Exception("Use a finite number.");x.SetWork(id,f);break;
            default:throw new Exception("Unsupported event variable.");
        }
    }
    static SaveFile ReadEventComparisonSave(string path) {
        var file=new FileInfo(path);if(!file.Exists||file.Length>BlockByteLimit)throw new Exception("Choose a supported save file smaller than 128 MiB.");
        if(!SaveUtil.TryGetSaveFile(path,out var sav))throw new Exception("That file is not a recognized game save.");return sav;
    }
    object CompareEvents(JsonElement r) {
        eventComparison=null;eventComparisonPaths=[];
        string previous=S(r,"previous"),updated=S(r,"updated");
        var old=ReadEventComparisonSave(previous);var next=string.IsNullOrEmpty(updated)?RequireSave():ReadEventComparisonSave(updated);
        if(old.GetType()!=next.GetType() || !SupportsEventResearch(old) || !SupportsEventResearch(next))throw new Exception("Choose saves from the same supported game family.");
        var before=ReadEventRecords(old);var after=ReadEventRecords(next);if(before.Count!=after.Count)throw new Exception("These saves use different event layouts.");
        var prior=before.ToDictionary(x=>x.id);var differences=new List<EventDifference>();int unchanged=0;
        foreach(var row in after){if(!prior.TryGetValue(row.id,out var was)||was.kind!=row.kind)throw new Exception("These saves use different event layouts.");if(was.value==row.value){unchanged++;continue;}differences.Add(new(row.id,row.index,row.section,row.name,was.value,row.value));}
        string left=Path.GetFileName(previous),right=string.IsNullOrEmpty(updated)?"Current workspace":Path.GetFileName(updated);
        var report=new StringBuilder($"KeepSake event comparison\n{GameInfo.GetVersionName(old.Version)} → {GameInfo.GetVersionName(next.Version)}\nBefore: {left}\nAfter: {right}\n{differences.Count} changed · {unchanged} unchanged\n\n");
        foreach(var row in differences)report.AppendLine($"{row.section} {row.index}: {row.name}\n  {row.before} → {row.after}");
        eventComparisonPaths=new[]{previous,updated}.Where(x=>!string.IsNullOrEmpty(x)).Select(Path.GetFullPath).ToArray();return eventComparison=new(Guid.NewGuid().ToString("N"),left,right,unchanged,differences.ToArray(),report.ToString());
    }
    object ExportEventComparison(JsonElement r) {
        var review=eventComparison;if(review==null||review.token!=S(r,"token"))throw new Exception("Run the comparison again before exporting its report.");
        string path=ExportPath(S(r,"path"),sourcePath);foreach(var source in eventComparisonPaths)path=ExportPath(path,source);
        AtomicWrite(path,Encoding.UTF8.GetBytes(review.report));return new{path};
    }
}
