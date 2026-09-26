using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    // Core flag order is HP/Atk/Def/SpA/SpD/Spe; game labels use a different order.
    static readonly int[] trainingNames = [3,4,7,2,5,6,9,10,13,8,11,12,15,16,19,14,17,18,20,21,22,23,24,25,26,27,28,29,30,31];
    object? SuperTraining()
    {
        if (entity is not ISuperTrainRegimen training) return null;
        var pk = RequireEntity();
        var entries = Enumerable.Range(0, SuperTrainRegimenExtensions.CountRegimen).Select(i => new {
            id = i, name = strings.trainingstage[trainingNames[i]], group = i < 18 ? $"Rank {i / 6 + 1}" : "Secret Training",
            completed = training.GetRegimenState(i), enabled = pk is not PK6 || i < 18 || training.SecretSuperTrainingUnlocked,
        }).ToArray();
        var distribution = Enumerable.Range(0, SuperTrainRegimenExtensions.CountRegimenDistribution).Select(i => new {
            id = i, name = strings.trainingstage[32 + i], group = "Distribution", completed = training.GetRegimenStateDistribution(i), enabled = true,
        }).ToArray();
        return new {
            entries, distribution, native = pk is PK6, unlocked = training.SecretSuperTrainingUnlocked, complete = training.SuperTrainSupremelyTrained,
            bag = pk is PK6 p6 ? (int)p6.TrainingBag : 0, hits = pk is PK6 p ? (int)p.TrainingBagHits : 0,
            bags = strings.trainingbags.Select((name, i) => new Choice(i.ToString(), i == 0 ? "None" : name)).ToArray(),
        };
    }
    void EditSuperTraining(JsonElement r)
    {
        var pk = RequireEntity();
        if (pk is not ISuperTrainRegimen training) throw new Exception("This format does not store Super Training records.");
        switch (S(r, "mode"))
        {
            case "regimen":
                int id = N(r, "id"); bool dist = B(r, "distribution");
                int max = dist ? SuperTrainRegimenExtensions.CountRegimenDistribution : SuperTrainRegimenExtensions.CountRegimen;
                if (id < 0 || id >= max) throw new Exception("Choose a valid training regimen.");
                if (!dist && id >= 18 && pk is PK6 && !training.SecretSuperTrainingUnlocked && B(r, "value")) throw new Exception("Unlock Secret Super Training before completing its regimens.");
                if (dist) training.SetRegimenStateDistribution(id, B(r, "value"));
                else training.SetRegimenState(id, B(r, "value"));
                break;
            case "unlocked":
                if (pk is not PK6) throw new Exception("The Secret Training switch is only editable in generation 6.");
                training.SecretSuperTrainingUnlocked = B(r, "value");
                if (!training.SecretSuperTrainingUnlocked)
                {
                    training.SuperTrainSupremelyTrained = false;
                    for (int i = 18; i < SuperTrainRegimenExtensions.CountRegimen; i++) training.SetRegimenState(i, false);
                }
                break;
            case "complete":
                if (pk is not PK6 || !training.SecretSuperTrainingUnlocked) throw new Exception("Unlock Secret Super Training in generation 6 first.");
                training.SuperTrainSupremelyTrained = B(r, "value"); break;
            case "bag":
                if (pk is not PK6 bag) throw new Exception("Training bags are only stored in generation 6.");
                int bagID = N(r, "bag"), hits = N(r, "hits");
                if (bagID < 0 || bagID >= strings.trainingbags.Length || hits < 0 || hits > 255) throw new Exception("Choose a listed training bag and 0–255 hits.");
                bag.TrainingBag = (byte)bagID; bag.TrainingBagHits = (byte)hits; break;
            case "all":
                if (pk is PK6) { training.SecretSuperTrainingUnlocked = true; training.SuperTrainSupremelyTrained = true; }
                for (int i = 0; i < SuperTrainRegimenExtensions.CountRegimen; i++) training.SetRegimenState(i, true);
                if (B(r, "distribution")) for (int i = 0; i < SuperTrainRegimenExtensions.CountRegimenDistribution; i++) training.SetRegimenStateDistribution(i, true);
                break;
            case "clear":
                for (int i = 0; i < SuperTrainRegimenExtensions.CountRegimen; i++) training.SetRegimenState(i, false);
                for (int i = 0; i < SuperTrainRegimenExtensions.CountRegimenDistribution; i++) training.SetRegimenStateDistribution(i, false);
                training.SecretSuperTrainingUnlocked = training.SuperTrainSupremelyTrained = false;
                break;
            default: throw new Exception("Unknown Super Training action.");
        }
        pk.RefreshChecksum(); pending = true;
    }
}
