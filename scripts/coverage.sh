#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: scripts/coverage.sh [fast|all] [dotnet test options]

Runs scripts/test.sh with code coverage collection and merges the per-project
Cobertura files into one report under artifacts/coverage:

  index.html        browsable HTML report
  Summary.md        per-assembly Markdown summary (shown on the CI run page)
  Cobertura.xml     merged Cobertura file

The mode and the remaining options are forwarded to scripts/test.sh, so the
same environment requirements apply (all mode needs PostgreSQL and Redis).

Examples:
  scripts/coverage.sh
  scripts/coverage.sh all -c Release --no-build
EOF
}

case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_root"

results=artifacts/coverage/results
report=artifacts/coverage
rm -rf "$report"

bash scripts/test.sh "$@" \
    --collect "XPlat Code Coverage" \
    --results-directory "$results" \
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura \
    DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute=GeneratedCodeAttribute,CompilerGeneratedAttribute

# Test assemblies, protobuf contracts and source-generated CLI plumbing are not
# hand-written product code, so they stay out of the report.
dotnet tool restore >/dev/null
dotnet reportgenerator \
    "-reports:$results/*/coverage.cobertura.xml" \
    "-targetdir:$report" \
    "-reporttypes:Html;MarkdownAssembliesSummary;Cobertura" \
    "-assemblyfilters:-*.Tests;-Moongate.Api.TestHost;-Moongate.Admin.Contracts" \
    "-classfilters:-ConsoleAppFramework.*;-Cli" \
    "-title:Moongate"

printf 'Coverage report: %s/index.html\n' "$report"
