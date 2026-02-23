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
AOT_JSON="$TMP_DIR/aot.json"
REPORT_MD="$ARTIFACTS_DIR/aot-vs-jit.md"

echo "Running JIT comparison benchmarks..."
dotnet run -c Release --project "$PROJECT_PATH" -- --json --iterations "$ITERATIONS" --output "$JIT_JSON"

echo "Publishing NativeAOT comparison runner..."
dotnet publish "$PROJECT_PATH" -c Release -r "$RID" --self-contained true -o "$TMP_DIR/aot"

AOT_BIN="$TMP_DIR/aot/Moongate.Benchmarks.Compare"
if [[ ! -x "$AOT_BIN" ]]; then
  echo "AOT binary not found: $AOT_BIN" >&2
  exit 1
fi

echo "Running NativeAOT comparison benchmarks..."
"$AOT_BIN" --json --iterations "$ITERATIONS" --output "$AOT_JSON"

python3 - <<'PY' "$JIT_JSON" "$AOT_JSON" "$REPORT_MD"
import json
import sys
from datetime import datetime, timezone

jit_path, aot_path, report_path = sys.argv[1], sys.argv[2], sys.argv[3]
with open(jit_path, "r", encoding="utf-8") as f:
    jit = {x["name"]: x for x in json.load(f)}
with open(aot_path, "r", encoding="utf-8") as f:
    aot = {x["name"]: x for x in json.load(f)}

names = sorted(set(jit.keys()) & set(aot.keys()))

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
lines.append("# AOT vs JIT Benchmark Snapshot")
lines.append("")
lines.append(f"- Generated at: {datetime.now(timezone.utc).isoformat()}")
lines.append("")
lines.append("| Benchmark | JIT Mean | AOT Mean | Speedup (JIT/AOT) | JIT Alloc/op | AOT Alloc/op |")
lines.append("|---|---:|---:|---:|---:|---:|")
for name in names:
    j = jit[name]
    a = aot[name]
    speedup = j["meanNanoseconds"] / a["meanNanoseconds"] if a["meanNanoseconds"] > 0 else 0
    lines.append(
        f"| `{name}` | `{fmt_ns(j['meanNanoseconds'])}` | `{fmt_ns(a['meanNanoseconds'])}` | `{speedup:.2f}x` | `{fmt_bytes(j['allocatedBytesPerOperation'])}` | `{fmt_bytes(a['allocatedBytesPerOperation'])}` |"
    )

with open(report_path, "w", encoding="utf-8") as f:
    f.write("\n".join(lines) + "\n")

print("\n".join(lines))
PY

echo "Saved report to: $REPORT_MD"
