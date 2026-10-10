#!/usr/bin/env bash
# Installs the pinned helm and kubeconform into $1 (default: ~/.local/bin), checking their SHA-256.
set -euo pipefail

HELM_VERSION=v4.3.0
HELM_SHA256=86584a54def73570558f66f5111cc53dfed56689637ae32c1201205d494f54fb
KUBECONFORM_VERSION=v0.8.0
KUBECONFORM_SHA256=9bc2bffbf71f261128533edaf912153948b7ff238f9a531ae6d34466ec287883

bin=${1:-$HOME/.local/bin}
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir -p "$bin"

fetch() { # <url> <sha256> <archive>
    curl -fsSL "$1" -o "$work/$3"
    echo "$2  $work/$3" | sha256sum -c -
}

fetch "https://get.helm.sh/helm-$HELM_VERSION-linux-amd64.tar.gz" "$HELM_SHA256" helm.tgz
tar -xzf "$work/helm.tgz" -C "$work"
install -m755 "$work/linux-amd64/helm" "$bin/helm"

fetch "https://github.com/yannh/kubeconform/releases/download/$KUBECONFORM_VERSION/kubeconform-linux-amd64.tar.gz" "$KUBECONFORM_SHA256" kubeconform.tgz
tar -xzf "$work/kubeconform.tgz" -C "$work" kubeconform
install -m755 "$work/kubeconform" "$bin/kubeconform"

"$bin/helm" version --short
"$bin/kubeconform" -v
