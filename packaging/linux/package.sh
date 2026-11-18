#!/usr/bin/env bash
# Builds Roundhouse for x86_64 Linux:
#
#   $DIST/roundhouse-<version>-linux-x86_64.AppImage  runs on most distributions, without FUSE
#   $DIST/roundhouse-<version>-linux-x86_64.tar.gz    the same program as a plain folder
#
# Usage: packaging/linux/package.sh [--framework-dependent]
#
# Needs the .NET 10 SDK. appimagetool and the AppImage runtime are downloaded at pinned versions
# into $WORK and checked against their published SHA-256s.

# shellcheck source=../env.sh
. "$(dirname "${BASH_SOURCE[0]}")/../env.sh"
parse_options "$@"
HERE="$ROOT/packaging/linux"
ARCH=x86_64
BASENAME="roundhouse-$VERSION-linux-$ARCH"
echo "==> $APP $VERSION for Linux $ARCH (self-contained: $SELF_CONTAINED)"

STAGE="$WORK/linux/$BASENAME"
publish linux-x64 "$SELF_CONTAINED" "$STAGE"
[[ -f "$STAGE/libSkiaSharp.so" ]] || fail "the published Roundhouse has no libSkiaSharp.so, which it draws with."
copy_docs "$STAGE"
smoke_test "$STAGE/$APP" linux

# ---- .tar.gz ------------------------------------------------------------------------------------
tar -C "$WORK/linux" -czf "$DIST/$BASENAME.tar.gz" "$BASENAME"
echo "wrote $DIST/$BASENAME.tar.gz"

# ---- AppImage -----------------------------------------------------------------------------------
# The desktop entry and the icon go at the AppImage's root, where appimagetool looks for them.
APPDIR="$WORK/linux/$APP.AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/share/applications" "$APPDIR/usr/share/icons/hicolor/512x512/apps"
cp -R "$STAGE" "$APPDIR/usr/bin"
cp "$HERE/$APP.desktop" "$APPDIR/usr/share/applications/$APP.desktop"
ln -s "usr/share/applications/$APP.desktop" "$APPDIR/$APP.desktop"
cp "$ROOT/src/Venture.Roundhouse/Assets/$APP.png" "$APPDIR/usr/share/icons/hicolor/512x512/apps/$APP.png"
ln -s "usr/share/icons/hicolor/512x512/apps/$APP.png" "$APPDIR/$APP.png"
ln -s "$APP.png" "$APPDIR/.DirIcon"
install -m755 "$HERE/AppRun" "$APPDIR/AppRun"
if command -v desktop-file-validate >/dev/null; then
    desktop-file-validate "$APPDIR/usr/share/applications/$APP.desktop"
fi

# The pins CityLimits and Photocraft use, from the releases' published digests:
# https://github.com/AppImage/appimagetool/releases/tag/1.9.1
# https://github.com/AppImage/type2-runtime/releases/tag/20251108
# They stay together, since appimagetool would otherwise download the latest runtime itself.
TOOL_VERSION=1.9.1
TOOL_SHA256=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0
RUNTIME_VERSION=20251108
RUNTIME_SHA256=2fca8b443c92510f1483a883f60061ad09b46b978b2631c807cd873a47ec260d
TOOL="$WORK/appimagetool-$TOOL_VERSION-$ARCH.AppImage"
RUNTIME="$WORK/type2-runtime-$RUNTIME_VERSION-$ARCH"
download_verified "https://github.com/AppImage/appimagetool/releases/download/$TOOL_VERSION/appimagetool-$ARCH.AppImage" "$TOOL" "$TOOL_SHA256" true
download_verified "https://github.com/AppImage/type2-runtime/releases/download/$RUNTIME_VERSION/runtime-$ARCH" "$RUNTIME" "$RUNTIME_SHA256"

# Extracting appimagetool instead of mounting it means it runs without FUSE, as on many immutable
# distributions and in containers; the AppImage carries the static runtime, so nobody needs libfuse2.
OUT="$DIST/$BASENAME.AppImage"
rm -f "$OUT"
ARCH="$ARCH" APPIMAGE_EXTRACT_AND_RUN=1 "$TOOL" --no-appstream --runtime-file "$RUNTIME" "$APPDIR" "$OUT"
echo "wrote $OUT"

# The AppImage itself draws a document too, run the way people run it.
APPIMAGE_EXTRACT_AND_RUN=1 smoke_test "$OUT" appimage
echo "==> done"
ls -lh "$DIST/$BASENAME".*
