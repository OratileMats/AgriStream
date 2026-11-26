#!/usr/bin/env python3
"""
AgriStream load test (task e) - no third-party packages needed (Python 3.8+).

Simulates many concurrent sensors/users hitting the App Service:
  * POST /api/telemetry  (writes to Azure Table Storage - the heavy, realistic path)
  * GET  /health         (cheap anonymous request)
  * GET  /Account/Login  (rendered MVC view)

Usage:
  python loadtest.py --url https://<your-app>.azurewebsites.net --api-key <Ingest:ApiKey> \
                     --users 100 --duration 300 --ramp 60
Run it for several minutes so Azure Monitor / autoscale can react, and screenshot the metrics.
"""
import argparse, json, random, statistics, threading, time, urllib.request, urllib.error
from collections import Counter

p = argparse.ArgumentParser()
p.add_argument("--url", required=True)
p.add_argument("--api-key", required=True)
p.add_argument("--users", type=int, default=50, help="concurrent virtual users")
p.add_argument("--duration", type=int, default=120, help="seconds")
p.add_argument("--ramp", type=int, default=30, help="seconds to ramp up to full user count")
a = p.parse_args()
base = a.url.rstrip("/")

lock = threading.Lock()
latencies, codes, errors = [], Counter(), Counter()
stop_at = time.time() + a.duration
SENSORS = [(f"SEN{n:03d}", f"Farm {'ABC'[(n - 1) // 2 % 3]}") for n in range(1, 7)]


def hit(method, path, body=None, headers=None):
    req = urllib.request.Request(base + path, data=body, method=method, headers=headers or {})
    t0 = time.perf_counter()
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            r.read()
            code = r.status
    except urllib.error.HTTPError as e:
        code = e.code
    except Exception as e:  # timeouts, connection resets, ...
        with lock:
            errors[type(e).__name__] += 1
        return
    dt = (time.perf_counter() - t0) * 1000
    with lock:
        latencies.append(dt)
        codes[code] += 1


def user(delay):
    time.sleep(delay)
    while time.time() < stop_at:
        sid, farm = random.choice(SENSORS)
        payload = json.dumps({
            "sensorId": sid, "farmName": farm,
            "soilMoisture": round(random.uniform(30, 85), 1),   # >25 so no auto-irrigation storm
            "temperatureC": round(random.uniform(15, 38), 1),
            "humidity": round(random.uniform(30, 90), 1)}).encode()
        hit("POST", "/api/telemetry", payload, {"Content-Type": "application/json", "X-Api-Key": a.api_key})
        hit("GET", "/health")
        if random.random() < 0.3:
            hit("GET", "/Account/Login")
        time.sleep(random.uniform(0.05, 0.3))


threads = [threading.Thread(target=user, args=(i * a.ramp / max(a.users, 1),), daemon=True) for i in range(a.users)]
print(f"Starting {a.users} users for {a.duration}s (ramp {a.ramp}s) against {base}")
start = time.time()
for t in threads: t.start()

while time.time() < stop_at:
    time.sleep(10)
    with lock:
        n = len(latencies)
        recent = latencies[-200:]
    if recent:
        print(f"[{int(time.time() - start):>4}s] requests={n:<7} recent avg={statistics.mean(recent):.0f} ms")
for t in threads: t.join(timeout=35)

elapsed = time.time() - start
print("\n=== RESULTS ===")
if latencies:
    s = sorted(latencies)
    pct = lambda q: s[min(len(s) - 1, int(len(s) * q))]
    print(f"Total requests : {len(s)}  ({len(s) / elapsed:.1f} req/s)")
    print(f"Latency ms     : avg {statistics.mean(s):.0f} | p50 {pct(.5):.0f} | p95 {pct(.95):.0f} | p99 {pct(.99):.0f} | max {s[-1]:.0f}")
print("Status codes   :", dict(codes))
print("Failures       :", dict(errors) or "none")
