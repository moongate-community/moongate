#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_PATH="$REPO_ROOT/benchmarks/Moongate.Benchmarks/Moongate.Benchmarks.csproj"

echo "Running Lua script engine benchmarks..."
dotnet run -c Release --project "$PROJECT_PATH" -- --filter "*LuaScript*" "$@"
