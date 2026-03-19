#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/benchmarks/Moongate.Benchmarks.Compare/Moongate.Benchmarks.Compare.csproj"
ARTIFACTS_DIR="$REPO_ROOT/BenchmarkDotNet.Artifacts/results"
TMP_DIR="$REPO_ROOT/benchmarks/.compare"
RID="${RID:-osx-arm64}"
ITERATIONS="${ITERATIONS:-200000}"

mkdir -p "$ARTIFACTS_DIR" "$TMP_DIR"

JIT_JSON="$TMP_DIR/jit.json"
SECOND_JSON="$TMP_DIR/second-run.json"
REPORT_MD="$ARTIFACTS_DIR/jit-benchmark-repeatability.md"

echo "Running JIT comparison benchmarks..."
dotnet run -c Release --project "$PROJECT_PATH" -- --json --iterations "$ITERATIONS" --output "$JIT_JSON"

echo "Running second JIT comparison pass..."
dotnet run -c Release --project "$PROJECT_PATH" -- --json --iterations "$ITERATIONS" --output "$SECOND_JSON"

python3 - <<'PY' "$JIT_JSON" "$SECOND_JSON" "$REPORT_MD"
import json
import sys
from datetime import datetime, timezone

jit_path, second_path, report_path = sys.argv[1], sys.argv[2], sys.argv[3]
with open(jit_path, "r", encoding="utf-8") as f:
    jit = {x["name"]: x for x in json.load(f)}
with open(second_path, "r", encoding="utf-8") as f:
    second = {x["name"]: x for x in json.load(f)}

names = sorted(set(jit.keys()) & set(second.keys()))

def fmt_ns(value: float) -> str:
    if value >= 1000:
        return f"{value / 1000.0:.2f} us"
    return f"{value:.2f} ns"

def fmt_bytes(value: float) -> str:
    if value <= 0.0:
        return "-"
    if value >= 1024:
        return f"{value / 1024.0:.2f} KB"
    return f"{value:.2f} B"

lines = []
lines.append("# JIT Benchmark Repeatability Snapshot")
lines.append("")
lines.append(f"- Generated at: {datetime.now(timezone.utc).isoformat()}")
lines.append("")
lines.append("| Benchmark | JIT Mean (run 1) | JIT Mean (run 2) | Delta (run2/run1) | Alloc/op run 1 | Alloc/op run 2 |")
lines.append("|---|---:|---:|---:|---:|---:|")
for name in names:
    j = jit[name]
    s = second[name]
    delta = s["meanNanoseconds"] / j["meanNanoseconds"] if j["meanNanoseconds"] > 0 else 0
    lines.append(
        f"| `{name}` | `{fmt_ns(j['meanNanoseconds'])}` | `{fmt_ns(s['meanNanoseconds'])}` | `{delta:.2f}x` | `{fmt_bytes(j['allocatedBytesPerOperation'])}` | `{fmt_bytes(s['allocatedBytesPerOperation'])}` |"
    )

with open(report_path, "w", encoding="utf-8") as f:
    f.write("\n".join(lines) + "\n")

print("\n".join(lines))
PY

echo "Saved report to: $REPORT_MD"
