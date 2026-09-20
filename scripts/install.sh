#!/bin/sh
# Moongate, an Ultima Online server and reusable .NET libraries.
# https://github.com/moongate-community/moongate
#
# Copyright (c) Squid Development. Licensed under the AGPL-3.0-or-later. The full
# licence travels with every release, in LICENSE beside the installed binary.
#
# Why do liches never run this script? They would rather raise things from source.
#
# If that made you smile, email it to tom@orivega.io with your postal address and
# we will send you a Moongate sticker, free.
#
#   curl -fsSL https://moongate.sh/install.sh | sh
#
# It downloads the release archive for this machine, checks it against the checksum
# published beside it, and leaves the archive's contents in /opt/moongate with a
# symlink in /usr/local/bin. It creates no user, service or data directory: the
# first start writes config/moongate.toml and exits until ultima_path names a client.

set -eu

REPOSITORY_URL="https://github.com/moongate-community/moongate"
BASE_URL="${MOONGATE_BASE_URL:-${REPOSITORY_URL}/releases/download}"
INSTALL_DIR="${MOONGATE_INSTALL_DIR:-/opt/moongate}"
BIN_DIR="${MOONGATE_BIN_DIR:-/usr/local/bin}"
SUDO=""
WORK_DIR=""
STAGING_DIR=""
DOWNLOADER=""
CHECKSUM=""

fail() {
    echo "moongate: $1" >&2
    exit 1
}

cleanup() {
    status=$?
    if [ -n "$WORK_DIR" ] && [ -d "$WORK_DIR" ]; then
        rm -rf "$WORK_DIR"
    fi
    if [ -n "$STAGING_DIR" ] && [ -d "$STAGING_DIR" ]; then
        $SUDO rm -rf "$STAGING_DIR"
    fi
    exit "$status"
}

usage() {
    cat <<'USAGE'
Usage: install.sh

Installs the latest Moongate release for this machine.

  MOONGATE_VERSION       version to install, such as 0.4.0 (default: the latest release)
  MOONGATE_RID           linux-x64 or linux-arm64 (default: from uname -m)
  MOONGATE_BASE_URL      release download base URL
  MOONGATE_INSTALL_DIR   install directory (default: /opt/moongate)
  MOONGATE_BIN_DIR       symlink directory (default: /usr/local/bin)
USAGE
}

has() {
    command -v "$1" >/dev/null 2>&1
}

select_tools() {
    if has curl; then
        DOWNLOADER="curl"
    elif has wget; then
        DOWNLOADER="wget"
    else
        fail "curl or wget is required"
    fi

    if has sha256sum; then
        CHECKSUM="sha256sum"
    elif has shasum; then
        CHECKSUM="shasum -a 256"
    else
        fail "sha256sum or shasum is required"
    fi

    if ! has tar; then
        fail "tar is required"
    fi
}

detect_rid() {
    machine=$(uname -m)
    case "$machine" in
        x86_64 | amd64) echo "linux-x64" ;;
        aarch64 | arm64) echo "linux-arm64" ;;
        *) fail "unsupported architecture '${machine}'; the releases ship linux-x64 and linux-arm64" ;;
    esac
}

refuse_musl() {
    if [ -f /etc/alpine-release ]; then
        fail "musl libc is not supported; the release binary needs glibc"
    fi
    if has ldd && ldd --version 2>&1 | grep -qi musl; then
        fail "musl libc is not supported; the release binary needs glibc"
    fi
}

resolve_latest() {
    latest="${REPOSITORY_URL}/releases/latest"
    if [ "$DOWNLOADER" = "curl" ]; then
        resolved=$(curl -fsSLI -o /dev/null -w '%{url_effective}' "$latest" 2>/dev/null || true)
    else
        resolved=$(wget -q -S --spider --max-redirect=0 "$latest" 2>&1 |
            sed -n 's/^[[:space:]]*Location:[[:space:]]*\([^[:space:]]*\).*/\1/p' | tail -n 1)
    fi

    case "$resolved" in
        */tag/v*) echo "${resolved##*/tag/v}" ;;
        *) fail "could not resolve the latest release; set MOONGATE_VERSION" ;;
    esac
}

