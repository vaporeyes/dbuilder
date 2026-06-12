#!/usr/bin/env bash
# ABOUTME: Packages a published Windows DBuilder Editor runtime directory as an unsigned zip archive.
# ABOUTME: Keeps Windows release artifacts reproducible before installer or code-signing work exists.
set -euo pipefail

rid="${1:-win-x64}"
case "$rid" in
    win-x64) ;;
    *)
        echo "unsupported Windows runtime id: $rid" >&2
        exit 2
        ;;
esac

output_root="${OUTPUT_ROOT:-artifacts/release}"
package_root="${PACKAGE_ROOT:-artifacts/package/windows}"
publish_dir="$output_root/$rid"
package_dir="$package_root/$rid"
archive="$package_dir/DBuilder.Editor-$rid.zip"

if [[ ! -f "$publish_dir/DBuilder.Editor.exe" ]]; then
    echo "published editor host not found: $publish_dir/DBuilder.Editor.exe" >&2
    exit 2
fi

mkdir -p "$package_dir"
archive="$(cd "$package_dir" && pwd -P)/DBuilder.Editor-$rid.zip"
(
    cd "$publish_dir"
    zip -qr "$archive" .
)

echo "Windows zip package: $archive"
