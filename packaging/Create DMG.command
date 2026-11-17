#!/bin/bash
# Creates a distributable image only from a verified Developer ID-signed app.
set -euo pipefail
cd "$(dirname "$0")"
staging=""
trap 'result=$?; if [ -n "$staging" ]; then /bin/rm -rf "$staging"; fi; if [ "$result" -ne 0 ]; then echo "DMG creation failed."; fi; printf "\nPress Enter to close.\n"; read -r reply || true; exit "$result"' EXIT
[ "$(uname -s)" = Darwin ] || { echo "Run this on your Mac."; exit 1; }
bundle="$PWD/Steward Cold Turkey.app"
/usr/bin/codesign --verify --deep --strict "$bundle"
details=$(/usr/bin/codesign -dv --verbose=4 "$bundle" 2>&1)
printf '%s\n' "$details" | /usr/bin/grep -q '^Authority=Developer ID Application:'
printf '%s\n' "$details" | /usr/bin/grep -Fqx 'Identifier=org.steward.coldturkey.agent'
version=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$bundle/Contents/Info.plist")
output="$PWD/Steward-Cold-Turkey-$version.dmg"
[ ! -e "$output" ] && [ ! -e "$output.sha256" ] || { echo "DMG/checksum already exists; it was not overwritten."; exit 1; }
staging=$(/usr/bin/mktemp -d "${TMPDIR:-/tmp}/steward-dmg.XXXXXX")
/usr/bin/ditto "$bundle" "$staging/Steward Cold Turkey.app"
/bin/ln -s /Applications "$staging/Applications"
/bin/cp "$PWD/INSTALL.txt" "$staging/INSTALL.txt"
/usr/bin/codesign --verify --deep --strict "$staging/Steward Cold Turkey.app"
/usr/bin/hdiutil create -volname 'Steward Cold Turkey' -srcfolder "$staging" -format UDZO "$output"
/usr/bin/hdiutil verify "$output"
/usr/bin/shasum -a 256 "$output" > "$output.sha256"
echo "Created: $output"
echo "The contained app is Developer ID-signed. The disk image is not notarized."
echo "Open the image and drag the app to Applications; use ~/Applications if needed."
