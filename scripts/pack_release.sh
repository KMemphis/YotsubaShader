#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: scripts/pack_release.sh <version> <output-directory>}"
output="${2:?usage: scripts/pack_release.sh <version> <output-directory>}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"

# Fork-only package IDs change NuGet's project-reference entries in packages.lock.json.
# Preserve the checkout's lock files so later CI builds still use ShadowDusk's normal IDs.
lock_backup="$(mktemp -d)"
python3 - "$lock_backup" <<'PY'
from pathlib import Path
from shutil import copy2
import sys
backup = Path(sys.argv[1])
for source in Path('ShadowDusk').rglob('packages.lock.json'):
    target = backup / source
    target.parent.mkdir(parents=True, exist_ok=True)
    copy2(source, target)
PY
restore_shadow_locks() {
  python3 - "$lock_backup" <<'PY'
from pathlib import Path
from shutil import copy2, rmtree
import sys
backup = Path(sys.argv[1])
for source in backup.rglob('packages.lock.json'):
    copy2(source, source.relative_to(backup))
rmtree(backup)
PY
}
trap restore_shadow_locks EXIT

for name in ShadowDusk.Core ShadowDusk.HLSL ShadowDusk.GLSL ShadowDusk.Compiler; do
  dotnet pack "ShadowDusk/src/$name/$name.csproj" \
    -c Release -p:Version="$version" -p:UseYotsubaPackageIds=true \
    -p:RestoreLockedMode=false -o "$output"
done
restore_shadow_locks
trap - EXIT

for name in \
  YotsubaShader.Concepts YotsubaShader.Language YotsubaShader.IR \
  YotsubaShader.Roslyn YotsubaShader.Compiler.SpirV \
  YotsubaShader.GlslSyntax YotsubaShader.HlslSyntax YotsubaShader.MslSyntax \
  YotsubaShader.SpirvSyntax YotsubaShader.WgslSyntax \
  YotsubaShader.Compiler.Library YotsubaShader.IDE.Analyzer \
  YotsubaShader.Compiler.CLI; do
  dotnet restore "$name/$name.csproj" -p:Version="$version" \
    -p:UseForkShadowDuskPackages=true \
    --source "$output" --source https://api.nuget.org/v3/index.json
  dotnet pack "$name/$name.csproj" -c Release --no-restore \
    -p:Version="$version" -p:UseForkShadowDuskPackages=true -o "$output"
done

for name in YotsubaShaderCross.Runtime YotsubaShaderCross; do
  dotnet pack "YotsubaShaderCross/src/$name/$name.csproj" \
    -c Release -p:Version="$version" -o "$output"
done
