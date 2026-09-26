using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    static readonly string[] markingNames = ["Circle", "Triangle", "Square", "Heart", "Star", "Diamond"];
    static bool WritableProperty(PKM pk, string name)
    {
        var setter = Property(pk, name)?.SetMethod;
        var il = setter?.GetMethodBody()?.GetILAsByteArray();
        return setter?.IsPublic == true && (il == null || !il.All(b => b is 0x00 or 0x2A));
    }
    static List<RibbonInfo> EditableRibbons(PKM pk) => RibbonInfo.GetRibbonInfo(pk).Where(r => WritableProperty(pk, r.Name)).ToList();
    string RibbonName(string id) => strings.Ribbons.GetNameSafe(id, out var name) ? name : Label(id);
    static string RibbonSprite(RibbonInfo ribbon, int format)
    {
        if (ribbon.Type == RibbonValueType.Boolean) return ribbon.Name.Replace("CountG3", "G3").ToLowerInvariant();
        string name = ribbon.Name.ToLowerInvariant();
        int max = ribbon.MaxCount;
        if (ribbon.Name == "RibbonCountMemoryBattle" && format >= 9) max = 7;
        if (max != 4) return name + (ribbon.RibbonCount >= max ? "2" : "");
        return name.Replace("count", "") + (ribbon.RibbonCount switch { 2 => "super", 3 => "hyper", 4 => "master", _ => "" });
    }
    object Decorations()
    {
        var pk = RequireEntity(); var ribbons = EditableRibbons(pk);
        var issues = new Dictionary<string, string>(); var suggested = new HashSet<string>();
        string analysisNote = "";
        if (pk.Species != 0 && ribbons.Count != 0)
        {
            try
            {
                var analysis = new LegalityAnalysis(pk);
                var args = new RibbonVerifierArguments(pk, analysis.EncounterOriginal, analysis.Info.EvoChainsAllGens);
                Span<RibbonResult> results = stackalloc RibbonResult[RibbonVerifier.MaxRibbonCount];
                int count = RibbonVerifier.GetRibbonResults(args, results);
                foreach (var result in results[..count]) issues[result.PropertyName] = result.IsMissing ? "Missing" : "Invalid";
                var clone = pk.Clone(); RibbonApplicator.SetAllValidRibbons(clone);
                foreach (var r in RibbonInfo.GetRibbonInfo(clone)) if (r.HasRibbon || r.RibbonCount > 0) suggested.Add(r.Name);
            }
            catch { analysisNote = "Ribbon suggestions are unavailable for this Pokémon. Individual edits are still available; review the full legality report."; }
        }
        var entries = ribbons.Select(r => new {
            id = r.Name, name = RibbonName(r.Name), mark = r.Name.StartsWith("RibbonMark"),
            count = r.Type == RibbonValueType.Byte, value = r.Type == RibbonValueType.Byte ? r.RibbonCount : r.HasRibbon ? 1 : 0,
            max = r.Type == RibbonValueType.Byte ? r.MaxCount : 1, sprite = RibbonSprite(r, pk.Format),
            status = issues.GetValueOrDefault(r.Name, suggested.Contains(r.Name) ? "Available" : ""),
        }).OrderBy(r => r.mark).ThenBy(r => r.name).ToArray();
        var markings = markingNames.Where(n => pk is IAppliedMarkings && WritableProperty(pk, "Marking" + n)).Select(n => new {
            id = "Marking" + n, name = n, value = Convert.ToInt32(Property(pk, "Marking" + n)!.GetValue(pk)),
        }).ToArray();
        bool colored = pk is IAppliedMarkings<MarkingColor>;
        var affixedChoices = pk is IRibbonSetAffixed ? new[] { new Choice("-1", "None") }.Concat(Enumerable.Range(0, AffixedRibbon.Max + 1).Select(i => {
            var name = $"Ribbon{(RibbonIndex)i}";
            bool owned = entries.Any(e => e.id == name && e.value != 0);
            return new Choice(i.ToString(), RibbonName(name) + (owned ? "" : " · Not owned"));
        }).OrderBy(c => c.value == "-1" ? "" : c.label)).ToArray() : [];
        return new { entries, markings, colored, canSuggestMarkings = pk is IAppliedMarkings { MarkingCount: 6 },
            canAffix = pk is IRibbonSetAffixed, affixed = pk is IRibbonSetAffixed a ? (int)a.AffixedRibbon : -1, affixedChoices,
            analysisNote, canSuggest = pk.Species != 0 && ribbons.Count != 0 && analysisNote.Length == 0 };
    }
    void EditDecorations(JsonElement r)
    {
        var pk = RequireEntity(); var ribbons = EditableRibbons(pk);
        switch (S(r, "mode"))
        {
            case "ribbon":
                var ribbon = ribbons.FirstOrDefault(x => x.Name == S(r, "id")) ?? throw new Exception("This Pokémon format does not store that ribbon.");
                int value = N(r, "value"); int max = ribbon.Type == RibbonValueType.Byte ? ribbon.MaxCount : 1;
                if (value < 0 || value > max) throw new Exception($"Enter a ribbon value from 0 to {max}.");
                SetProperty(pk, ribbon.Name, ribbon.Type == RibbonValueType.Byte ? value.ToString() : value == 1 ? "true" : "false");
                break;
            case "all":
            case "clear":
                if (ribbons.Count == 0) throw new Exception("This format has no editable ribbons.");
                bool all = S(r, "mode") == "all";
                foreach (var rib in ribbons)
                    SetProperty(pk, rib.Name, rib.Type == RibbonValueType.Byte ? (all ? rib.MaxCount : 0).ToString() : all ? "true" : "false");
                if (!all && pk is IRibbonSetAffixed cleared) cleared.AffixedRibbon = AffixedRibbon.None;
                break;
            case "suggest":
            case "required":
                if (pk.Species == 0 || ribbons.Count == 0) throw new Exception("Select a Pokémon with ribbon support first.");
                RibbonApplicator.RemoveAllValidRibbons(pk);
                if (S(r, "mode") == "suggest") RibbonApplicator.SetAllValidRibbons(pk);
                else if (pk is IRibbonSetAffixed required) required.AffixedRibbon = AffixedRibbon.None;
                break;
            case "affix":
                if (pk is not IRibbonSetAffixed affixed) throw new Exception("This format does not store an equipped ribbon or mark.");
                int index = N(r, "value");
                if (index < AffixedRibbon.None || index > AffixedRibbon.Max) throw new Exception("Choose a valid ribbon or mark title.");
                affixed.AffixedRibbon = (sbyte)index;
                break;
            case "marking":
                string name = S(r, "id"); int marking = N(r, "value");
                if (pk is not IAppliedMarkings || !markingNames.Any(n => "Marking" + n == name) || !WritableProperty(pk, name)) throw new Exception("This format does not store that shape marking.");
                if (pk is IAppliedMarkings<MarkingColor>)
                {
                    if (marking < 0 || marking > 2) throw new Exception("Choose None, Blue, or Pink.");
                    SetProperty(pk, name, ((MarkingColor)marking).ToString());
                }
                else
                {
                    if (marking is not (0 or 1)) throw new Exception("This format supports unmarked or marked shapes only.");
                    SetProperty(pk, name, marking == 1 ? "true" : "false");
                }
                break;
            case "markingsClear":
                if (pk is not IAppliedMarkings) throw new Exception("This format has no shape markings.");
                foreach (string n in markingNames.Where(n => WritableProperty(pk, "Marking" + n)))
                    SetProperty(pk, "Marking" + n, pk is IAppliedMarkings<MarkingColor> ? "None" : "false");
                break;
            case "markingsSuggest":
                if (pk is not IAppliedMarkings { MarkingCount: 6 }) throw new Exception("IV-based markings need six stored shapes.");
                pk.SetMarkings(); break;
            default: throw new Exception("Unknown ribbon or marking action.");
        }
        pk.RefreshChecksum(); pending = true;
    }
}
