#!/usr/bin/env bash
# ABOUTME: Publishes unsigned DBuilder Editor release artifacts for supported desktop runtime IDs.
# ABOUTME: Keeps release output paths and dotnet publish flags stable until platform packaging lands.
set -euo pipefail

configuration="${CONFIGURATION:-Release}"
project="${PROJECT:-src/DBuilder.Editor/DBuilder.Editor.csproj}"
output_root="${OUTPUT_ROOT:-artifacts/release}"
self_contained="${SELF_CONTAINED:-true}"

rids=("$@")
if [[ "${#rids[@]}" -eq 0 ]]; then
    rids=(osx-arm64 osx-x64 win-x64 linux-x64)
fi

for rid in "${rids[@]}"; do
    case "$rid" in
        osx-arm64|osx-x64|win-x64|linux-x64) ;;
        *)
            echo "unsupported runtime id: $rid" >&2
            exit 2
            ;;
    esac

    output="$output_root/$rid"
    dotnet publish "$project" \
        --configuration "$configuration" \
        --runtime "$rid" \
        --self-contained "$self_contained" \
        -p:PublishSingleFile=false \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        --output "$output"
done
