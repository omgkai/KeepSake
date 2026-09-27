"""Focused export/backup regression check using a caller-supplied valid save copy."""
import hashlib, json, pathlib, subprocess, sys, tempfile
bridge = str(pathlib.Path(sys.argv[1]).resolve())
source = pathlib.Path(sys.argv[2]).resolve()
original = source.read_bytes()
with tempfile.TemporaryDirectory(prefix="keepsake-export-") as directory:
    root = pathlib.Path(directory)
    settings = root / "settings.json"
    settings.write_text(json.dumps({"BackupOnOpen": True}))
    child = subprocess.Popen([bridge, str(settings)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
    def request(**payload):
        child.stdin.write(json.dumps(payload) + "\n"); child.stdin.flush()
        result = json.loads(child.stdout.readline())
        assert result["ok"], result.get("error")
        return result["data"]
    try:
        opened = request(op="open", path=str(source))
        assert opened["hasSave"]
        backup = root / "Save Backups" / (hashlib.sha256(original).hexdigest() + ".bak")
        assert backup.read_bytes() == original, "Automatic backup differs from original bytes"
        exported = root / "main"
        request(op="exportSave", path=str(exported))
        reopened = request(op="open", path=str(exported))
        assert reopened["hasSave"] and reopened["checksumValid"]
        assert reopened["game"] == opened["game"]
        assert exported.read_bytes() == original, "Unedited export changed serialized save bytes"
        assert source.read_bytes() == original, "Source file changed"
        print("PASS: backup byte identity, export byte identity, fresh recognition, game and checksums; original unchanged")
    finally:
        child.stdin.close(); child.wait(timeout=10)
