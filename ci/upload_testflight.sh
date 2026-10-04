#!/bin/bash
# Unity Build Automation post-build hook (iOS target).
# Uploads the finished .ipa to App Store Connect / TestFlight with an App Store Connect API key.
#
# Set these as environment variables on the iOS build target in Unity Build Automation (never commit them):
#   ASC_KEY_ID            App Store Connect API key ID (10 characters)
#   ASC_ISSUER_ID         Issuer ID shown on the API keys page
#   ASC_KEY_P8_BASE64     Contents of AuthKey_<KEYID>.p8, base64 encoded on one line
# If any are missing, the script skips the upload and the build still succeeds.
set -euo pipefail

if [ "$(uname)" != "Darwin" ]; then echo "[testflight] not a Mac builder, skipping"; exit 0; fi
if [ -z "${ASC_KEY_ID:-}" ] || [ -z "${ASC_ISSUER_ID:-}" ] || [ -z "${ASC_KEY_P8_BASE64:-}" ]; then
  echo "[testflight] ASC_KEY_ID / ASC_ISSUER_ID / ASC_KEY_P8_BASE64 not set, skipping upload"; exit 0
fi

# Find the .ipa: argument first, then the usual Build Automation output folder, then anything recent.
IPA=""
if [ -n "${1:-}" ]; then
  if [ -f "$1" ] && [[ "$1" == *.ipa ]]; then IPA="$1"; elif [ -d "$1" ]; then IPA="$(find "$1" -name '*.ipa' -print -quit)"; fi
fi
if [ -z "$IPA" ] && [ -n "${WORKSPACE:-}" ] && [ -n "${TARGET_NAME:-}" ] && [ -d "$WORKSPACE/.build/last/$TARGET_NAME" ]; then
  IPA="$(find "$WORKSPACE/.build/last/$TARGET_NAME" -name '*.ipa' -print -quit)"
fi
if [ -z "$IPA" ] && [ -n "${WORKSPACE:-}" ]; then
  IPA="$(find "$WORKSPACE" -name '*.ipa' -mmin -180 -print -quit 2>/dev/null || true)"
fi
if [ -z "$IPA" ]; then echo "[testflight] no .ipa found (is code signing set up on this target?)"; exit 1; fi
echo "[testflight] uploading $IPA"

KEY_DIR="$HOME/.appstoreconnect/private_keys"
mkdir -p "$KEY_DIR"
KEY_FILE="$KEY_DIR/AuthKey_${ASC_KEY_ID}.p8"
trap 'rm -f "$KEY_FILE"' EXIT
echo "$ASC_KEY_P8_BASE64" | base64 --decode > "$KEY_FILE"
chmod 600 "$KEY_FILE"

xcrun altool --upload-app -f "$IPA" -t ios --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID" --output-format normal
echo "[testflight] upload finished. Apple processes the build for 5-30 minutes before it shows in TestFlight."
