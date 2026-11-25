#!/usr/bin/env bash
# Draws Settings' preview pictures from preview.uix with Roundhouse, as VentureOS itself draws, so they follow the look:
# Style's Auto, Light, and Dark, and Scroll bars' Classic and Automatic. Run it from anywhere after a change to the look,
# and commit the pictures it writes to App/Resources. Needs the .NET SDK and ImageMagick.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../../.." && pwd)"
resources="$here/../App/Resources"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

dotnet build "$root/src/Venture.Roundhouse" -v quiet -nologo > /dev/null
roundhouse="$root/src/Venture.Roundhouse/bin/Debug/net10.0/Roundhouse"

render() { "$roundhouse" --render "$here/preview.uix" "$work/$1.png" "${@:2}"; }
render light
render dark --dark
render classic
render automatic --scrollbars automatic

# Auto is light on the left and dark on the right, as the day turns.
width=$(magick identify -format '%w' "$work/light.png")
magick "$work/light.png" \( "$work/dark.png" -crop "50%x100%+$((width / 2))+0" +repage \) \
  -geometry "+$((width / 2))+0" -composite "$work/auto.png"

# Each picture fills a grid cell, 112 by 72, cut from the middle.
thumbnail() { magick "$work/$1.png" -resize '112x72^' -gravity center -extent 112x72 -strip "PNG24:$resources/$2.png"; }
thumbnail auto StyleAuto
thumbnail light StyleLight
thumbnail dark StyleDark
thumbnail classic ScrollClassic
thumbnail automatic ScrollAutomatic
echo "Wrote StyleAuto, StyleLight, StyleDark, ScrollClassic, and ScrollAutomatic to $resources."
