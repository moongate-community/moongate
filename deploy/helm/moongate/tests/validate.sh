#!/usr/bin/env bash
# Lint every CI value set, run the render assertions and validate the manifests against the Kubernetes schemas.
set -euo pipefail
cd "$(dirname "$0")/.."
for values in ci/*.yaml; do
    helm lint --strict . -f "$values"
    # kubeVersion allows 1.26 and later: check the oldest supported schemas and a recent one.
    for version in 1.26.0 1.32.0; do
        helm template t . -f "$values" | kubeconform -strict -summary -kubernetes-version "$version"
    done
done
tests/test.sh
