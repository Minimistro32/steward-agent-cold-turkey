#!/bin/sh
cd "$(dirname "$0")" || exit 1
bundle="$PWD/Steward Cold Turkey.app"
if ! /usr/bin/codesign --verify --deep --strict "$bundle"; then
    printf '\nRun Sign Steward.command successfully first.\nPress Enter to close.\n'
    read -r reply
    exit 1
fi
if ! /usr/bin/codesign -dv "$bundle" 2>&1 | /usr/bin/grep -q '^Authority=Developer ID Application:'; then
    echo "Developer ID signature missing. Run Sign Steward.command first."
    read -r reply
    exit 1
fi
/usr/bin/open "$bundle"
