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
for source in Path('ThirdParty/ShadowDusk').rglob('packages.lock.json'):
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
  dotnet pack "ThirdParty/ShadowDusk/src/$name/$name.csproj" \
    -c Release -p:Version="$version" -p:UseYotsubaPackageIds=true \
    -p:RestoreLockedMode=false -o "$output"
done
restore_shadow_locks
trap - EXIT

# Every YotsubaShader.Extensions* project (the contract and each CLI plugin such as
# .MonoGame, .KNI and .FNA) is packed and published; new plugins are picked up by name.
extension_projects=()
for directory in Plugins/YotsubaShader.Extensions Plugins/YotsubaShader.Extensions.*/; do
  directory="${directory%/}"
  name="${directory##*/}"
  [[ -f "$directory/$name.csproj" ]] && extension_projects+=("$directory")
done

# Each entry is the project's folder; the .csproj inside has the folder's name.
for directory in \
  Core/Syntax/YotsubaShader.Concepts Core/Representations/YotsubaShader.Language Core/Representations/YotsubaShader.IR \
  Core/Analyzers/YotsubaShader.Roslyn Core/Compilers/YotsubaShader.Compiler.SpirV \
  Core/Syntax/YotsubaShader.GlslSyntax Core/Syntax/YotsubaShader.HlslSyntax Core/Syntax/YotsubaShader.MslSyntax \
  Core/Syntax/YotsubaShader.SpirvSyntax Core/Syntax/YotsubaShader.WgslSyntax \
  Core/YotsubaShader.Compiler.Library Core/Analyzers/YotsubaShader.IDE.Analyzer \
  Core/YotsubaShader.Compiler.CLI "${extension_projects[@]}"; do
  name="${directory##*/}"
  dotnet restore "$directory/$name.csproj" -p:Version="$version" \
    -p:UseForkShadowDuskPackages=true \
    --source "$output" --source https://api.nuget.org/v3/index.json
  dotnet pack "$directory/$name.csproj" -c Release --no-restore \
    -p:Version="$version" -p:UseForkShadowDuskPackages=true -o "$output"
done

for name in YotsubaShaderCross.Runtime YotsubaShaderCross; do
  dotnet pack "Core/ShaderCross/YotsubaShaderCross/src/$name/$name.csproj" \
    -c Release -p:Version="$version" -o "$output"
done
