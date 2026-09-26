using PKHeX.Core;

sealed partial class EditorSession
{
    // Keep slots distinct even when normal and hidden slots share an ability ID.
    Choice[] AbilityChoices(PKM? pk)
    {
        if (pk is null || pk.Format < 3 || pk.Species == 0) return [];
        var pi = pk.PersonalInfo;
        var result = new List<Choice>();
        for (int slot = 0; slot < pi.AbilityCount; slot++)
        {
            int ability = pi.GetAbilityAtIndex(slot);
            if (pk.Format == 3 && slot == 1 && ability == pi.GetAbilityAtIndex(0)) continue;
            if (ability <= 0 || ability > pk.MaxAbilityID) continue;
            result.Add(new($"{ability}:{slot}", $"{MoveLabel(strings.abilitylist, ability)}{(slot == 2 ? " · Hidden" : "")}"));
        }
        // Like PKHeX's picker, retain Gen 5 Blue-Striped Basculin's traded ability.
        if (pk is { Context: EntityContext.Gen5, Species: (ushort)PKHeX.Core.Species.Basculin, Form: 1 })
            result.Add(new($"{(int)Ability.Reckless}:0", "Reckless · Traded ability"));
        return result.ToArray();
    }
    void SetAbilityChoice(PKM pk, string value)
    {
        if (!AbilityChoices(pk).Any(c => c.value == value))
            throw new Exception("Choose an ability available to this Pokémon’s species and form.");
        var parts = value.Split(':');
        int ability = int.Parse(parts[0]), slot = int.Parse(parts[1]);
        pk.SetAbilityIndex(slot);
        pk.Ability = ability;
    }
}
