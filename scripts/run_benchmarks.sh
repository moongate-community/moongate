#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/benchmarks/Moongate.Benchmarks/Moongate.Benchmarks.csproj"

if [[ ! -f "$PROJECT_PATH" ]]; then
  echo "Benchmark project not found: $PROJECT_PATH" >&2
  exit 1
fi

dotnet run -c Release --project "$PROJECT_PATH" -- --exporters markdown csv "$@"
