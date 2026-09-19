#!/usr/bin/env bash
# Build the seven library packages and verify their contents and README examples.
set -euo pipefail

task_repository="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$task_repository"
mkdir -p artifacts
task_package_dir="$(mktemp -d "$task_repository/artifacts/nuget.XXXXXX")"

printf 'Package output: %s\n' "$task_package_dir"
dotnet pack Moongate.slnx -c Release -o "$task_package_dir" -p:ContinuousIntegrationBuild=true
dotnet run --file scripts/VerifyNuGetPackages.cs -- "$task_repository" "$task_package_dir"
dotnet run --file scripts/VerifyNuGetConsumers.cs -- "$task_repository" "$task_package_dir"
printf 'Verified seven libraries and their consumers: %s\n' "$task_package_dir"
