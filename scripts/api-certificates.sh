#!/usr/bin/env bash
# Creates the private certificate authority and the leaf certificates that
# Moongate.Api validates: a CA that may only sign certificates, server leaves
# with the serverAuth usage and a DNS name equal to the host clients connect
# to, client leaves with the clientAuth usage. Every leaf is printed with the
# SHA-256 fingerprint that ApiTlsOptions.PeersByCertificateSha256 expects.
#
# Usage:
#   scripts/api-certificates.sh init [--out DIR] [--days N]
#   scripts/api-certificates.sh issue server <dns-name> [--out DIR] [--days N]
#   scripts/api-certificates.sh issue client <name> [--out DIR] [--days N]
#   scripts/api-certificates.sh fingerprint <name> [--out DIR]
#
# Output (PEM, plus PKCS#12 for X509CertificateLoader.LoadPkcs12FromFile):
#   DIR/ca.crt DIR/ca.key DIR/<name>.crt DIR/<name>.key DIR/<name>.pfx
# Private keys are written with mode 0600. The PKCS#12 password is read from
# MOONGATE_PFX_PASSWORD, or prompted for on a terminal; it never comes from an
# argument. Revocation is removal of the fingerprint from the allowlist, so
# no CRL or OCSP material is produced. Nothing here is renewed automatically.
set -euo pipefail

usage() {
    sed -n '2,19p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'
    exit "${1:-2}"
}

fail() {
    printf 'api-certificates: %s\n' "$1" >&2
    exit 1
}

command -v openssl >/dev/null 2>&1 || fail "openssl is not on the PATH"

repository="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
out="$repository/artifacts/api-certs"
days=""
positional=()
scratch=""
request=""
# Paths of a leaf being issued; cleared once every file is in place, so a
# failure half-way never leaves a private key next to an empty certificate.
partial=""
cleanup() {
    rm -f -- "$scratch" "$request"

    if [ -n "$partial" ]; then
        rm -f -- "$partial.crt" "$partial.key" "$partial.pfx"
        printf 'api-certificates: removed the partially issued files for %s\n' "$partial" >&2
    fi
}
trap cleanup EXIT

while [ $# -gt 0 ]; do
    case "$1" in
        --out)
            [ $# -ge 2 ] && [ "${2#-}" = "$2" ] || fail "--out needs a directory"
            out="$2"
            shift 2
            ;;
        --days)
            [ $# -ge 2 ] && [ "${2#-}" = "$2" ] || fail "--days needs a number"
            days="$2"
            shift 2
            ;;
        -h | --help)
            usage 0
            ;;
        -*)
            fail "unknown option $1"
            ;;
        *)
            positional+=("$1")
            shift
            ;;
    esac
done

[ ${#positional[@]} -ge 1 ] || usage
command="${positional[0]}"

if [ -n "$days" ] && ! [[ "$days" =~ ^[1-9][0-9]*$ ]]; then
    fail "--days must be a positive integer"
fi

require_name() {
    [[ "$1" =~ ^[A-Za-z0-9][A-Za-z0-9.-]*$ ]] || fail "'$1' is not a valid name: letters, digits, dots and dashes only"
}

refuse_existing() {
    [ ! -e "$1" ] || fail "$1 already exists; remove it first if you really mean to replace it"
}

fingerprint_of() {
    # Same shape as X509Certificate2.GetCertHashString(HashAlgorithmName.SHA256):
    # upper-case hexadecimal, no separators.
    openssl x509 -in "$1" -noout -fingerprint -sha256 | sed 's/^.*=//; s/://g'
}

init() {
    [ ${#positional[@]} -eq 1 ] || usage
    local ca_days="${days:-3650}"
    mkdir -p "$out"
    refuse_existing "$out/ca.crt"
    refuse_existing "$out/ca.key"
    (
        umask 077
        openssl genpkey -quiet -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out "$out/ca.key"
    )
    openssl req -x509 -new -key "$out/ca.key" -sha256 -days "$ca_days" \
        -subj "/CN=Moongate API CA" \
        -addext "basicConstraints=critical,CA:TRUE,pathlen:0" \
        -addext "keyUsage=critical,keyCertSign" \
        -addext "subjectKeyIdentifier=hash" \
        -out "$out/ca.crt"
    printf 'Created CA %s (valid %s days)\n' "$out/ca.crt" "$ca_days"
    printf 'Keep %s private; TrustedRoots takes %s.\n' "$out/ca.key" "$out/ca.crt"
}

issue() {
    [ ${#positional[@]} -eq 3 ] || usage
    local role="${positional[1]}"
    local name="${positional[2]}"
    local leaf_days="${days:-730}"
    local usage_oid
    case "$role" in
        server) usage_oid="serverAuth" ;;
        client) usage_oid="clientAuth" ;;
        *) fail "role must be 'server' or 'client', not '$role'" ;;
    esac
    require_name "$name"
    [ -f "$out/ca.crt" ] && [ -f "$out/ca.key" ] || fail "no CA in $out; run 'init' first"
    refuse_existing "$out/$name.crt"
    refuse_existing "$out/$name.key"
    refuse_existing "$out/$name.pfx"

    local password
    if [ -n "${MOONGATE_PFX_PASSWORD:-}" ]; then
        password="$MOONGATE_PFX_PASSWORD"
    elif [ -t 0 ]; then
        read -r -s -p "PKCS#12 password for $name.pfx: " password
        printf '\n'
        [ -n "$password" ] || fail "the PKCS#12 password cannot be empty"
    else
        fail "set MOONGATE_PFX_PASSWORD or run on a terminal to be prompted"
    fi

    scratch="$(mktemp)"
    cat >"$scratch" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature
extendedKeyUsage=$usage_oid
subjectAltName=DNS:$name
subjectKeyIdentifier=hash
authorityKeyIdentifier=keyid
EOF

    partial="$out/$name"
    (
        umask 077
        openssl genpkey -quiet -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$out/$name.key"
    )
    request="$(mktemp)"
    openssl req -new -key "$out/$name.key" -sha256 -subj "/CN=$name" -out "$request"
    openssl x509 -req -in "$request" -CA "$out/ca.crt" -CAkey "$out/ca.key" -CAcreateserial \
        -days "$leaf_days" -sha256 -extfile "$scratch" -out "$out/$name.crt"
    (
        umask 077
        MOONGATE_PFX_PASSWORD="$password" openssl pkcs12 -export \
            -inkey "$out/$name.key" -in "$out/$name.crt" -certfile "$out/ca.crt" \
            -name "$name" -passout env:MOONGATE_PFX_PASSWORD -out "$out/$name.pfx"
    )
    partial=""
    printf 'Issued %s certificate %s (valid %s days)\n' "$role" "$out/$name.crt" "$leaf_days"
    printf 'SHA-256 fingerprint for PeersByCertificateSha256: %s\n' "$(fingerprint_of "$out/$name.crt")"
}

fingerprint() {
    [ ${#positional[@]} -eq 2 ] || usage
    local name="${positional[1]}"
    require_name "$name"
    [ -f "$out/$name.crt" ] || fail "no certificate named $name in $out"
    fingerprint_of "$out/$name.crt"
}

case "$command" in
    init) init ;;
    issue) issue ;;
    fingerprint) fingerprint ;;
    *) usage ;;
esac
