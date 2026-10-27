#!/usr/bin/env bash
# Builds and tests the backend inside the .NET 10 SDK container (no local SDK needed).
set -euo pipefail
cd "$(dirname "$0")"
docker run --rm -v "$PWD":/src -v trailhub-nuget:/root/.nuget/packages -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c "${1:-dotnet build TrailHub.slnx -nologo -v q}"
