#!/usr/bin/env bash
set -euo pipefail

version="${1:?usage: scripts/pack_release.sh <version> <output-directory>}"
output="${2:?usage: scripts/pack_release.sh <version> <output-directory>}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"

for name in ShadowDusk.Core ShadowDusk.HLSL ShadowDusk.GLSL ShadowDusk.Compiler; do
  dotnet pack "ShadowDusk/src/$name/$name.csproj" \
    -c Release -p:Version="$version" -p:UseYotsubaPackageIds=true \
    -p:RestoreLockedMode=false -o "$output"
done

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
