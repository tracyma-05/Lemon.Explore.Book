#!/usr/bin/env bash
# Builds one desktop package of the book with native AOT, named the way the updater recognises it:
#
#   artifacts/explore-book-<version>-win-x64.zip     the app folder (Windows; run it in Git Bash)
#   artifacts/explore-book-<version>-osx-arm64.zip   "Explore Book.app", ad-hoc signed, zipped with ditto (macOS)
#   artifacts/explore-book-<version>-osx-x64.zip
#
# Usage: scripts/package-desktop.sh <rid> [update-feed]
#   rid          win-x64, win-arm64, osx-arm64 or osx-x64
#   update-feed  built into the package as its update address (see docs/UPDATES.md), e.g. github:owner/repo, or off.
#                Empty: this repository's GitHub Releases (the Release build's default).
#
# Windows needs the Visual Studio C++ build tools (the native linker); macOS needs the Xcode command line tools.
set -euo pipefail

cd "$(dirname "$0")/.."

RID="${1:?usage: scripts/package-desktop.sh <rid> [update-feed]}"
FEED="${2:-}"
VERSION="$(sed -nE 's|.*<Version>([^<]+)</Version>.*|\1|p' Directory.Build.props | head -1)"
NAME="explore-book-$VERSION-$RID"
OUT="publish/$RID"
mkdir -p artifacts
rm -rf "$OUT"

echo "==> $NAME (update feed: ${FEED:-the default})"
# An empty -p:UpdateFeed= would override the project's default, so only pass one that was given.
FEED_ARG=()
if [[ -n "$FEED" ]]; then FEED_ARG=("-p:UpdateFeed=$FEED"); fi
dotnet publish src/Lemon.Explore.Book.Desktop -c Release -r "$RID" \
  -p:PublishAot=true -p:DebugType=none ${FEED_ARG[@]+"${FEED_ARG[@]}"} -warnaserror \
  -o "$OUT/app"

# Debug symbols (the app's and the native libraries' .pdb / .dSYM) are not shipped: most of the size, no use to readers.
find "$OUT/app" \( -name "*.pdb" -o -name "*.dSYM" -o -name "*.dbg" \) -prune -exec rm -rf {} +

ZIP="artifacts/$NAME.zip"
rm -f "$ZIP"

case "$RID" in
  win-*)
    # The folder's contents at the zip's root: the updater finds ExploreBook.exe there.
    powershell -NoProfile -Command "Compress-Archive -Path '$OUT/app/*' -DestinationPath '$ZIP' -Force"
    ;;
  osx-*)
    APP="$OUT/Explore Book.app"
    mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
    cp -R "$OUT/app/." "$APP/Contents/MacOS/"
    # Narration clips are data, not code: Resources is where the signature seals them (the app looks there too).
    if [ -d "$APP/Contents/MacOS/narration" ]; then
      mv "$APP/Contents/MacOS/narration" "$APP/Contents/Resources/"
    fi
    sed "s/__VERSION__/$VERSION/g" src/Lemon.Explore.Book.Desktop/macOS/Info.plist > "$APP/Contents/Info.plist"
    chmod +x "$APP/Contents/MacOS/ExploreBook"

    # The Dock icon, from the book's PNG.
    ICONSET="$OUT/book.iconset"
    mkdir -p "$ICONSET"
    for size in 16 32 128 256 512; do
      sips -z "$size" "$size" src/Lemon.Explore.Book/Assets/book.png --out "$ICONSET/icon_${size}x${size}.png" > /dev/null
      double=$((size * 2))
      if [ "$double" -le 512 ]; then
        sips -z "$double" "$double" src/Lemon.Explore.Book/Assets/book.png --out "$ICONSET/icon_${size}x${size}@2x.png" > /dev/null
      fi
    done
    iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/book.icns"
    rm -rf "$ICONSET"

    # Ad-hoc signature: Apple silicon will not start an unsigned executable. Without a Developer ID the first start
    # needs right-click → Open; updates the app downloads itself are not quarantined and open normally.
    codesign --force --deep --sign - "$APP"

    # ditto keeps the bundle's links, permissions and signature; the updater unpacks it with ditto too.
    ditto -c -k --keepParent "$APP" "$ZIP"
    ;;
  *)
    echo "Unsupported runtime: $RID" >&2
    exit 1
    ;;
esac

echo "   -> $ZIP"
