#!/usr/bin/env bash
# Wraps a published osx-* output folder into a Vault.app bundle.
# Usage: scripts/bundle-macos.sh <publish-dir> [output.app]
set -euo pipefail

PUBLISH_DIR="${1:?usage: bundle-macos.sh <publish-dir> [output.app]}"
APP="${2:-Vault.app}"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH_DIR"/. "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/Vault"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Vault</string>
  <key>CFBundleDisplayName</key><string>Vault</string>
  <key>CFBundleIdentifier</key><string>com.vault.app</string>
  <key>CFBundleVersion</key><string>1.0.0</string>
  <key>CFBundleShortVersionString</key><string>1.0.0</string>
  <key>CFBundleExecutable</key><string>Vault</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>LSMinimumSystemVersion</key><string>12.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST

echo "Created $APP"
