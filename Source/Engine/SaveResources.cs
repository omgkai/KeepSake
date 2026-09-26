using System.Security.Cryptography;
using System.Text.Json;
using PKHeX.Core;

sealed partial class EditorSession
{
    string BackupDirectory => settingsPath is null ? throw new Exception("Backup storage requires a settings location.") : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(settingsPath))!, "Save Backups");
    sealed record SaveBackup(string id, string name, string created, long size);
    void BackupOriginal(string path, byte[] bytes)
    {
        if (settingsPath is null) return;
        var directory = BackupDirectory;
        Directory.CreateDirectory(directory);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var destination = Path.Combine(directory, hash + ".bak");
        // Content-addressed snapshots never overwrite an earlier save revision.
        if (!File.Exists(destination)) AtomicWrite(destination, bytes);
        var metadata = new SaveBackup(hash, Path.GetFileName(path), File.GetCreationTimeUtc(destination).ToString("O"), bytes.LongLength);
        if (!File.Exists(Path.Combine(directory, hash + ".json")))
            AtomicWrite(Path.Combine(directory, hash + ".json"), JsonSerializer.SerializeToUtf8Bytes(metadata));
    }
    object ListSaveBackups()
    {
        var rows = new List<SaveBackup>();
        if (settingsPath is null || !Directory.Exists(BackupDirectory)) return rows;
        foreach (var path in Directory.EnumerateFiles(BackupDirectory, "*.json").Take(10000))
        {
            try
            {
                var item = JsonSerializer.Deserialize<SaveBackup>(File.ReadAllText(path));
                if (item != null && ValidBackupID(item.id) && File.Exists(Path.Combine(BackupDirectory, item.id + ".bak"))) rows.Add(item);
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        }
        return rows.OrderByDescending(x => x.created).ToArray();
    }
    static bool ValidBackupID(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    object ExportSaveBackup(JsonElement r)
    {
        var id = S(r, "id");
        if (!ValidBackupID(id)) throw new Exception("Choose a saved backup.");
        var bytes = File.ReadAllBytes(Path.Combine(BackupDirectory, id + ".bak"));
        if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != id) throw new Exception("This backup failed its integrity check.");
        var destination = Path.GetFullPath(S(r, "path"));
        if (File.Exists(destination)) throw new Exception("Choose a new file name to keep existing saves intact.");
        if (destination.StartsWith(BackupDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new Exception("Export outside the backup folder.");
        using (var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) file.Write(bytes);
        return new { path = destination };
    }
    object DiscoverSaves(JsonElement r)
    {
        var root = Path.GetFullPath(S(r, "path"));
        if (!Directory.Exists(root)) throw new Exception("Choose a save folder.");
        var results = new List<object>();
        var options = new EnumerationOptions { RecurseSubdirectories = B(r,"recursive"), IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 12 };
        int examined = 0, unreadable = 0;
        long bytesExamined = 0;
        bool limited = false;
        foreach (var path in Directory.EnumerateFiles(root, "*", options))
        {
            if (++examined > 10000 || results.Count >= 500) { limited = true; break; }
            try
            {
                var info = new FileInfo(path);
                if (info.Length < 512 || info.Length > 64 * 1024 * 1024) continue;
                var extension = info.Extension.ToLowerInvariant();
                if (extension is not ("" or ".sav" or ".dat" or ".bin" or ".dsv" or ".gci" or ".raw" or ".bak")) continue;
                bytesExamined += info.Length;
                if (bytesExamined > 512L * 1024 * 1024) { limited = true; break; }
                var sav = SaveUtil.GetSaveFile(File.ReadAllBytes(path), path);
                if (sav is null) continue;
                results.Add(new { path, name = info.Name, game = GameInfo.GetVersionName(sav.Version), trainer = sav.OT, modified = info.LastWriteTimeUtc.ToString("O"), size = info.Length });
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or IndexOutOfRangeException or InvalidDataException) { unreadable++; }
        }
        return new { entries = results, examined = Math.Min(examined, 10000), unreadable, limited };
    }
}
