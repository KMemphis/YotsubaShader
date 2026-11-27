#!/usr/bin/env bash
# Builds the platform-independent content that every Hub package carries:
#   <out>/ui       the Hub interface
#   <out>/learn    the offline documentation (learn.yotsubashader.com)
#   <out>/site     the website with the playground and its WebAssembly backend
#   <out>/LICENSE
#
# Needs Node 22 and, for the site, the .NET SDKs the playground builds with. The Web repository must already be
# checked out in ./Web. To reuse an existing build instead of rebuilding, set HUB_LEARN_DIST and/or HUB_SITE_DIST.
#
# The narration audio of learn (hundreds of MB) is not bundled unless HUB_INCLUDE_AUDIO=1; the Hub serves missing
# audio from the public site when it is online.
set -euo pipefail

out="${1:?usage: scripts/hub/build_content.sh <output-directory>}"
repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
rm -rf "$out"
mkdir -p "$out/ui" "$out/learn" "$out/site"
out="$(cd "$out" && pwd)"

# Copies a directory's contents, optionally leaving one top-level folder out.
copy_tree() { # <from> <to> [excluded-top-level-name]
  if [[ -n "${3:-}" ]]; then
    tar -C "$1" --exclude="./$3" -cf - . | tar -C "$2" -xf -
  else
    tar -C "$1" -cf - . | tar -C "$2" -xf -
  fi
}

echo "== Hub UI"
(cd "$repo/Apps/YotsubaShader.Hub/ui" && npm ci --no-audit --no-fund && npm run build)
copy_tree "$repo/Apps/YotsubaShader.Hub/ui/dist" "$out/ui"

echo "== learn"
if [[ -n "${HUB_LEARN_DIST:-}" ]]; then
  learn_dist="$HUB_LEARN_DIST"
else
  (cd "$repo/Web/learn" && node tools/gen-mcp-data.mjs && node build.mjs --strict-i18n)
  learn_dist="$repo/Web/learn/dist"
fi
if [[ "${HUB_INCLUDE_AUDIO:-0}" == 1 ]]; then copy_tree "$learn_dist" "$out/learn"; else copy_tree "$learn_dist" "$out/learn" audio; fi

echo "== site and playground"
if [[ -n "${HUB_SITE_DIST:-}" ]]; then
  site_dist="$HUB_SITE_DIST"
else
  (cd "$repo/Web" && npm run playground && node build.mjs)
  site_dist="$repo/Web/dist"
fi
copy_tree "$site_dist" "$out/site"

cp "$repo/LICENSE" "$out/LICENSE"
test -f "$out/ui/index.html"
test -f "$out/learn/en/index.html"
test -f "$out/site/playground-app/_framework/blazor.boot.json"
du -sh "$out"/*
