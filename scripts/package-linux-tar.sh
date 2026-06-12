#!/usr/bin/env bash
# ABOUTME: Packages a published Linux DBuilder Editor runtime directory as an unsigned tar.gz archive.
# ABOUTME: Keeps Linux release artifacts reproducible before distro-specific installer work exists.
set -euo pipefail

rid="${1:-linux-x64}"
case "$rid" in
    linux-x64) ;;
    *)
        echo "unsupported Linux runtime id: $rid" >&2
        exit 2
        ;;
esac

output_root="${OUTPUT_ROOT:-artifacts/release}"
package_root="${PACKAGE_ROOT:-artifacts/package/linux}"
publish_dir="$output_root/$rid"
package_dir="$package_root/$rid"

if [[ ! -f "$publish_dir/DBuilder.Editor" ]]; then
    echo "published editor host not found: $publish_dir/DBuilder.Editor" >&2
    exit 2
fi

mkdir -p "$package_dir"
archive="$(cd "$package_dir" && pwd -P)/DBuilder.Editor-$rid.tar.gz"
chmod +x "$publish_dir/DBuilder.Editor"
tar -C "$publish_dir" -czf "$archive" .

echo "Linux tar package: $archive"
