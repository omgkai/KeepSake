using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    readonly HashSet<string> importedGiftPaths = new(StringComparer.OrdinalIgnoreCase);
    IMysteryGiftStorage AlbumStorage() => RequireSave() is IMysteryGiftStorageProvider p ? p.MysteryGiftStorage : throw new Exception("This game does not store an editable Mystery Gift album.");
    DataMysteryGift[] ReadAlbum(IMysteryGiftStorage cards)
    {
        var album = Enumerable.Range(0, cards.GiftCountMax).Select(cards.GetMysteryGift).ToList();
        if (save is SAV4HGSS hgss) album.Add(hgss.LockCapsuleSlot);
        return album.ToArray();
    }
    static void EndAlbumAccess(IMysteryGiftStorage cards) { if (cards is MysteryBlock5 gen5) gen5.EndAccess(); }
    static int AlbumIndex(JsonElement r, DataMysteryGift[] album)
    {
        int index = N(r, "index");
        if (index < 0 || index >= album.Length) throw new Exception("Choose a valid album slot.");
        return index;
    }
    object GiftAlbum()
    {
        var cards = AlbumStorage();
        try
        {
            var album = ReadAlbum(cards); var flags = cards as IMysteryGiftFlags;
            var entries = album.Select((g, i) => new {
                id = i, card = g.CardID, title = g.IsEmpty ? "Empty slot" : g.CardTitle, type = g.Type,
                empty = g.IsEmpty, used = g.GiftUsed, canUse = g is not (WR7 or PGT or PCD),
                extension = g.Extension, name = g.IsEmpty ? "" : g.IsEntity ? Species(g.Species) : "Item gift",
                special = save is SAV4HGSS && i == album.Length - 1,
            }).ToArray();
            int flagMax = flags?.MysteryGiftReceivedFlagMax ?? 0;
            var received = flags == null ? [] : Enumerable.Range(1, Math.Max(0, flagMax - 1)).Where(flags.GetMysteryGiftReceivedFlag).ToArray();
            return new { entries, received, flagMax };
        }
        finally { EndAlbumAccess(cards); }
    }
    void WriteAlbum(IMysteryGiftStorage cards, DataMysteryGift[] album)
    {
        if (cards is MysteryBlock4 gen4)
        {
            gen4.IsDeliveryManActive = album.Any(g => !g.IsEmpty);
            MysteryBlock4.UpdateSlotPGT(album, save is SAV4HGSS);
            if (save is SAV4HGSS hgss) hgss.LockCapsuleSlot = (PCD)album[^1];
        }
        for (int i = 0; i < cards.GiftCountMax; i++) cards.SetMysteryGift(i, album[i]);
    }
    void EditGiftAlbum(JsonElement r)
    {
        if(r.TryGetProperty("revision",out _) && N(r,"revision")!=revision)throw new Exception("The save changed. Refresh the gift album.");
        var cards = AlbumStorage();
        try
        {
            var album = ReadAlbum(cards); var flags = cards as IMysteryGiftFlags;
            var mode = S(r, "mode");
            if (mode == "flag")
            {
                int id = N(r, "card");
                if (flags == null || id < 1 || id >= flags.MysteryGiftReceivedFlagMax) throw new Exception("Card ID is outside this game's received-card range.");
                flags.SetMysteryGiftReceivedFlag(id, B(r, "value"));
            }
            else if (mode == "usedAll")
            {
                foreach (var gift in album.Where(g => !g.IsEmpty && g is not (WR7 or PGT or PCD))) gift.GiftUsed = B(r, "value");
                WriteAlbum(cards, album);
            }
            else
            {
                int index = AlbumIndex(r, album);
                switch (mode)
                {
                    case "import":
                    case "library":
                        DataMysteryGift gift;
                        string? importedPath = null;
                        if (mode == "library") gift = (Gift(N(r, "id")) as DataMysteryGift)?.Clone() ?? throw new Exception("This event has no card file.");
                        else
                        {
                            importedPath = Path.GetFullPath(S(r, "path"));
                            if (new FileInfo(importedPath).Length > 1024 * 1024) throw new Exception("This file is too large to be an event card.");
                            gift = MysteryGift.GetMysteryGift(File.ReadAllBytes(importedPath), Path.GetExtension(importedPath)) ?? throw new Exception("PKHeX could not recognize this event card.");
                        }
                        if (gift.IsEmpty) throw new Exception("This card is empty.");
                        if (!gift.IsCardCompatible(RequireSave(), out var message)) throw new Exception("This card is incompatible with the open save. " + message);
                        if (gift is PCD { IsLockCapsule: true })
                        {
                            if (save is not SAV4HGSS) throw new Exception("The Lock Capsule card needs a HeartGold or SoulSilver save.");
                            index = album.Length - 1;
                        }
                        else
                        {
                            if (gift is PCD { CanConvertToPGT: true } pcd && album[index] is PGT) gift = pcd.Gift;
                            int firstEmpty = Array.FindIndex(album, g => g.IsEmpty && g.Type == gift.Type);
                            if (firstEmpty >= 0 && firstEmpty < index) index = firstEmpty;
                            if (save is SAV4HGSS && index == album.Length - 1) throw new Exception("This slot is reserved for the Lock Capsule card.");
                        }
                        if (gift.Type != album[index].Type) throw new Exception($"Choose a {gift.Type} slot for this card.");
                        album[index] = gift.Clone();
                        if (flags != null && gift.CardID > 0 && gift.CardID < flags.MysteryGiftReceivedFlagMax) flags.SetMysteryGiftReceivedFlag(gift.CardID, true);
                        WriteAlbum(cards, album);
                        if (importedPath != null) importedGiftPaths.Add(importedPath);
                        break;
                    case "delete":
                        album[index].Clear();
                        int limit = save is SAV4HGSS ? album.Length - 1 : album.Length;
                        for (int i = index; i + 1 < limit && !album[i + 1].IsEmpty && album[i].Type == album[i + 1].Type; i++)
                            (album[i], album[i + 1]) = (album[i + 1], album[i]);
                        WriteAlbum(cards, album);
                        break;
                    case "used":
                        if (album[index].IsEmpty || album[index] is WR7 or PGT or PCD) throw new Exception("This slot has no editable received status.");
                        album[index].GiftUsed = B(r, "value"); WriteAlbum(cards, album);
                        break;
                    default: throw new Exception("Unknown gift album action.");
                }
            }
            dirty = true;
        }
        finally { EndAlbumAccess(cards); }
    }
    object ExportAlbumGift(JsonElement r)
    {
        var cards = AlbumStorage();
        try
        {
            var album = ReadAlbum(cards); var gift = album[AlbumIndex(r, album)];
            if (gift.IsEmpty) throw new Exception("Choose an occupied album slot.");
            var path = ExportPath(S(r, "path"), sourcePath ?? entitySourcePath);
            if (importedGiftPaths.Contains(path)) throw new Exception("Export to a new file to preserve the imported card.");
            AtomicWrite(path, gift.Write().ToArray()); return new { count = 1, path };
        }
        finally { EndAlbumAccess(cards); }
    }
}
