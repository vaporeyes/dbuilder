#!/usr/bin/env bash
# ABOUTME: Wraps a published macOS DBuilder Editor runtime directory in an unsigned .app bundle.
# ABOUTME: Keeps macOS package layout reproducible before signing, notarization, or installer work exists.
set -euo pipefail

rid="${1:-osx-arm64}"
case "$rid" in
    osx-arm64|osx-x64) ;;
    *)
        echo "unsupported macOS runtime id: $rid" >&2
        exit 2
        ;;
esac

output_root="${OUTPUT_ROOT:-artifacts/release}"
package_root="${PACKAGE_ROOT:-artifacts/package/macos}"
publish_dir="$output_root/$rid"
app_dir="$package_root/$rid/DBuilder.Editor.app"
contents_dir="$app_dir/Contents"
macos_dir="$contents_dir/MacOS"
resources_dir="$contents_dir/Resources"

if [[ ! -f "$publish_dir/DBuilder.Editor" ]]; then
    echo "published editor host not found: $publish_dir/DBuilder.Editor" >&2
    exit 2
fi

mkdir -p "$macos_dir" "$resources_dir"
cp -R "$publish_dir/." "$macos_dir/"
chmod +x "$macos_dir/DBuilder.Editor"

if [[ -f "$macos_dir/main.png" ]]; then
    cp "$macos_dir/main.png" "$resources_dir/main.png"
fi

cat > "$contents_dir/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>DBuilder.Editor</string>
    <key>CFBundleIdentifier</key>
    <string>dev.jsh.dbuilder.editor</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>DBuilder Editor</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
PLIST

printf "APPL????" > "$contents_dir/PkgInfo"
echo "macOS app package: $app_dir"
