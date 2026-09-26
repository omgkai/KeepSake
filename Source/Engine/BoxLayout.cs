using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    readonly string dragSession = Guid.NewGuid().ToString("N");
    static int WallpaperCount(SaveFile sav) => sav is not IBoxDetailWallpaper || sav is SAV8LA or SAV9ZA ? 0 : sav.Generation switch {
        3 when sav is SAV3 or SAV3RSBox => 16, 4 or 5 or 6 => 24, 7 => 16, 8 when sav is SAV8BS => 32, 8 => 19, 9 => 20, _ => 0,
    };
    static string WallpaperSprite(SaveFile sav, int value)
    {
        if (sav is SAV8LA) return "box_wp01bdsp";
        if (sav is SAV9ZA) return "box_wp02bdsp";
        int i = value + 1; var v = sav.Version;
        string suffix = v.Context switch {
            EntityContext.Gen3 when v == GameVersion.E => "e",
            EntityContext.Gen3 when GameVersion.FRLG.Contains(v) && i > 12 => "frlg",
            EntityContext.Gen3 => "rs",
            EntityContext.Gen4 when i <= 16 => "dp",
            EntityContext.Gen4 when v == GameVersion.Pt => "pt",
            EntityContext.Gen4 when GameVersion.HGSS.Contains(v) => "hgss",
            EntityContext.Gen4 => "dp",
            EntityContext.Gen5 => GameVersion.B2W2.Contains(v) && i > 16 ? "b2w2" : "bw",
            EntityContext.Gen6 => GameVersion.ORAS.Contains(v) && i > 16 ? "ao" : "xy",
            EntityContext.Gen7 => "xy", EntityContext.Gen8b => "bdsp", EntityContext.Gen8 => "swsh", EntityContext.Gen9 => "sv", _ => "",
        };
        string variant = i == 20 ? v == GameVersion.SL ? "_n" : v == GameVersion.VL ? "_u" : "" : "";
        return $"box_wp{i:00}{suffix}{variant}";
    }
    object? BoxLayout()
    {
        if (save is not { BoxCount: > 0 } sav) return null;
        int count = WallpaperCount(sav);
        var availability = ReadBoxAvailability(sav);
        return new {
            canName = sav is IBoxDetailName,
            canUnlock = availability.canUnlock, unlocked = availability.unlocked, flags = availability.flags,
            entries = Enumerable.Range(0, sav.BoxCount).Select(i => {
                int wallpaper = sav is IBoxDetailWallpaper w ? w.GetBoxWallpaper(i) : -1;
                return new { id = i, name = sav is IBoxDetailNameRead n ? n.GetBoxName(i) : $"Box {i + 1}", wallpaper,
                    sprite = wallpaper >= 0 ? WallpaperSprite(sav, wallpaper) : "" };
            }).ToArray(),
            wallpapers = Enumerable.Range(0, count).Select(i => new { id = i, name = sav.Generation < 8 || sav is SAV8BS ? strings.wallpapernames[i] : $"Wallpaper {i + 1}", sprite = WallpaperSprite(sav, i) }).ToArray(),
        };
    }
    static (bool canUnlock,int unlocked,int[] flags) ReadBoxAvailability(SaveFile sav)
    {
        int count=-1;int[] flags=[];
        try {count=sav.BoxesUnlocked;} catch(ArgumentException) {} catch(InvalidOperationException) {} catch(KeyNotFoundException) {}
        try {flags=sav.BoxFlags.Select(x=>(int)x).ToArray();} catch(ArgumentException) {} catch(InvalidOperationException) {} catch(KeyNotFoundException) {} catch(OverflowException) {}
        bool writable=sav.GetType().GetProperty(nameof(SaveFile.BoxesUnlocked))?.SetMethod?.DeclaringType != typeof(SaveFile);
        return (writable && count>=0,count,flags);
    }
    static void InitializeDemoBoxLayout(SaveFile sav)
    {
        if (sav is not ISCBlockArray blocks) return;
        uint key=sav switch {
            SAV8SWSH=>SaveBlockAccessor8SWSH.KBoxesUnlocked,
            SAV8LA=>SaveBlockAccessor8LA.KBoxesUnlocked,
            SAV9SV=>SaveBlockAccessor9SV.KBoxesUnlocked,
            SAV9ZA=>SaveBlockAccessor9ZA.KBoxesUnlocked,
            _=>0,
        };
        if(key!=0) {var b=blocks.Accessor.GetBlock(key);b.ChangeStoredType(SCTypeCode.Byte);b.SetValue((byte)sav.BoxCount);}
        uint[] flags=sav switch {
            SAV8SWSH=>[SaveBlockAccessor8SWSH.KSecretBoxUnlocked],
            SAV8LA=>[SaveBlockAccessor8LA.KUnlockedSecretBox01,SaveBlockAccessor8LA.KUnlockedSecretBox02,SaveBlockAccessor8LA.KUnlockedSecretBox03],
            _=>[],
        };
        foreach(var flag in flags) if(blocks.Accessor.TryGetBlock(flag,out var block)) block.ChangeStoredType(SCTypeCode.Bool1);
    }
    static void ValidateBox(SaveFile sav, int index)
    {
        if (index < 0 || index >= sav.BoxCount) throw new Exception("Choose a valid box.");
    }
    static void ValidateMovableSlot(SaveFile sav, int b, int s)
    {
        ValidateBox(sav, b);
        if (s < 0 || s >= sav.BoxSlotCount || b * sav.BoxSlotCount + s >= sav.SlotCount) throw new Exception("Choose a valid box slot.");
        var flags = sav.GetBoxSlotFlags(b, s);
        if (flags.IsOverwriteProtected() || flags.IsParty() >= 0) throw new Exception("This slot is locked, protected, or linked to the party. Move an unlinked box slot instead.");
    }
    void EditBoxLayout(JsonElement r)
    {
        var sav = RequireSave(); int index = N(r, "box", box); ValidateBox(sav, index);
        switch (S(r, "mode", "name"))
        {
            case "unlocked":
                int count=N(r,"count");
                if (sav.GetType().GetProperty(nameof(SaveFile.BoxesUnlocked))?.SetMethod?.DeclaringType == typeof(SaveFile) || count<0 || count>sav.BoxCount) throw new Exception("Choose a supported unlocked-box count.");
                sav.BoxesUnlocked=count;
                if(sav.BoxesUnlocked!=count) throw new Exception("This save could not store the unlocked-box count.");
                break;
            case "flag":
                int flag=N(r,"index"), value=N(r,"value");var flags=sav.BoxFlags;
                if(flag<0 || flag>=flags.Length || value<0 || value>255) throw new Exception("Choose a valid box flag byte (0–255).");
                flags[flag]=(byte)value;sav.BoxFlags=flags;
                if(!sav.BoxFlags.SequenceEqual(flags)) throw new Exception("This save could not store the box flags.");
                break;
            case "name":
                if (sav is not IBoxDetailName names) throw new Exception("This game does not support editable box names.");
                string name = S(r, "name");
                if (name.Length > 16 || name.Any(char.IsControl)) throw new Exception("Use a box name of up to 16 characters without control characters.");
                names.SetBoxName(index, name);
                if (names.GetBoxName(index) != name) throw new Exception("This game cannot store that exact box name. Try a shorter name using characters supported by the game.");
                break;
            case "wallpaper":
                int wallpaper = N(r, "wallpaper");
                if (sav is not IBoxDetailWallpaper walls || wallpaper < 0 || wallpaper >= WallpaperCount(sav)) throw new Exception("Choose a wallpaper supported by this game.");
                walls.SetBoxWallpaper(index, wallpaper);
                if (walls.GetBoxWallpaper(index) != wallpaper) throw new Exception("The game did not accept that wallpaper.");
                break;
            case "move":
            case "swap":
                if (pending) throw new Exception("Set or discard Pokémon edits before reordering boxes.");
                int destination = N(r, "destination"); ValidateBox(sav, destination);
                if (index == destination) throw new Exception("Choose a different box position.");
                int start = Math.Min(index, destination), stop = Math.Max(index, destination);
                for (int b = start; b <= stop; b++)
                    for (int s = 0; s < sav.BoxSlotCount; s++) ValidateMovableSlot(sav, b, s);
                if (S(r, "mode") == "swap")
                {
                    if (!sav.SwapBox(index, destination)) throw new Exception("These boxes cannot be swapped.");
                }
                else
                {
                    // Adjacent upstream swaps keep contents, names, wallpapers, and pointers together.
                    int step = destination > index ? 1 : -1;
                    for (int current = index; current != destination; current += step)
                        if (!sav.SwapBox(current, current + step)) throw new Exception("This box cannot be moved through a protected region.");
                }
                Select(destination, 0, false);
                break;
            default: throw new Exception("Unknown box layout action.");
        }
        dirty = true;
    }
    void SwapBoxSlots(JsonElement r)
    {
        if (pending) throw new Exception("Set or discard Pokémon edits before moving a stored Pokémon.");
        if (S(r, "session") != dragSession || N(r, "revision", -1) != revision) throw new Exception("The save changed after this move began. Select the Pokémon again and retry.");
        var sav = RequireSave(); int fromBox = N(r, "fromBox"), fromSlot = N(r, "fromSlot"), toBox = N(r, "toBox"), toSlot = N(r, "toSlot");
        bool fromParty=B(r,"fromParty"),toParty=B(r,"toParty");
        ValidateTransferSlot(sav,fromBox,fromSlot,fromParty,true);
        ValidateTransferSlot(sav,toBox,toSlot,toParty,false);
        if (fromParty==toParty && (fromParty || fromBox==toBox) && fromSlot==toSlot) throw new Exception("Choose a different destination slot.");
        ISlotInfo source=fromParty ? new SlotInfoParty(fromSlot) : new SlotInfoBox(fromBox,fromSlot,sav);
        ISlotInfo destination=toParty ? new SlotInfoParty(toSlot) : new SlotInfoBox(toBox,toSlot,sav);
        var pk=source.Read(sav);
        if(pk.Species==0)throw new Exception("The source slot is empty.");
        // Check the resulting party before writing; SlotEditor.Swap only checks slot writability.
        if(fromParty != toParty) {
            int partyIndex=fromParty ? fromSlot : toSlot;
            var replacement=fromParty ? destination.Read(sav) : pk;
            bool hasUsable=replacement.Species!=0 && !replacement.IsEgg;
            for(int i=0;i<sav.PartyCount;i++)if(i!=partyIndex) {
                var member=sav.GetPartySlotAtIndex(i);hasUsable |= member.Species!=0 && !member.IsEgg;
            }
            if(!hasUsable)throw new Exception("Keep at least one non-Egg Pokémon in the party.");
        }
        if(fromParty && toParty && toSlot==sav.PartyCount) {
            // Removing a party member compacts the list; append at the new last position.
            sav.DeletePartySlot(fromSlot);
            toSlot=sav.PartyCount;
            sav.SetPartySlotAtIndex(pk,toSlot,EntityImportSettings.None);
        } else {
            var result=new SlotEditor<object>(sav).Swap(source,destination);
            if(result!=SlotTouchResult.Success)throw new Exception("These slots could not be swapped.");
        }
        Select(toBox,toSlot,toParty);dirty=true;
    }
    static void ValidateTransferSlot(SaveFile sav,int b,int s,bool party,bool source)
    {
        if(!party){ValidateMovableSlot(sav,b,s);return;}
        if(!sav.HasParty || s<0 || s>=6 || s>sav.PartyCount || (source && s>=sav.PartyCount))
            throw new Exception("Choose an occupied party slot or the next empty party slot. Party gaps are not supported.");
    }
}
