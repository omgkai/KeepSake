using PKHeX.Core;
using System.Text.Json;
sealed partial class EditorSession {
    void ImportSlotFile(JsonElement r) {
        var sav=RequireSave();if(pending)throw new Exception("Set or discard the current Pokémon edits before dropping a file into a slot.");
        if(N(r,"revision")!=revision || S(r,"session")!=dragSession)throw new Exception("The workspace changed. Drop the file again.");
        int b=N(r,"box"),s=N(r,"slot");ValidateMovableSlot(sav,b,s);
        var path=Path.GetFullPath(S(r,"path"));var info=new FileInfo(path);if(info.Length is <1 or >1048576)throw new Exception("Drop a Pokémon file up to 1 MB into a slot. Drop a save onto the window background to open it.");
        var pk=EntityFormat.GetFromBytes(File.ReadAllBytes(path),EntityFileExtension.GetContextFromExtension(info.Extension,sav.Context)) ?? throw new Exception("This is not a Pokémon file. Drop save files onto the window background.");
        if(pk.GetType()!=sav.PKMType)pk=EntityConverter.ConvertToType(pk,sav.PKMType,out _) ?? throw new Exception("This Pokémon cannot transfer into the loaded game.");
        if(pk.Species==0 || !sav.Personal.IsPresentInGame(pk.Species,pk.Form))throw new Exception("This Pokémon or form is unavailable in the loaded game.");
        sav.SetBoxSlotAtIndex(pk,b,s);Select(b,s,false);entitySourcePath=path;dirty=true;
    }
}
