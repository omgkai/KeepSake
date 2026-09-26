using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    IEnumerable<IBoxManip> AvailableBoxActions() => BoxManipUtil.ManipCategories.SelectMany(g=>g).Where(a=>a.Usable(RequireSave())).GroupBy(a=>a.Type).Select(g=>g.First());
    object BoxActions() => AvailableBoxActions().Select(a=>new Choice(a.Type.ToString(),Label(a.Type.ToString()))).ToArray();
    object BoxPreview(JsonElement r)
    {
        batchCandidate=null;
        if(pending) throw new Exception("Set or discard Pokémon edits before organizing boxes.");
        var source=RequireSave();
        if(source.BoxCount==0) throw new Exception("This save has no boxes.");
        var action=AvailableBoxActions().FirstOrDefault(a=>a.Type.ToString()==S(r,"action")) ?? throw new Exception("This action is not available for the loaded game.");
        int start=B(r,"all") ? 0 : box, stop=B(r,"all") ? source.BoxCount-1 : box;
        if(source.IsAnySlotLockedInBox(start,stop)) throw new Exception("This region contains locked slots. Choose another box.");
        var candidate=source.Clone();
        action.Execute(candidate,new BoxManipParam(start,stop));
        var changes=new List<object>();
        for(int b=0;b<source.BoxCount;b++) for(int s=0;s<source.BoxSlotCount && b*source.BoxSlotCount+s<source.SlotCount;s++) {
            var before=source.GetBoxSlotAtIndex(b,s);var after=candidate.GetBoxSlotAtIndex(b,s);
            if(before.Data.SequenceEqual(after.Data)) continue;
            if(source.GetBoxSlotFlags(b,s).IsOverwriteProtected()) throw new Exception("The operation would change a protected slot.");
            string details;
            if(before.Species!=after.Species || before.PID!=after.PID) details=$"{(before.Species==0 ? "Empty" : Species(before.Species))} → {(after.Species==0 ? "Empty" : Species(after.Species))}";
            else {
                var previous=EntityFields(before).ToDictionary(f=>f.id,f=>f.value);
                details=string.Join("; ",EntityFields(after).Where(f=>previous.TryGetValue(f.id,out var v) && v!=f.value).Select(f=>$"{f.label}: {previous[f.id]} → {f.value}"));
                if(details.Length==0) details="Pokémon data updated";
            }
            changes.Add(new {location=$"Box {b+1} · slot {s+1}",name=after.Species==0 ? "Empty slot" : Species(after.Species),detail=details});
        }
        batchToken=Guid.NewGuid().ToString("N");batchRevision=revision;
        if(changes.Count>0) batchCandidate=candidate;
        return new {token=batchToken,count=changes.Count,changes,errors=Array.Empty<string>()};
    }
}
