#!/bin/sh
# Invoked by Terminal when Finder opens the app. Paths never become shell code.
contents=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd) || exit 1
"$contents/MacOS/Steward.ColdTurkeyAgent" menu
result=$?
printf '\nPress Enter to close this menu window.\n'
read -r reply
exit "$result"
