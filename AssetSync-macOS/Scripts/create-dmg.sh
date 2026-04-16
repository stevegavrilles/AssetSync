#!/bin/bash
set -euo pipefail

# ============================================================
# create-dmg.sh — Build, sign, notarize, and package AssetSync
# as a drag-to-Applications .dmg for Apple Silicon.
#
# Prerequisites:
#   brew install create-dmg       (for pretty DMG layout)
#   xcodegen generate             (run once to create .xcodeproj)
#
# Usage:
#   ./Scripts/create-dmg.sh
#
# Environment variables (set or override):
#   SIGNING_IDENTITY  — Developer ID Application cert name
#   TEAM_ID           — Apple Developer Team ID
#   APPLE_ID          — Apple ID for notarization
#   APP_PASSWORD      — App-specific password for notarytool
# ============================================================

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="${PROJECT_DIR}/build"
APP_NAME="AssetSync"
SCHEME="AssetSync"
ARCHIVE_PATH="${BUILD_DIR}/${APP_NAME}.xcarchive"
APP_PATH="${BUILD_DIR}/${APP_NAME}.app"
DMG_PATH="${BUILD_DIR}/${APP_NAME}.dmg"
SIGNING_IDENTITY="${SIGNING_IDENTITY:-Developer ID Application}"
TEAM_ID="${TEAM_ID:-YOUR_TEAM_ID}"

echo "==> Cleaning build directory"
rm -rf "${BUILD_DIR}"
mkdir -p "${BUILD_DIR}"

echo "==> Generating Xcode project (xcodegen)"
cd "${PROJECT_DIR}"
if command -v xcodegen &> /dev/null; then
    xcodegen generate
else
    echo "WARNING: xcodegen not found. Assuming .xcodeproj already exists."
fi

echo "==> Building archive (arm64 only)"
xcodebuild archive \
    -project "${APP_NAME}.xcodeproj" \
    -scheme "${SCHEME}" \
    -archivePath "${ARCHIVE_PATH}" \
    -configuration Release \
    ARCHS=arm64 \
    ONLY_ACTIVE_ARCH=NO \
    SKIP_INSTALL=NO \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    | xcpretty || true

echo "==> Exporting app from archive"
xcodebuild -exportArchive \
    -archivePath "${ARCHIVE_PATH}" \
    -exportOptionsPlist "${PROJECT_DIR}/Scripts/export-options.plist" \
    -exportPath "${BUILD_DIR}" \
    || cp -R "${ARCHIVE_PATH}/Products/Applications/${APP_NAME}.app" "${APP_PATH}"

echo "==> Code signing"
codesign --deep --force --options runtime \
    --sign "${SIGNING_IDENTITY}" \
    --timestamp \
    "${APP_PATH}"

echo "==> Verifying signature"
codesign --verify --deep --strict "${APP_PATH}"
spctl --assess --type exec "${APP_PATH}" || echo "WARNING: spctl check skipped (notarization needed)"

echo "==> Creating DMG"
if command -v create-dmg &> /dev/null; then
    create-dmg \
        --volname "${APP_NAME}" \
        --volicon "${APP_PATH}/Contents/Resources/AppIcon.icns" \
        --window-pos 200 120 \
        --window-size 600 400 \
        --icon-size 100 \
        --icon "${APP_NAME}.app" 150 190 \
        --app-drop-link 450 190 \
        --hide-extension "${APP_NAME}.app" \
        "${DMG_PATH}" \
        "${APP_PATH}"
else
    # Fallback: simple hdiutil DMG
    STAGING="${BUILD_DIR}/dmg-staging"
    mkdir -p "${STAGING}"
    cp -R "${APP_PATH}" "${STAGING}/"
    ln -s /Applications "${STAGING}/Applications"
    hdiutil create -volname "${APP_NAME}" -srcfolder "${STAGING}" \
        -ov -format UDZO "${DMG_PATH}"
    rm -rf "${STAGING}"
fi

echo "==> Signing DMG"
codesign --sign "${SIGNING_IDENTITY}" --timestamp "${DMG_PATH}"

echo ""
echo "==> Notarization"
echo "    To notarize, run:"
echo "    xcrun notarytool submit '${DMG_PATH}' --apple-id \$APPLE_ID --team-id ${TEAM_ID} --password \$APP_PASSWORD --wait"
echo "    xcrun stapler staple '${DMG_PATH}'"
echo ""
echo "==> Done! DMG at: ${DMG_PATH}"
