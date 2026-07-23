#!/usr/bin/env bash
set -euo pipefail

# Runs the Moongate Docker image, mounting the host directories the server needs as volumes rather than
# passing them as plain arguments: inside the container those paths must be reachable and, for the root,
# writable. The image already carries the portal at /app/ui/dist, so nothing extra is needed to serve it.

IMAGE_TAG="${IMAGE_TAG:-moongate-server:local}"
CONTAINER_NAME="${CONTAINER_NAME:-moongate-server}"
ROOT_DIR=""
UO_DIR=""
HTTP_PORT="${HTTP_PORT:-8933}"
GAME_PORT="${GAME_PORT:-2593}"
DETACH="false"

# The container runs as uid 1654 (app), so a root directory owned by the host user would not be writable
# and the server could not seed moongate.yaml, saves or logs. Running as the host user makes every mounted
# directory writable with its existing ownership. Override with --run-as (e.g. --run-as 1654:1654 to keep
# the image's own user, or --run-as root).
RUN_AS="$(id -u):$(id -g)"

# Fixed mount points inside the container. The server is pointed at these, not at the host paths.
ROOT_MOUNT="/data/root"
UO_MOUNT="/data/uo"

usage() {
  cat <<USAGE
Usage: $(basename "$0") -r <root-dir> -u <uo-dir> [options] [-- server-args...]

Run the Moongate server container, mounting the host directories it needs.

Required:
  -r, --root-directory <dir>  Host runtime directory (config, saves, logs). Created if missing;
                              mounted read-write at $ROOT_MOUNT.
  -u, --uo-directory <dir>    Host UO client files (MUL/UOP). Must exist; mounted read-only at $UO_MOUNT.

Options:
  -t, --tag <tag>             Image to run (default: moongate-server:local)
  -n, --name <name>           Container name (default: moongate-server)
      --http-port <port>      Host port mapped to the API/portal on 8933 (default: 8933)
      --game-port <port>      Host port mapped to the game server on 2593 (default: 2593)
      --run-as <uid:gid>      User to run as (default: the host user, so mounts stay writable)
  -d, --detach                Run in the background and print the container id
  -h, --help                  Show this help

Anything after -- is passed straight to Moongate.Server, e.g.:
  $(basename "$0") -r ~/moongate -u ~/uo -- --show-header false
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
  -r | --root-directory)
    ROOT_DIR="$2"
    shift 2
    ;;
  -u | --uo-directory)
    UO_DIR="$2"
    shift 2
    ;;
  -t | --tag)
    IMAGE_TAG="$2"
    shift 2
    ;;
  -n | --name)
    CONTAINER_NAME="$2"
    shift 2
    ;;
  --http-port)
    HTTP_PORT="$2"
    shift 2
    ;;
  --game-port)
    GAME_PORT="$2"
    shift 2
    ;;
  --run-as)
    RUN_AS="$2"
    shift 2
    ;;
  -d | --detach)
    DETACH="true"
    shift
    ;;
  -h | --help)
    usage
    exit 0
    ;;
  --)
    shift
    break
    ;;
  *)
    echo "Unknown option: $1" >&2
    usage
    exit 1
    ;;
  esac
done

# Everything past -- is forwarded to the server verbatim.
SERVER_ARGS=("$@")

if [[ -z "$ROOT_DIR" || -z "$UO_DIR" ]]; then
  echo "Both --root-directory and --uo-directory are required." >&2
  usage
  exit 1
fi

# The root directory is created before the run: if it is left to Docker, the daemon makes it owned by
# root, and then the --run-as user cannot write to the very directory the server needs.
mkdir -p "$ROOT_DIR"
ROOT_DIR="$(cd "$ROOT_DIR" && pwd)"

if [[ ! -d "$UO_DIR" ]]; then
  echo "UO directory not found: $UO_DIR" >&2
  exit 1
fi
UO_DIR="$(cd "$UO_DIR" && pwd)"

if ! docker image inspect "$IMAGE_TAG" >/dev/null 2>&1; then
  echo "Image not found: $IMAGE_TAG" >&2
  echo "Build it first: scripts/build_image.sh --tag $IMAGE_TAG" >&2
  exit 1
fi

RUN_CMD=(
  docker run --rm
  --name "$CONTAINER_NAME"
  --user "$RUN_AS"
  -p "$HTTP_PORT:8933"
  -p "$GAME_PORT:2593"
  -v "$ROOT_DIR:$ROOT_MOUNT"
  -v "$UO_DIR:$UO_MOUNT:ro"
)

if [[ "$DETACH" == "true" ]]; then
  RUN_CMD+=(-d)
else
  # A TTY so Ctrl-C reaches the server and the coloured log stays readable.
  RUN_CMD+=(-it)
fi

RUN_CMD+=(
  "$IMAGE_TAG"
  --root-directory "$ROOT_MOUNT"
  --uo-directory "$UO_MOUNT"
  "${SERVER_ARGS[@]}"
)

echo "Running $IMAGE_TAG as $CONTAINER_NAME"
echo "  root : $ROOT_DIR -> $ROOT_MOUNT (rw)"
echo "  uo   : $UO_DIR -> $UO_MOUNT (ro)"
echo "  ports: http $HTTP_PORT->8933, game $GAME_PORT->2593"
echo "  portal: http://localhost:$HTTP_PORT/"
echo

"${RUN_CMD[@]}"