download() {
    if [ "$DOWNLOADER" = "curl" ]; then
        curl -fsSL "$1" -o "$2"
    else
        wget -q -O "$2" "$1"
    fi
}

writable() {
    directory="$1"
    while [ ! -e "$directory" ]; do
        parent=$(dirname "$directory")
        if [ "$parent" = "$directory" ]; then
            break
        fi
        directory="$parent"
    done

    [ -w "$directory" ]
}

select_privileges() {
    if [ "$(id -u)" = "0" ]; then
        SUDO=""
    elif writable "$(dirname "$INSTALL_DIR")" && writable "$BIN_DIR"; then
        SUDO=""
    elif has sudo; then
        SUDO="sudo"
    else
        fail "root privileges are required; re-run with sudo, or set MOONGATE_INSTALL_DIR and MOONGATE_BIN_DIR to writable paths"
    fi
}

main() {
    case "${1:-}" in
        -h | --help)
            usage
            exit 0
            ;;
        "") ;;
        *) fail "unknown argument '$1'; run with --help" ;;
    esac

    if [ "$(uname -s)" != "Linux" ]; then
        fail "this installer supports Linux only"
    fi

    select_tools
    refuse_musl

    if [ -n "${MOONGATE_RID:-}" ]; then
        rid="$MOONGATE_RID"
    else
        rid=$(detect_rid) || exit 1
    fi

    case "$rid" in
        linux-x64 | linux-arm64) ;;
        *) fail "unsupported architecture '${rid}'; the releases ship linux-x64 and linux-arm64" ;;
    esac

    if [ -n "${MOONGATE_VERSION:-}" ]; then
        version="${MOONGATE_VERSION#v}"
    else
        version=$(resolve_latest) || exit 1
    fi

    archive="moongate-${rid}-${version}.tar.gz"
    url="${BASE_URL}/v${version}/${archive}"

    select_privileges
    WORK_DIR=$(mktemp -d)
    echo "Moongate ${version} ${rid}"

    if ! download "$url" "${WORK_DIR}/${archive}"; then
        fail "release v${version} has no asset for ${rid}"
    fi
    echo "  download   ${archive}"

    if ! download "${url}.sha256" "${WORK_DIR}/${archive}.sha256"; then
        fail "release v${version} has no checksum for ${archive}"
    fi

    if ! (cd "$WORK_DIR" && $CHECKSUM -c "${archive}.sha256" >/dev/null 2>&1); then
        fail "checksum mismatch for ${archive}"
    fi
    echo "  verify     sha256 ok"

    if ! tar -xzf "${WORK_DIR}/${archive}" -C "$WORK_DIR"; then
        fail "the archive could not be extracted"
    fi

    if [ ! -f "${WORK_DIR}/moongate-${rid}/Moongate.Server" ]; then
        fail "the archive does not contain moongate-${rid}/Moongate.Server"
    fi

    STAGING_DIR="${INSTALL_DIR}.new.$$"
    old_dir="${INSTALL_DIR}.old.$$"
    $SUDO rm -rf "$STAGING_DIR" "$old_dir"
    $SUDO mkdir -p "$(dirname "$INSTALL_DIR")" "$BIN_DIR"
    $SUDO cp -R "${WORK_DIR}/moongate-${rid}" "$STAGING_DIR"
    $SUDO chmod 0755 "${STAGING_DIR}/Moongate.Server"

    if [ -e "$INSTALL_DIR" ]; then
        $SUDO mv "$INSTALL_DIR" "$old_dir"
    fi

    $SUDO mv "$STAGING_DIR" "$INSTALL_DIR"
    STAGING_DIR=""
    $SUDO rm -rf "$old_dir"
    echo "  install    ${INSTALL_DIR}"

    $SUDO rm -f "${BIN_DIR}/moongate"
    $SUDO ln -s "${INSTALL_DIR}/Moongate.Server" "${BIN_DIR}/moongate"
    echo "  link       ${BIN_DIR}/moongate"

    cat <<'NEXT'

Next steps:
  moongate --root-directory /srv/moongate
  edit /srv/moongate/config/moongate.toml and set ultima_path
NEXT
}

trap cleanup EXIT INT TERM
main "$@"
