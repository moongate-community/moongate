#!/usr/bin/env bash
# Regenerates THIRD-PARTY-NOTICES.md from the separate dependency graphs of the
# shard host and migration runner, preserving both versions of shared packages.
#
# Build-only packages such as analyzers and source generators appear in the list
# too. Naming a package that is never distributed costs nothing, while leaving one
# out would drop a notice that has to travel with the binaries.
set -euo pipefail

cd "$(dirname "$0")/.."

output=THIRD-PARTY-NOTICES.md
table=$(mktemp)
runner_table=$(mktemp)
trap 'rm -f "$table" "$runner_table"' EXIT

dotnet tool restore >/dev/null
dotnet nuget-license \
    --input src/Moongate.Server/Moongate.Server.csproj \
    --include-transitive \
    --output Markdown \
    --file-output "$table"
dotnet nuget-license \
    --input src/Moongate.MigrationRunner/Moongate.MigrationRunner.csproj \
    --include-transitive \
    --output Markdown \
    --file-output "$runner_table"

{
    echo "# Third-party notices"
    echo
    echo "Moongate is distributed together with the packages listed below. Each one"
    echo "keeps its own licence and copyright, reproduced here as those licences require."
    echo
    echo "This file is generated. Run \`scripts/third-party-notices.sh\` after adding,"
    echo "removing or upgrading a dependency. Continuous integration regenerates it and"
    echo "fails when the result differs from the copy committed here, so the notices that"
    echo "ship beside the binaries always describe what those binaries contain."
    echo
    echo "## Server"
    echo
    # Keep generated table separators consistent with the Markdown formatter.
    sed '/^|[- |]*|$/s/ /-/g' "$table"
    echo
    echo "## Separate migration runner"
    echo
    echo "The \`migration-runner/\` executable has its own dependency graph. Versions"
    echo "listed here belong to that executable and do not replace server dependencies."
    echo
    sed '/^|[- |]*|$/s/ /-/g' "$runner_table"
} >"$output"

echo "Wrote $output"
