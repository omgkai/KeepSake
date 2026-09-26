using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    Choice[] DexForms(PokedexSave8a dex, ushort species)
    {
        var names = FormConverter.GetFormList(species, strings.Types, strings.forms, EntityContext.Gen8a);
        return Enumerable.Range(0, PersonalTable.LA[species].FormCount)
            .Where(f => dex.HasFormStorage(species, (byte)f) && !dex.IsBlacklisted(species, (byte)f))
            .Select(f => new Choice(f.ToString(), f < names.Length && !string.IsNullOrWhiteSpace(names[f]) ? names[f] : f == 0 ? "Normal" : $"Form {f}" )).ToArray();
    }
    static byte DexForm(PokedexSave8a dex, ushort species, int form)
    {
        if (form < 0 || form >= PersonalTable.LA[species].FormCount || !dex.HasFormStorage(species, (byte)form) || dex.IsBlacklisted(species, (byte)form))
            throw new Exception("Choose a form stored in the Hisui Pokédex.");
        return (byte)form;
    }
    object DexDetails(JsonElement r)
    {
        var dex = (RequireSave() as SAV8LA ?? throw new Exception("These Pokédex details are specific to Legends: Arceus.")).PokedexSave;
        var species = ResearchSpecies(N(r, "species"));
        var forms = DexForms(dex, species);
        var form = DexForm(dex, species, N(r, "form", int.Parse(forms[0].value)));
        dex.GetSizeStatistics(species, form, out var hasMax, out var minHeight, out var maxHeight, out var minWeight, out var maxWeight);
        return new {
            species = (int)species, name = Species(species), form = (int)form, forms,
            seen = (int)dex.GetPokeSeenInWildFlags(species, form), obtained = (int)dex.GetPokeObtainFlags(species, form), caught = (int)dex.GetPokeCaughtInWildFlags(species, form),
            displayForm = dex.GetSelectedForm(species), female = dex.GetSelectedGender1(species), shiny = dex.GetSelectedShiny(species), alpha = dex.GetSelectedAlpha(species),
            multipleGenders = PokedexSave8a.HasMultipleGenders(species), hasMax, minHeight, maxHeight, minWeight, maxWeight,
        };
    }
    void EditDexDetails(JsonElement r)
    {
        var dex = (RequireSave() as SAV8LA ?? throw new Exception("These Pokédex details are specific to Legends: Arceus.")).PokedexSave;
        var species = ResearchSpecies(N(r, "species"));
        var form = DexForm(dex, species, N(r, "form"));
        switch (S(r, "mode"))
        {
            case "flags":
                byte Flags(string key) { int value = N(r, key); if (value < 0 || value > 255) throw new Exception("Form flags must be between 0 and 255."); return (byte)value; }
                var seen = Flags("seen"); var obtained = Flags("obtained"); var caught = Flags("caught");
                dex.SetPokeSeenInWildFlags(species, form, seen);
                dex.SetPokeObtainFlags(species, form, obtained);
                dex.SetPokeCaughtInWildFlags(species, form, caught);
                if ((seen | obtained | caught) != 0) dex.SetPokeHasBeenUpdated(species);
                break;
            case "display":
                var display = DexForm(dex, species, N(r, "displayForm"));
                dex.SetSelectedGenderForm(species, display, B(r, "female"), B(r, "shiny"), B(r, "alpha"));
                dex.SetPokeHasBeenUpdated(species);
                break;
            case "size":
                float Size(string key) { float v = r.GetProperty(key).GetSingle(); if (!float.IsFinite(v) || v < 0) throw new Exception("Size records must be finite, nonnegative numbers."); return v; }
                var minH = Size("minHeight"); var maxH = Size("maxHeight"); var minW = Size("minWeight"); var maxW = Size("maxWeight");
                var hasMax = B(r, "hasMax");
                if (hasMax && (maxH < minH || maxW < minW)) throw new Exception("Maximum size cannot be smaller than minimum size.");
                dex.SetSizeStatistics(species, form, hasMax, minH, maxH, minW, maxW);
                break;
            default: throw new Exception("Unknown Pokédex detail action.");
        }
        dirty = true;
    }
}
