using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    void AddInventory(JsonElement r)
    {
        var sav = RequireSave(); var bag = sav.Inventory;
        int index = N(r,"pouch"), id = N(r,"item"), count = N(r,"count");
        if (index < 0 || index >= bag.Pouches.Count) throw new Exception("Choose a valid pouch.");
        var pouch = bag.Pouches[index];
        if (id <= 0 || id > sav.MaxItemID || !pouch.CanContain((ushort)id)) throw new Exception("Choose an item from this pouch.");
        var matches = pouch.Items.Where(x=>x.Index == id).ToArray();
        if (matches.Length > 1) throw new Exception("This pouch contains duplicate rows for that item. Resolve those rows before adding more.");
        int owned = matches.FirstOrDefault()?.Count ?? 0;
        int max = bag.GetMaxCount(pouch.Type,id);
        if (count <= 0 || owned < 0 || (long)owned + count > max) throw new Exception($"Add between 1 and {Math.Max(0,max-owned)} more of this item (maximum {max}).");
        if (pouch.GiveItem(bag,(ushort)id,count) < 0) throw new Exception("This pouch is full. Remove an item before adding another.");
        bag.CopyTo(sav);
        var persisted = sav.Inventory.Pouches[index].Items.Where(x=>x.Index == id).ToArray();
        if (persisted.Length != 1 || persisted[0].Count != owned + count) throw new Exception("The game could not store this item. No change was applied.");
        dirty = true;
    }
}
