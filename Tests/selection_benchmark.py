"""Headless warm selection latency; measures bridge work, not rendered UI frames."""
import json, pathlib, statistics, subprocess, sys, tempfile, time

def measure(executable):
    with tempfile.TemporaryDirectory(prefix="keepsake-benchmark-") as directory:
        process = subprocess.Popen([str(pathlib.Path(executable).resolve()), str(pathlib.Path(directory)/"settings.json")], stdin=subprocess.PIPE, stdout=subprocess.PIPE, text=True)
        def call(payload):
            start = time.perf_counter()
            process.stdin.write(json.dumps(payload)+"\n"); process.stdin.flush()
            line = process.stdout.readline()
            elapsed = (time.perf_counter()-start)*1000
            response = json.loads(line)
            assert response["ok"], response.get("error")
            return elapsed, len(line.encode())
        try:
            call({"op":"demo", "version":"PLA"})
            samples = [call({"op":"select", "box":0, "slot":index%6, "party":False}) for index in range(65)][5:]
            times = sorted(t for t,_ in samples)
            return {"median_ms":round(statistics.median(times),2),"p95_ms":round(times[56],2),"response_bytes":int(statistics.median(n for _,n in samples))}
        finally:
            process.stdin.close(); process.wait(timeout=10)

for round_number in range(3):
    # Alternate order to reduce warm-up/order bias when comparing two versions.
    paths = sys.argv[1:] if round_number%2 == 0 else list(reversed(sys.argv[1:]))
    for executable in paths:
        print(json.dumps({"round":round_number+1,"bridge":executable,**measure(executable)}),flush=True)
