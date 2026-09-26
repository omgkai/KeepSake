using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    record BlockTransfer(uint key, byte[] data, SCTypeCode? type = null, SCTypeCode? subType = null);
    record BlockDifference(string id, string name, string status, string before, string after, int changedBytes, int firstOffset, bool importable);
    record BlockReview(string token, int revision, string source, bool comparison, int unchanged, BlockDifference[] entries, string[] issues, bool canApply);
    BlockTransfer[]? blockImport;
    string? blockImportToken;
    int blockImportRevision;
    const long BlockByteLimit = 128L * 1024 * 1024;
    static bool BooleanBlock(SCTypeCode type) => type is SCTypeCode.Bool1 or SCTypeCode.Bool2;
    static bool BlockKey(string name, out uint key) {
        string stem = Path.GetFileNameWithoutExtension(name).Split(' ')[0];
        return uint.TryParse(stem.Length == 8 ? stem : "", NumberStyles.HexNumber, CultureInfo.InvariantCulture, out key);
    }
    static string BlockSummary(SCBlock block) => $"{block.Type}{(block.SubType != 0 ? "/" + block.SubType : "")} · {block.Data.Length:N0} bytes" + (BlockValue(block) is { Length: > 0 } value ? " · " + value : "");
    static (int count, int first) ChangedBytes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after) {
        int count = Math.Abs(before.Length-after.Length), first = -1;
        for (int i=0;i<Math.Min(before.Length,after.Length);i++) if(before[i]!=after[i]) {count++;if(first<0)first=i;}
        if(first<0 && before.Length!=after.Length)first=Math.Min(before.Length,after.Length);
        return (count,first);
    }
    BlockReview ReviewBlocks(JsonElement r) {
        blockImport=null;blockImportToken=null;
        var blocks=BlockSave();var current=blocks.AllBlocks.ToDictionary(b=>b.Key);
        var metadata=new SCBlockMetadata(blocks.Accessor,[]);
        string Name(SCBlock b)=>metadata.GetBlockName(b,out _)??"Unnamed block";
        string path=Path.GetFullPath(S(r,"path"));string mode=S(r,"mode");
        var issues=new List<string>();var rows=new List<BlockDifference>();var changes=new List<BlockTransfer>();int unchanged=0;
        if(mode=="compare") {
            var file=new FileInfo(path);
            if(!file.Exists || file.Length>BlockByteLimit)throw new Exception("Choose a supported save file smaller than 128 MiB.");
            if(!SaveUtil.TryGetSaveFile(path,out var other) || other is not ISCBlockArray incoming || other.GetType()!=RequireSave().GetType())throw new Exception("Choose another save from the same game family.");
            foreach(var b in incoming.AllBlocks) {
                if(!current.TryGetValue(b.Key,out var old)){rows.Add(new(b.Key.ToString("X8"),"New block","Added","Absent",BlockSummary(b),b.Data.Length,0,false));continue;}
                var diff=ChangedBytes(old.Data,b.Data);
                var status=old.Type!=b.Type||old.SubType!=b.SubType?"Type changed":old.Data.Length!=b.Data.Length?"Size changed":diff.count>0?"Changed":"";
                if(status.Length==0){unchanged++;continue;}
                rows.Add(new(b.Key.ToString("X8"),Name(old),status,BlockSummary(old),BlockSummary(b),diff.count,diff.first,false));
            }
            var keys=incoming.AllBlocks.Select(b=>b.Key).ToHashSet();
            foreach(var b in current.Values.Where(b=>!keys.Contains(b.Key)))rows.Add(new(b.Key.ToString("X8"),Name(b),"Removed",BlockSummary(b),"Absent",b.Data.Length,0,false));
        } else {
            if(mode is not ("folder" or "archive"))throw new Exception("Choose a block folder, KeepSake archive or save comparison.");
            var imports=mode=="folder"?ReadBlockFolder(path,issues):ReadBlockArchive(path,issues);
            foreach(var item in imports) {
                string key=item.key.ToString("X8");
                if(!current.TryGetValue(item.key,out var old)){issues.Add(key+": not present in this save.");continue;}
                bool boolPair=item.type.HasValue&&BooleanBlock(old.Type)&&BooleanBlock(item.type.Value);
                if(item.data.Length!=old.Data.Length){issues.Add(key+$": expected {old.Data.Length} bytes; received {item.data.Length}.");continue;}
                if(item.type.HasValue && !boolPair && (item.type!=old.Type || item.subType!=old.SubType)){issues.Add(key+": stored type differs from this save.");continue;}
                if(old.Data.Length==0 && !boolPair){unchanged++;continue;}
                var diff=ChangedBytes(old.Data,item.data);bool typeChanged=boolPair&&item.type!=old.Type;
                if(diff.count==0&&!typeChanged){unchanged++;continue;}
                changes.Add(item);
                rows.Add(new(key,Name(old),typeChanged?"Flag changed":"Changed",BlockSummary(old),boolPair?item.type==SCTypeCode.Bool2?"Enabled":"Disabled":$"{item.data.Length:N0} bytes · {diff.count:N0} changed",diff.count,diff.first,true));
            }
        }
        var token=Guid.NewGuid().ToString("N");bool canApply=mode!="compare"&&issues.Count==0&&changes.Count>0&&!pending;
        if(pending && mode!="compare")issues.Add("Set or discard the edited Pokémon before importing save blocks.");
        if(canApply){blockImport=changes.ToArray();blockImportToken=token;blockImportRevision=revision;}
        return new(token,revision,Path.GetFileName(path),mode=="compare",unchanged,rows.OrderBy(x=>x.id).ToArray(),issues.ToArray(),canApply);
    }
    static BlockTransfer[] ReadBlockFolder(string path,List<string> issues) {
        if(!Directory.Exists(path))throw new Exception("Choose a folder containing exported .bin blocks.");
        var rows=new List<BlockTransfer>();var keys=new HashSet<uint>();long total=0;int count=0;
        foreach(var file in Directory.EnumerateFiles(path).Where(f=>Path.GetExtension(f).Equals(".bin",StringComparison.OrdinalIgnoreCase))) {
            if(++count>100000)throw new Exception("This folder contains too many blocks.");
            var info=new FileInfo(file);
            if(!BlockKey(info.Name,out var key)){issues.Add(info.Name+": use an eight-digit hexadecimal block filename.");continue;}
            if(!keys.Add(key)){issues.Add(info.Name+": duplicate block key.");continue;}
            total+=info.Length;if(total>BlockByteLimit)throw new Exception("Block data exceeds 128 MiB.");
            rows.Add(new(key,File.ReadAllBytes(file)));
        }
        if(count==0)issues.Add("No .bin blocks were found in this folder.");
        return rows.ToArray();
    }
    static BlockTransfer[] ReadBlockArchive(string path,List<string> issues) {
        if(new FileInfo(path).Length>BlockByteLimit)throw new Exception("Choose a block archive smaller than 128 MiB.");
        using var zip=ZipFile.OpenRead(path);
        if(zip.Entries.Count>100001 || zip.Entries.Sum(e=>e.Length)>BlockByteLimit)throw new Exception("Expanded block archive exceeds the supported limits.");
        var manifests=zip.Entries.Where(e=>e.FullName=="blocks.json").ToArray();
        if(manifests.Length!=1 || manifests[0].Length>16*1024*1024)throw new Exception("Use a KeepSake block archive containing one blocks.json manifest.");
        using var stream=manifests[0].Open();using var doc=JsonDocument.Parse(stream);
        var metadata=new Dictionary<uint,(SCTypeCode type,SCTypeCode sub,int length)>();
        foreach(var entry in doc.RootElement.EnumerateArray()) {
            if(!uint.TryParse(entry.GetProperty("key").GetString(),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var key)||!Enum.TryParse<SCTypeCode>(entry.GetProperty("type").GetString(),out var type)||!Enum.IsDefined(type)||!Enum.TryParse<SCTypeCode>(entry.GetProperty("subType").GetString(),out var sub)||!Enum.IsDefined(sub)||!metadata.TryAdd(key,(type,sub,entry.GetProperty("length").GetInt32())))throw new Exception("The block manifest has invalid or duplicate entries.");
        }
        var rows=new List<BlockTransfer>();var keys=new HashSet<uint>();
        foreach(var entry in zip.Entries.Where(e=>e.FullName!="blocks.json")) {
            if(entry.FullName!=entry.Name || !entry.Name.EndsWith(".bin",StringComparison.OrdinalIgnoreCase) || !BlockKey(entry.Name,out var key)){issues.Add(entry.FullName+": unrecognized archive entry.");continue;}
            if(!keys.Add(key)){issues.Add(entry.Name+": duplicate block key.");continue;}
            if(!metadata.TryGetValue(key,out var meta)||entry.Length!=meta.length){issues.Add(entry.Name+": missing or inconsistent manifest entry.");continue;}
            using var input=entry.Open();var bytes=new byte[checked((int)entry.Length)];input.ReadExactly(bytes);if(input.ReadByte()!=-1)throw new Exception("A block expands beyond its declared size.");rows.Add(new(key,bytes,meta.type,meta.sub));
        }
        foreach(var key in metadata.Keys.Where(k=>!keys.Contains(k)))issues.Add($"{key:X8}: block file is missing.");
        if(metadata.Count==0)issues.Add("The archive contains no blocks.");
        return rows.ToArray();
    }
    void ApplyBlockReview(JsonElement r) {
        if(blockImport==null || S(r,"token")!=blockImportToken || N(r,"revision")!=revision || blockImportRevision!=revision || pending)throw new Exception("This review is no longer current. Preview the import again.");
        var blocks=BlockSave().AllBlocks.ToDictionary(b=>b.Key);
        foreach(var item in blockImport){var b=blocks[item.key];if(item.type.HasValue&&BooleanBlock(item.type.Value))b.ChangeBooleanType(item.type.Value);else b.ChangeData(item.data);}
        dirty=true;ParseSettings.InitFromSaveFileData(RequireSave());
        if(slot>=0)Select(box,slot,party);
        blockImport=null;blockImportToken=null;
    }
    object ExportRawBlocks(JsonElement r) {
        var options=(SCBlockExportOption)N(r,"options");
        if(((int)options&~15)!=0)throw new Exception("Unknown raw block export options.");
        var data=SCBlockUtil.ExportAllBlocks(BlockSave().AllBlocks,options);
        var path=ExportPath(S(r,"path"),sourcePath);AtomicWrite(path,data);return new{path};
    }
}
