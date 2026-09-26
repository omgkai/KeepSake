using PKHeX.Core;
using System.Text.Json;

sealed partial class EditorSession {
    ExtraPage ReadDownloads(ExtraTool tool,string? selected) {
        var rows=new List<ExtraRow>();
        void Add(string id,string name,string ext,string detail,ExtraValue[]? fields=null)=>rows.Add(ER(id,name,fields??[],detail) with {fileExtension=ext});
        if(save is SAV4 s4) {
            for(int i=0;i<4;i++) {var original=s4.GetBattleVideo(i);if(original==null){rows.Add(ER("video:"+i,$"Battle video {i+1}",[],"This save has no initialized video block here"));continue;}
                var video=new BattleVideo4(original.Data.ToArray());string detail="Battle video · "+(video.ChecksumValid?"Checksum valid":"Checksum invalid");
                Add("video:"+i,$"Battle video {i+1}",BattleVideo4.Extension,detail);
                if(selected=="video:"+i){video.Decrypt();var teams=video.GetTeams();var names=video.GetTrainerNames();var fs=new List<ExtraValue>();for(int t=0;t<teams.Length;t++){string team=string.Join(", ",teams[t].Select(p=>Species(p.Species)+$" · Lv. {p.CurrentLevel}"));fs.Add(new("team:"+t,$"Player {t+1} · {names[t]}",team,"readonly","0","0",[]));}rows[^1]=rows[^1] with {fields=fs.ToArray()};}
            }
        } else {
            var s=(SAV5)RequireSave();
            Add("cgear","C-Gear background",s is SAV5BW?CGearBackgroundBW.Extension:CGearBackgroundB2W2.Extension,"256 × 192 pixels · 16 colors · PNG or game-format files");
            Add("dexskin","Pokédex skin",PokeDexSkin5.Extension,"Import or export the saved skin file");
            Add("musical","Musical show",MusicalShow5.Extension,s.Musical.MusicalName,[ET("MusicalName","Show name",s.Musical.MusicalName,Musical5.MusicalNameMaxLength)]);
            Add("link1","Memory Link · first block","ml5","Import or export the first Memory Link record");Add("link2","Memory Link · second block","ml5","Import or export the second Memory Link record");
            Add("test","Battle test",BattleTest5.Extension,"Advanced file transfer; in-game support is unresolved in upstream PKHeX");
            for(int i=0;i<4;i++){var v=new BattleVideo5(s.GetBattleVideo(i).ToArray());Add("video:"+i,$"Battle video {i+1}",BattleVideo5.Extension,v.IsUninitialized?"Empty":v.GetTrainerNames());}
            if(s is SAV5B2W2 b){for(int i=0;i<SAV5B2W2.PWTCount;i++)Add("pwt:"+i,$"World Tournament {i+1}",WorldTournament5.Extension,new WorldTournament5(b.GetPWT(i).ToArray()).Name);for(int i=0;i<SAV5B2W2.PokestarCount;i++)Add("movie:"+i,$"Pokéstar movie {i+1}",PokestarMovie5.Extension,new PokestarMovie5(b.GetPokestarMovie(i).ToArray()).Name);}
        }
        return new(tool.id,revision,tool,rows.ToArray(),[],true);
    }
    byte[] DownloadBytes(string id,bool decrypted=false) {
        if(save is SAV4 s4){var v=s4.GetBattleVideo(DownloadIndex(id,"video",4))??throw new Exception("This video block is not initialized.");var copy=new BattleVideo4(v.Data.ToArray());if(decrypted)copy.Decrypt();return copy.Data.ToArray();}
        var s=(SAV5)RequireSave();byte[] data;
        if(id.StartsWith("video:")){data=s.GetBattleVideo(DownloadIndex(id,"video",4)).ToArray();if(decrypted){var v=new BattleVideo5(data);if(!v.IsUninitialized)v.Decrypt();}return data;}
        return id switch {"cgear"=>s.CGearSkinData.ToArray(),"dexskin"=>s.PokedexSkinData.ToArray(),"musical"=>s.MusicalDownloadData.ToArray(),"test"=>s.BattleTest.ToArray(),"link1"=>s.Link1Data.ToArray(),"link2"=>s.Link2Data.ToArray(),_ when id.StartsWith("pwt:")&&s is SAV5B2W2 b=>b.GetPWT(DownloadIndex(id,"pwt",SAV5B2W2.PWTCount)).ToArray(),_ when id.StartsWith("movie:")&&s is SAV5B2W2 b=>b.GetPokestarMovie(DownloadIndex(id,"movie",SAV5B2W2.PokestarCount)).ToArray(),_=>throw new Exception("Choose a supported content slot.")};
    }
    static int DownloadIndex(string id,string prefix,int count){var p=id.Split(':');if(p.Length!=2||p[0]!=prefix||!int.TryParse(p[1],out int n)||n<0||n>=count)throw new Exception("Choose a valid content slot.");return n;}
    void EditDownload(JsonElement r,string id,string mode,Dictionary<string,string> edits) {
        if(mode=="edit"){SetExtraProperties(((SAV5)RequireSave()).Musical,edits);return;}
        string path=S(r,"path");int size=DownloadBytes(id).Length;long length=new FileInfo(path).Length;
        int alternate=id.StartsWith("pwt:")?0x1314:id=="musical"?0x17D78:id=="dexskin"?0x6200:-1;
        if(length!=size&&length!=alternate)throw new Exception($"This content requires {size:N0} bytes"+(alternate>0?$" (or {alternate:N0} for the archive format)":"")+".");
        var data=File.ReadAllBytes(path);if(data.Length!=size)Array.Resize(ref data,size);
        if(save is SAV4 s4){int i=DownloadIndex(id,"video",4);var target=s4.GetBattleVideo(i)??throw new Exception("The video block is not initialized.");var state=BattleVideo4.DetectEncryption(data);if(!BattleVideo4.IsValid(data)||state==BattleVideo4DecryptionState.Invalid)throw new Exception("This is not a valid Generation 4 battle video.");var v=new BattleVideo4(data){IsDecrypted=state==BattleVideo4DecryptionState.Decrypted,Key=target.Key,Magic=target.Magic,Revision=target.Revision,BlockSize=target.BlockSize,BlockID=target.BlockID};v.RefreshChecksums();v.Encrypt();v.Data.CopyTo(target.Data);return;}
        var s=(SAV5)RequireSave();
        if(id.StartsWith("video:")){var v=new BattleVideo5(data){IsDecrypted=BattleVideo5.GetIsDecrypted(data)};v.Encrypt();if(!v.IsUninitialized)v.RefreshChecksums();s.SetBattleVideo(DownloadIndex(id,"video",4),data);}
        else if(id.StartsWith("pwt:"))((SAV5B2W2)s).SetPWT(DownloadIndex(id,"pwt",SAV5B2W2.PWTCount),data);
        else if(id.StartsWith("movie:"))((SAV5B2W2)s).SetPokestarMovie(DownloadIndex(id,"movie",SAV5B2W2.PokestarCount),data);
        else switch(id){
            case "cgear":CGearBackground bg=s is SAV5BW?new CGearBackgroundBW(data):new CGearBackgroundB2W2(data);bool shifted=PaletteTileSelection.IsPaletteShiftFormat(bg.Arrange);if(s is SAV5BW&&!shifted)PaletteTileSelection.ConvertToShiftFormat<CGearBackgroundBW>(bg.Arrange);else if(s is SAV5B2W2&&shifted)PaletteTileSelection.ConvertFromShiftFormat(bg.Arrange);if(!bg.IsUninitialized)_=bg.GetImageData();s.SetCGearSkin(bg.Data);break;
            case "dexskin":s.SetPokeDexSkin(data);break;
            case "musical":s.SetMusical(data);s.Musical.MusicalName=new MusicalShow5(data).IsUninitialized?"":ImportedMusicalName(path);break;
            case "link1":s.SetLink1Data(data);break;
            case "link2":s.SetLink2Data(data);break;
            case "test":var t=new BattleTest5(data);if(!t.IsUninitialized){t.Magic=BattleTest5.Sentinel;t.RefreshChecksums();}s.SetBattleTest(data);break;
        }
    }
    static string ImportedMusicalName(string path){string name=Path.GetFileNameWithoutExtension(path).Trim();int split=name.LastIndexOf(" - ",StringComparison.Ordinal);if(split>=0&&split+3<name.Length)name=name[(split+3)..].Trim();int suffix=name.LastIndexOf(" (",StringComparison.Ordinal);if(suffix>0&&name.EndsWith(')')){string tag=name[(suffix+2)..^1];if(tag.Length is >=2 and <=5&&tag.All(c=>c is >= 'A' and <= 'Z'))name=name[..suffix].TrimEnd();}return name[..Math.Min(name.Length,Musical5.MusicalNameMaxLength)].TrimEnd();}
    object ExportCGearPNG(JsonElement r){if(save is not SAV5)throw new Exception("Open a Generation 5 save first.");string value=S(r,"png");if(value.Length>1048576)throw new Exception("The PNG is too large.");var bytes=Convert.FromBase64String(value);if(bytes.Length<24||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})||System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4))!=256||System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4))!=192)throw new Exception("Expected a 256 × 192 PNG.");var path=ExportPath(S(r,"path"),sourcePath);AtomicWrite(path,bytes);return new{path};}
    object ReadCGearImage() {var s=RequireSave() as SAV5??throw new Exception("C-Gear backgrounds are only available in Generation 5.");CGearBackground bg=s is SAV5BW?new CGearBackgroundBW(s.CGearSkinData.ToArray()):new CGearBackgroundB2W2(s.CGearSkinData.ToArray());return new{width=256,height=192,pixels=Convert.ToBase64String(bg.GetImageData()),empty=bg.IsUninitialized};}
    void SetCGearImage(JsonElement r) {if(N(r,"revision")!=revision)throw new Exception("The save changed. Reopen this preview.");var s=RequireSave() as SAV5??throw new Exception("C-Gear backgrounds require a Generation 5 save.");string text=S(r,"pixels");if(text.Length>262144)throw new Exception("Use a 256 × 192 image.");var pixels=Convert.FromBase64String(text);if(pixels.Length!=256*192*4)throw new Exception("Use a 256 × 192 image.");var colors=new HashSet<int>();for(int i=0;i<pixels.Length;i+=4)colors.Add(pixels[i]|(pixels[i+1]<<8)|(pixels[i+2]<<16));if(colors.Count>16)throw new Exception($"This image has {colors.Count} colors. C-Gear supports at most 16.");var data=new byte[CGearBackground.SIZE];CGearBackground bg=s is SAV5BW?new CGearBackgroundBW(data):new CGearBackgroundB2W2(data);var result=bg.SetImageData(pixels);if(result.ColorCount>16||result.TileCount>=255)throw new Exception($"This image needs {result.ColorCount} colors and {result.TileCount+1} tiles. Use at most 16 colors and 255 unique 8 × 8 tiles.");s.SetCGearSkin(data);dirty=true;}
}
