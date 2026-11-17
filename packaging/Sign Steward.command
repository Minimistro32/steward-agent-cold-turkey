#!/bin/bash
# Final signing runs only on the Mac holding your Developer ID private key.
set -euo pipefail
cd "$(dirname "$0")"
trap 'result=$?; if [ "$result" -ne 0 ]; then printf "\nSigning failed. Do not distribute/install this app yet.\n"; fi; printf "\nPress Enter to close.\n"; read -r reply || true; exit "$result"' EXIT
[ "$(uname -s)" = Darwin ] || { echo "Run this on your Mac."; exit 1; }
bundle="$PWD/Steward Cold Turkey.app"
main="$bundle/Contents/MacOS/Steward.ColdTurkeyAgent"
[ -f "$main" ] || { echo "Agent bundle is missing."; exit 1; }
if /bin/ps -axo command= | /usr/bin/grep -F "$main" | /usr/bin/grep -v grep >/dev/null; then
    echo "Stop this agent and close its menu before signing."; exit 1
fi
identities=()
while IFS= read -r identity; do identities+=("$identity"); done < <(/usr/bin/security find-identity -v -p codesigning | /usr/bin/sed -nE 's/^[[:space:]]*[0-9]+\) ([A-Fa-f0-9]{40}) "(Developer ID Application: .*)"$/\1|\2/p')
if [ "${#identities[@]}" -eq 0 ]; then
    echo "No valid Developer ID Application identity with its private key was found."
    echo "Use Keychain Access to create a certificate signing request, then create"
    echo "Developer ID Application at developer.apple.com and install the .cer on this Mac."
    echo "See SIGNING-AND-INSTALL.txt. Ad-hoc signing is not used."
    exit 1
fi
for ((index=0; index<${#identities[@]}; index++)); do printf '%s) %s\n' "$((index+1))" "${identities[index]#*|}"; done
choice=1
if [ "${#identities[@]}" -gt 1 ]; then
    read -r -p "Select your Developer ID certificate number: " choice
fi
[[ "$choice" =~ ^[0-9]+$ ]] || { echo "Invalid selection."; exit 1; }
[ "$choice" -ge 1 ] && [ "$choice" -le "${#identities[@]}" ] || { echo "Invalid selection."; exit 1; }
selected="${identities[choice-1]}"
fingerprint="${selected%%|*}"
identityName="${selected#*|}"
expectedTeam="${identityName##*(}"
expectedTeam="${expectedTeam%)}"
echo "Signing with: $identityName"
# Sign every loose native component first; the main executable seals the bundle last.
while IFS= read -r -d '' native; do
    [ "$native" = "$main" ] && continue
    if /usr/bin/file -b "$native" | /usr/bin/grep -q 'Mach-O'; then
        /usr/bin/codesign --force --sign "$fingerprint" --options runtime --timestamp "$native"
    fi
done < <(/usr/bin/find "$bundle/Contents/MacOS" -type f -print0)
/usr/bin/codesign --force --sign "$fingerprint" --identifier org.steward.coldturkey.agent --options runtime --timestamp --entitlements "$PWD/entitlements.plist" "$bundle"
/usr/bin/codesign --verify --deep --strict --verbose=2 "$bundle"
details=$(/usr/bin/codesign -dv --verbose=4 "$bundle" 2>&1)
printf '%s\n' "$details"
printf '%s\n' "$details" | /usr/bin/grep -Fqx "Authority=$identityName"
printf '%s\n' "$details" | /usr/bin/grep -Fqx "TeamIdentifier=$expectedTeam"
printf '%s\n' "$details" | /usr/bin/grep -Fqx 'Identifier=org.steward.coldturkey.agent'
echo "Developer ID signature verified. You can now run Create DMG.command."
echo "This release is signed but not notarized. Do not edit the app after signing."
