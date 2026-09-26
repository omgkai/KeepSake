using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    record LibraryPokemon(PKM Entity,string RelativePath,string FullPath,string Hash);
    LibraryPokemon[] libraryPokemon=[];
    string libraryToken="";
    object ScanPokemonLibrary(JsonElement r)
    {
        var paths=r.TryGetProperty("paths",out var roots) ? roots.EnumerateArray().Select(x=>Path.GetFullPath(x.GetString()!)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray():new[]{Path.GetFullPath(S(r,"path"))};
        if(paths.Length is <1 or >32 || paths.Any(x=>!Directory.Exists(x)))throw new Exception("Choose between 1 and 32 existing folders.");
        var path=paths[0];
        if (!Directory.Exists(path)) throw new Exception("Choose an existing Pokémon folder.");
        var found=new List<LibraryPokemon>();int skipped=0,visited=0;bool truncated=false;
        var extensions=EntityFileExtension.GetExtensionsAll().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var options=new EnumerationOptions {RecurseSubdirectories=B(r,"recursive"),IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint|FileAttributes.Hidden|FileAttributes.System,MaxRecursionDepth=32};
        foreach(var file in paths.SelectMany(root=>Directory.EnumerateFiles(root,"*",options)).Distinct(StringComparer.OrdinalIgnoreCase)) {
            if (++visited>20000 || found.Count>=5000) {truncated=true;break;}
            if (!extensions.Contains(Path.GetExtension(file).TrimStart('.'))) continue;
            try {
                var info=new FileInfo(file);
                if (info.Length>1024*1024 || info.Length==0) {skipped++;continue;}
                var raw=File.ReadAllBytes(file);
                var pk=EntityFormat.GetFromBytes(raw,EntityFileExtension.GetContextFromExtension(info.Extension));
                if (pk==null || pk.Species==0 || pk.Species>pk.MaxSpeciesID) {skipped++;continue;}
                found.Add(new(pk,(paths.Length==1 ? Path.GetRelativePath(path,file):file),file,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raw))));
            } catch(IOException) {skipped++;} catch(UnauthorizedAccessException) {skipped++;} catch(ArgumentException) {skipped++;}
        }
        libraryPokemon=found.OrderBy(x=>x.RelativePath,StringComparer.OrdinalIgnoreCase).ToArray();libraryToken=Guid.NewGuid().ToString("N");
        return new {token=libraryToken,path,paths,skipped,truncated,entries=libraryPokemon.Select((x,i)=>new {
            id=i,path=x.RelativePath,name=Species(x.Entity.Species),nickname=x.Entity.Nickname,level=x.Entity.CurrentLevel,
            format=x.Entity.Extension,game=GameInfo.GetVersionName(x.Entity.Version),shiny=x.Entity.IsShiny,
            alpha=x.Entity is IAlphaReadOnly {IsAlpha:true},trainer=x.Entity.OriginalTrainerName,sprite=Sprite(x.Entity),portrait=Portrait(x.Entity),checksum=x.Entity.ChecksumValid
        }).ToArray()};
    }
    void PrepareLibraryPokemon(JsonElement r)
    {
        int id=N(r,"id");
        if(S(r,"token")!=libraryToken || id<0 || id>=libraryPokemon.Length) throw new Exception("The folder results changed. Choose a Pokémon from the current list.");
        var pk=libraryPokemon[id].Entity.Clone();
        if (save is {} sav) {
            if(pk.GetType()!=sav.PKMType) pk=EntityConverter.ConvertToType(pk,sav.PKMType,out _) ?? throw new Exception("This Pokémon cannot be transferred to the open save's format.");
            if(!sav.Personal.IsPresentInGame(pk.Species,pk.Form)) throw new Exception("This species or form is unavailable in the open game.");
        }
        entity=pk;entitySourcePath=libraryPokemon[id].FullPath;pending=true;
    }
}
