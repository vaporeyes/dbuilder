#!/usr/bin/env bash
# ABOUTME: Checks published DBuilder Editor output for launch files and bundled release assets.
# ABOUTME: Runs after release-build.sh to catch missing package layout files before platform installers exist.
set -euo pipefail

output_root="${OUTPUT_ROOT:-artifacts/release}"

rids=("$@")
if [[ "${#rids[@]}" -eq 0 ]]; then
    if [[ ! -d "$output_root" ]]; then
        echo "release output root not found: $output_root" >&2
        exit 2
    fi
    while IFS= read -r path; do
        rids+=("$(basename "$path")")
    done < <(find "$output_root" -mindepth 1 -maxdepth 1 -type d | sort)
fi

if [[ "${#rids[@]}" -eq 0 ]]; then
    echo "no release runtime directories found under $output_root" >&2
    exit 2
fi

status=0
for rid in "${rids[@]}"; do
    output="$output_root/$rid"
    executable="DBuilder.Editor"
    if [[ "$rid" == win-* ]]; then
        executable="DBuilder.Editor.exe"
    fi

    required=(
        "$executable"
        "DBuilder.Editor.dll"
        "DBuilder.Editor.deps.json"
        "DBuilder.Editor.runtimeconfig.json"
        "main.png"
        "assets/Common/Configurations/README.md"
        "assets/Common/Scripting/README.md"
    )

    for relative in "${required[@]}"; do
        if [[ ! -f "$output/$relative" ]]; then
            echo "missing $rid/$relative" >&2
            status=1
        fi
    done

    if [[ "$rid" != win-* && -f "$output/$executable" && ! -x "$output/$executable" ]]; then
        echo "not executable: $rid/$executable" >&2
        status=1
    fi

    if [[ "$status" -eq 0 ]]; then
        echo "package smoke ok: $rid"
    fi
done

exit "$status"
