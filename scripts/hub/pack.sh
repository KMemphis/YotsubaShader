#!/usr/bin/env bash
# Publishes the Hub for one runtime, adds the bundled content, checks it, and packs it with Velopack.
#
#   scripts/hub/pack.sh <version> <rid> <content-directory> <output-directory>
#
# The Velopack channel is the rid itself (osx-arm64, win-x64...) so that the packages of different architectures in one
# release do not overwrite each other; Updates/AppUpdater.cs asks for the same channel.
#
# <content-directory> comes from scripts/hub/build_content.sh. `vpk` (same version as the Velopack package) must be
# on PATH. Run it on the operating system of <rid>: Velopack builds the installer of the OS it runs on.
set -euo pipefail

version="${1:?usage: scripts/hub/pack.sh <version> <rid> <content-directory> <output-directory>}"
rid="${2:?missing rid}"
content="${3:?missing content directory}"
output="${4:?missing output directory}"
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
content="$(cd "$content" && pwd)"
mkdir -p "$output"
output="$(cd "$output" && pwd)"

exe="YotsubaShaderHub"
[[ "$rid" == win-* ]] && exe="YotsubaShaderHub.exe"

publish="$repo/publish/hub-$rid"
rm -rf "$publish"
dotnet publish "$repo/Apps/YotsubaShader.Hub/YotsubaShader.Hub.csproj" \
  -c Release -r "$rid" --self-contained -p:Version="$version" -o "$publish"
mkdir -p "$publish/content"
tar -C "$content" -cf - . | tar -C "$publish/content" -xf -

echo "== Self-test of the publish folder"
result="$publish/selftest.txt"
rm -f "$result"
set +e
"$publish/$exe" --selftest "$result"
code=$?
set -e
# A Windows GUI executable does not print to this console: wait for its result file instead.
for _ in $(seq 1 60); do [[ -f "$result" ]] && break; sleep 1; done
cat "$result" 2>/dev/null || { echo "The self-test wrote no result."; exit 1; }
grep -q '^RESULT ok' "$result" || { echo "The self-test failed (exit code $code)."; exit 1; }
rm -f "$result"

echo "== Velopack"
# Previous packages let vpk build delta updates. The first release has none, so failing here is expected and fine.
if [[ -n "${GH_TOKEN:-}" ]]; then
  vpk download github --repoUrl "https://github.com/KMemphis/YotsubaShader" --token "$GH_TOKEN" --channel "$rid" --outputDir "$output" \
    || echo "No earlier Hub package to build deltas from."
fi
case "$rid" in
  win-*) icon="$repo/Apps/YotsubaShader.Hub/Assets/icon/icon.ico" ;;
  osx-*) icon="$repo/Apps/YotsubaShader.Hub/Assets/icon/icon.icns" ;;
  *)     icon="$repo/Apps/YotsubaShader.Hub/Assets/icon/icon.png" ;;
esac

vpk pack \
  --icon "$icon" \
  --packId YotsubaShader.Hub \
  --packVersion "$version" \
  --packDir "$publish" \
  --mainExe "$exe" \
  --packTitle "Yotsuba Shader Hub" \
  --packAuthors "YotsubaShader contributors" \
  --runtime "$rid" \
  --channel "$rid" \
  --outputDir "$output"
# The Hub's "Download" button looks in the newest release for Yotsuba-Shader-Hub-<rid>.<ext> (Updates/InstallerDownload.cs),
# so the installer is also published under that fixed name (one per system and architecture: 6 in all).
for f in "$output"/*-"$rid"-Setup.*; do
  [[ -f "$f" ]] || continue
  cp "$f" "$output/Yotsuba-Shader-Hub-$rid.${f##*.}"
done
ls -lh "$output"
