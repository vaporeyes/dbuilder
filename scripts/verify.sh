#!/usr/bin/env bash
# ABOUTME: Runs the standard local verification loop for each DBuilder port phase.
# ABOUTME: Builds the solution and runs the full automated test suite.
set -euo pipefail

dotnet restore DBuilder.slnx -m:1
dotnet build DBuilder.slnx --no-restore -m:1
dotnet test DBuilder.slnx --no-build -m:1

if [[ -f rust/Cargo.toml ]]; then
    cargo test --manifest-path rust/Cargo.toml
fi
