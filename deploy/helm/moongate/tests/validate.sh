#!/usr/bin/env bash
# Lint every CI value set, run the render assertions and validate the manifests against the Kubernetes schemas.
set -euo pipefail
cd "$(dirname "$0")/.."
for values in ci/*.yaml; do
    helm lint --strict . -f "$values"
    helm template t . -f "$values" | kubeconform -strict -summary -kubernetes-version "${KUBERNETES_VERSION:-1.30.0}"
done
tests/test.sh
