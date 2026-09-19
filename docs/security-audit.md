# Dependency security audit

The `Security Audit` workflow checks all NuGet dependencies in `Moongate.slnx`,
including transitive and test dependencies, using NuGet's vulnerability database.

It runs only on `main`: after each push, daily at 03:17 UTC, and through
`workflow_dispatch` when `main` is selected. Pull requests and `develop` do not
run this workflow. The schedule becomes active when the workflow reaches `main`,
the repository's default branch.

## Failure policy

- High and critical vulnerabilities fail the job (`NU1903`, `NU1904`).
- Low and moderate vulnerabilities are reported as warnings (`NU1901`, `NU1902`).
- Audit-source errors fail the job (`NU1900`, `NU1905`), as do restore or report
  generation failures.

Each run forces dependency resolution and bypasses the HTTP cache to refresh
advisory data. The workflow uses the same .NET 10 SDK container and runner labels
as CI, with read-only repository permissions.

The run summary includes the restore/audit log and the report-generation status.
The `security-audit` artifact retains the restore log and JSON vulnerability
report for 14 days, including when high or critical findings fail the job. If
restore cannot complete, the log remains available even when a complete JSON
report cannot be generated.

## Run locally

From the repository root with the .NET 10 SDK installed:

```bash
dotnet restore Moongate.slnx --force --no-http-cache \
  -p:NuGetAudit=true \
  -p:NuGetAuditMode=all \
  -p:NuGetAuditLevel=low \
  -warnaserror:NU1900,NU1903,NU1904,NU1905

dotnet package list --project Moongate.slnx \
  --vulnerable --include-transitive --no-restore \
  --format json --output-version 1
```

The restore command enforces the severity policy. The package-list command
generates the report; its exit code alone is not a vulnerability gate.

For a transitive finding, use `dotnet nuget why Moongate.slnx <package-id>` to
identify the dependency that brings the affected package into the solution.

See [NuGet auditing documentation](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages)
for warning codes and severity configuration.
