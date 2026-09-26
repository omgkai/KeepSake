using PKHeX.Core;
var output = Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
foreach (var game in new[] { GameVersion.RD, GameVersion.C, GameVersion.E, GameVersion.Pt, GameVersion.B2, GameVersion.X, GameVersion.US, GameVersion.SW, GameVersion.BD, GameVersion.PLA, GameVersion.SL, GameVersion.ZA })
{
    try {
        var sav = BlankSaveFile.Get(game,"Fixture");
        // A blank Gen 5 buffer is not an encrypted empty album. Initialize synthetic fixtures explicitly.
        if (sav is IMysteryGiftStorageProvider provider)
        {
            var cards = provider.MysteryGiftStorage;
            for (int i = 0; i < cards.GiftCountMax; i++) {var gift = cards.GetMysteryGift(i); gift.Clear(); cards.SetMysteryGift(i, gift);}
            if (cards is IMysteryGiftFlags flags) flags.ClearReceivedFlags();
            if (cards is MysteryBlock5 gen5) gen5.EndAccess();
        }
        var pk = sav.BlankPKM; pk.Species = 25; pk.CurrentLevel = 25; pk.Nickname = "Pikachu";
        pk.OriginalTrainerName = "Fixture"; pk.TID16 = sav.TID16; pk.SID16 = sav.SID16; pk.Version = game;
        sav.SetBoxSlotAtIndex(pk,0,0);
        var saveBytes = sav.Write().ToArray();
        File.WriteAllBytes(Path.Combine(output,game+".sav"),saveBytes);
        var bytes = new byte[pk.SIZE_STORED]; int length = pk.WriteDecryptedDataStored(bytes);
        File.WriteAllBytes(Path.Combine(output,game+"."+pk.Extension),bytes[..length]);
        Console.WriteLine($"{game}: save {saveBytes.Length} bytes; PKM {length} bytes; reopen {SaveUtil.GetSaveFile(saveBytes) != null}");
        if (sav is SAV8BS bs)
        {
            bs.BoxLayout.TeamSlots[0] = 0;
            bs.BoxLayout.LockedTeam = 1;
            bs.BoxLayout.SaveBattleTeams();
            File.WriteAllBytes(Path.Combine(output,"BD-protected.sav"), sav.Write().ToArray());
        }
    } catch (Exception ex) { Console.WriteLine($"{game}: {ex.Message}"); }
}
