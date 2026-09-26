using PKHeX.Core;
using System.Text.Json;
using System.Security.Cryptography;

sealed partial class EditorSession
{
    static ModifyResult ModifyWithSlotFilters(SlotCache slot,StringInstructionSet set)
    {
        bool IsMeta(StringInstruction f)=>BatchFilters.FilterMeta.Any(m=>m.IsMatch(f.PropertyName));
        var meta=set.Filters.Where(IsMeta).ToArray();
        if(!EntityBatchEditor.IsFilterMatchMeta(meta,slot))return ModifyResult.Skipped;
        return EntityBatchEditor.Instance.TryModify(slot.Entity,set.Filters.Where(f=>!IsMeta(f)),set.Instructions);
    }
    record FolderBatchFile(string Source,string Hash,string Name,byte[] Data);
    FolderBatchFile[] folderBatchFiles=[];
    string folderBatchToken="",folderBatchLibrary="";
    object PreviewFolderBatch(JsonElement r)
    {
        folderBatchFiles=[];folderBatchToken="";
        if(S(r,"token")!=libraryToken || libraryToken.Length==0)throw new Exception("Refresh the file library before batch editing.");
        var ids=r.GetProperty("ids").EnumerateArray().Select(x=>x.GetInt32()).Distinct().ToArray();
        if(ids.Length is <1 or >5000 || ids.Any(i=>i<0 || i>=libraryPokemon.Length))throw new Exception("Choose valid library results.");
        string text=S(r,"text").Trim();
        if(text.Length is 0 or >100000 || StringInstructionSet.HasEmptyLine(text))throw new Exception("Enter batch instructions without blank lines.");
        var sets=StringInstructionSet.GetBatchSets(text.AsSpan());
        if(sets.Length==0 || sets.Any(x=>x.Instructions.Count==0))throw new Exception("Each batch set needs a modification.");
        foreach(var set in sets){EntityBatchEditor.ScreenStrings(set.Filters);EntityBatchEditor.ScreenStrings(set.Instructions);}
        var changes=new List<object>();var errors=new List<string>();var files=new List<FolderBatchFile>();
        foreach(int id in ids) {
            var entry=libraryPokemon[id];
            try {
                var raw=File.ReadAllBytes(entry.FullPath);
                if(Convert.ToHexString(SHA256.HashData(raw))!=entry.Hash)throw new Exception("File changed since the library scan; refresh the library.");
                var pk=entry.Entity.Clone();var before=EntityFields(pk).Where(x=>x.editable).ToDictionary(x=>x.id,x=>x.value);
                bool changed=false;
                foreach(var set in sets) {
                    var result=ModifyWithSlotFilters(new SlotCache(new SlotInfoFileSingle(entry.FullPath),pk),set);
                    if((result & ModifyResult.Error)!=0)throw new Exception("A filter or instruction failed.");
                    changed |= result==ModifyResult.Modified;
                }
                if(!changed)continue;
                pk.RefreshChecksum();var bytes=new byte[pk.SIZE_PARTY];pk.WriteDecryptedDataParty(bytes);
                var verified=EntityFormat.GetFromBytes(bytes,pk.Context);
                if(verified is null || !verified.ChecksumValid)throw new Exception("The edited file did not pass format verification.");
                var diff=EntityFields(pk).Where(x=>x.editable && before.TryGetValue(x.id,out var old) && old!=x.value).Select(x=>$"{x.label}: {before[x.id]} → {x.value}");
                var detail=string.Join("; ",diff);if(detail.Length==0)detail="Stored Pokémon data changed.";
                string name=$"{id:D5}-{Path.GetFileNameWithoutExtension(entry.FullPath)}.{pk.Extension}";
                files.Add(new(entry.FullPath,entry.Hash,name,bytes));
                changes.Add(new {location=entry.RelativePath,name=Species(pk.Species),detail});
            } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException || ex.GetType()==typeof(Exception)) {
                errors.Add($"{entry.RelativePath}: {ex.Message}");
            }
        }
        folderBatchToken=Guid.NewGuid().ToString("N");folderBatchLibrary=libraryToken;
        if(errors.Count==0)folderBatchFiles=files.ToArray();
        return new {token=folderBatchToken,count=changes.Count,changes,errors};
    }
    object ExportFolderBatch(JsonElement r)
    {
        if(S(r,"token")!=folderBatchToken || folderBatchLibrary!=libraryToken || folderBatchFiles.Length==0)throw new Exception("Preview the folder batch again before exporting.");
        foreach(var file in folderBatchFiles)
            if(!File.Exists(file.Source) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.Source)))!=file.Hash)throw new Exception("A source file changed after preview. Refresh the library and preview again.");
        var parent=Path.GetFullPath(S(r,"path"));if(!Directory.Exists(parent))throw new Exception("Choose an existing export folder.");
        var destination=Path.Combine(parent,"KeepSake Batch "+Guid.NewGuid().ToString("N")[..10]);var temporary=destination+".tmp";
        try {
            Directory.CreateDirectory(temporary);
            foreach(var file in folderBatchFiles)File.WriteAllBytes(Path.Combine(temporary,file.Name),file.Data);
            Directory.Move(temporary,destination);
        } finally {if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
        int count=folderBatchFiles.Length;folderBatchFiles=[];folderBatchToken="";
        return new {path=destination,count};
    }
}
