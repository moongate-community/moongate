#!/usr/bin/env bash
# Regenerates THIRD-PARTY-NOTICES.md from the dependency graph of the shard host,
# which transitively covers every package this repository ships.
#
# Build-only packages such as analyzers and source generators appear in the list
# too. Naming a package that is never distributed costs nothing, while leaving one
# out would drop a notice that has to travel with the binaries.
set -euo pipefail

cd "$(dirname "$0")/.."

output=THIRD-PARTY-NOTICES.md
table=$(mktemp)
trap 'rm -f "$table"' EXIT

dotnet tool restore >/dev/null
dotnet nuget-license \
    --input src/Moongate.Server/Moongate.Server.csproj \
    --include-transitive \
    --output Markdown \
    --file-output "$table"

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
    cat "$table"
} >"$output"

echo "Wrote $output"
