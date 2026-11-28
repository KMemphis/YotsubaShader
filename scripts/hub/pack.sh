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

# macOS: without a signature that covers the whole bundle, an app downloaded with a browser is reported as "damaged".
# "-" is an ad-hoc signature (no Apple account needed): it seals the bundle but Gatekeeper still asks for right-click > Open
# the first time. Set HUB_SIGN_IDENTITY to a "Developer ID Application" identity (and HUB_NOTARY_PROFILE) to remove that too.
sign=()
if [[ "$rid" == osx-* ]]; then
  sign=(--signAppIdentity "${HUB_SIGN_IDENTITY:--}")
  [[ -n "${HUB_NOTARY_PROFILE:-}" ]] && sign+=(--notaryProfile "$HUB_NOTARY_PROFILE")
fi

vpk pack \
  ${sign[@]+"${sign[@]}"} \
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
# The Hub's "Download" button looks in the newest release for Yotsuba-Shader-Hub-<rid>.<ext> (Updates/InstallerDownload.cs), so
# the installer is renamed to that fixed name (one per system and architecture: 6 in all). Velopack names it
# <packId>-<rid>-Setup.exe / .pkg on Windows and macOS, and <packId>-<rid>.AppImage on Linux. The update feeds only
# reference the .nupkg, so renaming the installer does not affect self-update.
installer=""
for f in "$output"/*-"$rid"-Setup.* "$output"/*"$rid".AppImage; do
  [[ -f "$f" ]] && { installer="$f"; break; }
done
[[ -n "$installer" ]] || { echo "No installer found for $rid in $output"; ls -l "$output"; exit 1; }
mv "$installer" "$output/Yotsuba-Shader-Hub-$rid.${installer##*.}"
ls -lh "$output"
