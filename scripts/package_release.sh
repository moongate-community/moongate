#!/usr/bin/env bash
set -euo pipefail

# Assembles one platform's release zip: the self-contained single-file server, the built portal beside it
# (which the server finds and serves), and the top-level README and LICENSE. CI calls this per RID; it also
# runs locally. One place owns how a release zip is shaped.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

RID=""
VERSION=""
UI_DIST=""
OUT_DIR="$REPO_ROOT/artifacts"

SUPPORTED_RIDS=("linux-x64" "win-x64" "linux-arm64")

usage() {
  cat <<USAGE
Usage: $(basename "$0") --rid <rid> --version <version> [options]

Build one self-contained server zip: executable + portal + README + LICENSE.

Required:
  -r, --rid <rid>          Target runtime: ${SUPPORTED_RIDS[*]}
  -v, --version <version>  Version string used in the zip name (e.g. 0.5.0)

Options:
      --ui-dist <path>     Prebuilt portal directory to bundle. If omitted, the
                           script builds it from ui/ with npm.
      --out <dir>          Output directory (default: ./artifacts)
  -h, --help               Show this help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
  -r | --rid)
    RID="$2"
    shift 2
    ;;
  -v | --version)
    VERSION="$2"
    shift 2
    ;;
  --ui-dist)
    UI_DIST="$2"
    shift 2
    ;;
  --out)
    OUT_DIR="$2"
    shift 2
    ;;
  -h | --help)
    usage
    exit 0
    ;;
  *)
    echo "Unknown option: $1" >&2
    usage
    exit 1
    ;;
  esac
done

if [[ -z "$RID" || -z "$VERSION" ]]; then
  echo "Both --rid and --version are required." >&2
  usage
  exit 1
fi

is_supported="false"
for candidate in "${SUPPORTED_RIDS[@]}"; do
  [[ "$candidate" == "$RID" ]] && is_supported="true"
done
if [[ "$is_supported" != "true" ]]; then
  echo "Unsupported rid: $RID (expected one of: ${SUPPORTED_RIDS[*]})" >&2
  exit 1
fi

# The portal is platform-independent. Prefer a path the caller already built (CI builds it once); fall back
# to a fresh build so a local run with no prior build still works.
if [[ -z "$UI_DIST" ]]; then
  if [[ -f "$REPO_ROOT/ui/dist/index.html" ]]; then
    UI_DIST="$REPO_ROOT/ui/dist"
  else
    echo "No portal build found; building it from ui/"
    npm --prefix "$REPO_ROOT/ui" ci
    npm --prefix "$REPO_ROOT/ui" run build
    UI_DIST="$REPO_ROOT/ui/dist"
  fi
fi

if [[ ! -f "$UI_DIST/index.html" ]]; then
  echo "Portal build has no index.html: $UI_DIST" >&2
  exit 1
fi

PKG_NAME="moongate-server-${VERSION}-${RID}"
PKG_DIR="$OUT_DIR/$PKG_NAME"

rm -rf "$PKG_DIR"
mkdir -p "$PKG_DIR"

echo "Publishing $RID (self-contained, single file)"
dotnet publish "$REPO_ROOT/src/Moongate.Server/Moongate.Server.csproj" \
  -c Release -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:DebugType=embedded \
  -p:GenerateDocumentationFile=false \
  -o "$PKG_DIR"

# Portal beside the executable, then the top-level docs. The parent ui/ is created first so that
# `cp -r <src> <dest>` — with dest absent — copies the portal's contents into a fresh ui/dist.
mkdir -p "$PKG_DIR/ui"
cp -r "$UI_DIST" "$PKG_DIR/ui/dist"
cp "$REPO_ROOT/README.md" "$REPO_ROOT/LICENSE" "$PKG_DIR/"

ZIP_PATH="$OUT_DIR/$PKG_NAME.zip"
rm -f "$ZIP_PATH"
(cd "$OUT_DIR" && zip -r -q "$PKG_NAME.zip" "$PKG_NAME")

# Leave only the zip.
rm -rf "$PKG_DIR"

echo "$ZIP_PATH"
