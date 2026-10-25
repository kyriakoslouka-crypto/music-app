#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
export DOTNET_CLI_HOME=/workspace/.dotnet-cli
export NUGET_PACKAGES=/workspace/.nuget
export XDG_DATA_HOME=/workspace/.local-share
export XDG_RUNTIME_DIR=/workspace/.runtime
export DOTNET_CLI_TELEMETRY_OPTOUT=1
mkdir -p "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
studio_dotnet=/workspace/.dotnet/dotnet
if [[ ! -x "$studio_dotnet" ]]; then
  echo 'Install .NET SDK 10.0.402 in /workspace/.dotnet from the official Microsoft distribution first.' >&2
  exit 1
fi
"$studio_dotnet" restore ProfessionalEarTrainingStudio.sln --locked-mode
"$studio_dotnet" build ProfessionalEarTrainingStudio.sln -c Release --no-restore
"$studio_dotnet" run --project tests/Studio.Tests -c Release --no-build -- "$@"

"$studio_dotnet" run --project tests/Studio.AudioOutput.Tests -c Release --no-build -- --report artifacts/audio-output-check.json
