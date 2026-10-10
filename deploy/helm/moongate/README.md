# Moongate Helm chart

Runs one Moongate login and any number of realms on Kubernetes. The full guide is in
[docs/kubernetes.md](../../../docs/kubernetes.md).

```sh
helm install moongate oci://ghcr.io/moongate-community/charts/moongate --version <release> \
  -n moongate --create-namespace -f values.yaml
```

From a checkout, use `deploy/helm/moongate` in place of the `oci://` address.

## Checks

`tests/validate.sh` lints every value set in `ci/`, validates the manifests with `kubeconform`
against two Kubernetes versions and runs the render assertions in `tests/test.sh`.
`../ci-tools.sh` installs the pinned `helm` and `kubeconform`.

## Publication

The release workflow packages the chart with the release version and pushes it to
`oci://ghcr.io/moongate-community/charts`. After the first push, an owner of the organisation
must set the package `charts/moongate` to public in the GitHub package settings; new packages
are private, and an anonymous `helm install` fails with 401 until then. release-please keeps
`version` and `appVersion` in `Chart.yaml` equal to the release; check on the first release PR
that `appVersion` is still a string.
