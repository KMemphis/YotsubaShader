#!/usr/bin/env python3
"""Checks the actual release artifacts, independently of the checkout build output."""

import argparse
import hashlib
import os
import pathlib
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile


PACKAGES = (
    "YotsubaShader.ShadowDusk.Core",
    "YotsubaShader.ShadowDusk.HLSL",
    "YotsubaShader.ShadowDusk.GLSL",
    "YotsubaShader.ShadowDusk.Compiler",
    "YotsubaShader.Concepts",
    "YotsubaShader.Language",
    "YotsubaShader.IR",
    "YotsubaShader.Roslyn",
    "YotsubaShader.Compiler.SpirV",
    "YotsubaShaderCross.Runtime",
    "YotsubaShaderCross",
    "YotsubaShader.GlslSyntax",
    "YotsubaShader.HlslSyntax",
    "YotsubaShader.MslSyntax",
    "YotsubaShader.SpirvSyntax",
    "YotsubaShader.WgslSyntax",
    "YotsubaShader.Compiler.Library",
    "YotsubaShader.IDE.Analyzer",
    "YotsubaShader.Compiler.CLI",
)

# Every YotsubaShader.Extensions* project is packed by pack_release.sh.
PACKAGES += tuple(sorted(
    path.name
    for path in pathlib.Path(__file__).resolve().parent.parent.joinpath("Plugins").glob("YotsubaShader.Extensions*")
    if (path / f"{path.name}.csproj").is_file()
))

RIDS = {
    "win-x64": ("dxcompiler.dll", "spirv-cross.dll", "libvkd3d-shader-1.dll"),
    "win-arm64": ("dxcompiler.dll", "spirv-cross.dll", "libvkd3d-shader-1.dll"),
    "linux-x64": ("libdxcompiler.so", "libspirv-cross.so", "libvkd3d-shader.so.1"),
    "linux-arm64": ("libdxcompiler.so", "libspirv-cross.so", "libvkd3d-shader.so.1"),
    "osx-x64": ("libdxcompiler.dylib", "libspirv-cross.dylib", "libvkd3d-shader.1.dylib"),
    "osx-arm64": ("libdxcompiler.dylib", "libspirv-cross.dylib", "libvkd3d-shader.1.dylib"),
}

VKD3D_SHA256 = {
    "win-x64": "9b97222601ee00ffc60f9f7bc426e13cc7df325c46725fe80a036e3eff9e2edb",
    "win-arm64": "90197d9a79c313cfecc581ca61d889cc3ba88f2b3c0f7638deb186fe74ed8262",
    "linux-x64": "bda15bc2a8b1a017a4adfed3c00c85f696cebb41f629b715ad8f58a37bc26678",
    "linux-arm64": "c548444aea512448514b23773b0e974fac3ee0f80643767d377b8d1fdc86dfed",
    "osx-x64": "0fcd4e99d7c4c9b0375535ccfd0c27cf2f72fe60fb28ca7ffc770616d58c09f6",
    "osx-arm64": "b624db5641469c34bc44849cbc42201ffd7cdd26bfc8bb6a4c8f5f6757f8d34b",
}
# MonoGame.Tool.Dxc 1.9.2607.1 binaries/linux-arm64/lib/libdxcompiler.so: the DXC ShadowDusk pins for Linux ARM64.
LINUX_ARM64_DXC_SHA256 = "b3cec73ebcada16b98707a61bacf04e450ff5b98c8978a7f7ce0f0cd9164094f"


def check_native_hash(package: zipfile.ZipFile, asset: str, expected: str) -> None:
    actual = hashlib.sha256(package.read(asset)).hexdigest()
    if actual != expected:
        raise ValueError(f"{asset}: SHA-256 {actual}, expected {expected}")


def check_packages(feed: pathlib.Path, version: str, require_six_natives: bool) -> None:
    for package_id in PACKAGES:
        path = feed / f"{package_id}.{version}.nupkg"
        if not path.is_file():
            raise ValueError(f"missing package: {path}")
        with zipfile.ZipFile(path) as package:
            files = set(package.namelist())
            nuspec = ET.fromstring(package.read(f"{package_id}.nuspec"))
            metadata = next(node for node in nuspec if node.tag.endswith("metadata"))
            children = {node.tag.rsplit("}", 1)[-1]: node for node in metadata}
            if children["version"].text != version:
                raise ValueError(f"{package_id}: wrong version")
            if not package_id.startswith("YotsubaShader.ShadowDusk") and "NOTICE" not in files:
                raise ValueError(f"{package_id}: NOTICE missing")
            if package_id == "YotsubaShader.ShadowDusk.HLSL":
                if "THIRD-PARTY-NOTICES.txt" not in files:
                    raise ValueError(f"{package_id}: THIRD-PARTY-NOTICES.txt missing")
                if require_six_natives:
                    for rid, natives in RIDS.items():
                        for native in natives:
                            if "vkd3d" not in native and not (rid == "linux-arm64" and native == "libdxcompiler.so"):
                                continue
                            asset = f"runtimes/{rid}/native/{native}"
                            if asset not in files:
                                raise ValueError(f"{package_id}: {asset} missing")
                            if "vkd3d" in native:
                                check_native_hash(package, asset, VKD3D_SHA256[rid])
                            elif rid == "linux-arm64" and native == "libdxcompiler.so":
                                check_native_hash(package, asset, LINUX_ARM64_DXC_SHA256)
            if package_id.startswith("YotsubaShaderCross"):
                for notice in ("LICENSE", "LICENSES/SPIRV-Cross-MIT.txt", "LICENSES/Khronos-FreeUse.txt"):
                    if notice not in files:
                        raise ValueError(f"{package_id}: {notice} missing")
            for dependency in metadata.iter():
                if not dependency.tag.endswith("dependency"):
                    continue
                dependency_id = dependency.attrib["id"]
                required = dependency.attrib["version"]
                if dependency_id in PACKAGES and required != version:
                    raise ValueError(f"{package_id}: {dependency_id} has version {required}")
                if dependency_id == "ShadowDusk.Compiler":
                    raise ValueError(f"{package_id}: unexpected upstream ShadowDusk.Compiler dependency")

            if package_id == "YotsubaShader.IDE.Analyzer":
                if "analyzers/dotnet/cs/YotsubaShader.IDE.Analyzer.dll" not in files:
                    raise ValueError("analyzer assembly is not in analyzers/dotnet/cs")
            if package_id == "YotsubaShader.Compiler.CLI":
                # The engine backends (ShadowDusk and its native libraries) are no longer inside the tool:
                # the MonoGame / KNI / FNA plugins pull them in as the YotsubaShader.ShadowDusk.* packages,
                # which are verified above.
                tool_root = "tools/net10.0/any/"
                for path_part in (
                    "DotnetToolSettings.xml",
                    "THIRD-PARTY-NOTICES.txt",
                    "LICENSE",
                    "YotsubaShader.Compiler.CLI.dll",
                ):
                    if tool_root + path_part not in files:
                        raise ValueError(f"tool package missing {path_part}")
                if any(name.startswith(tool_root + "ShadowDusk.") for name in files):
                    raise ValueError("tool package still bundles ShadowDusk assemblies; they belong to the plugins")
            if package_id.startswith("YotsubaShader.Extensions.") and package_id != "YotsubaShader.Extensions":
                dependencies = {node.attrib["id"] for node in metadata.iter() if node.tag.endswith("dependency")}
                # The Godot plugin translates through SPIRV-Cross to .gdshader text: it has no ShadowDusk dependency.
                if package_id != "YotsubaShader.Extensions.Godot" and "YotsubaShader.ShadowDusk.Compiler" not in dependencies:
                    raise ValueError(f"{package_id}: must depend on YotsubaShader.ShadowDusk.Compiler")
    print(f"Verified {len(PACKAGES)} packages" + (" and native assets for all six RIDs" if require_six_natives else ""))


def smoke(executable: pathlib.Path, project: pathlib.Path) -> None:
    executable = executable.resolve(strict=True)
    project = project.resolve(strict=True)
    help_result = subprocess.run(
        [str(executable), "--help"], capture_output=True, text=True, timeout=30,
        check=True)
    if "Uso:" not in help_result.stdout:
        raise ValueError("CLI help output is missing")
    targets = {
        "spirv": ".spv",
        "hlsl": ".hlsl",
        "glsl": ".glsl",
        "msl": ".msl",
    }
    # Plugin targets come from the plugins installed under YOTSUBA_HOME (scripts/stage_plugins.py stages the ones of
    # this checkout) or are fetched from NuGet, so without staging they can only be smoke-tested once the plugin
    # packages are published. YOTSUBA_SMOKE_PLUGINS is "1" for all of them or a list such as "fna,monogame,kni".
    plugin_extensions = {"monogame": ".mgfx", "kni": ".knifx", "fna": ".fxb", "godot": ".gdshader"}
    requested = os.environ.get("YOTSUBA_SMOKE_PLUGINS", "")
    if requested == "1":
        targets.update(plugin_extensions)
    elif requested:
        for name in (item.strip() for item in requested.split(",") if item.strip()):
            if name not in plugin_extensions:
                raise ValueError(f"YOTSUBA_SMOKE_PLUGINS names an unknown plugin '{name}'")
            targets[name] = plugin_extensions[name]
    with tempfile.TemporaryDirectory(prefix="yotsuba-release-smoke-") as scratch:
        output = pathlib.Path(scratch) / "output"
        args = [str(executable), "compile", str(project)]
        for target in targets:
            args.extend(("--target", target))
        args.extend(("--output", str(output)))
        subprocess.run(args, cwd=scratch, check=True, timeout=300)
        for target, extension in targets.items():
            generated = [item for item in output.rglob(f"*{extension}")
                         if item.is_file() and item.stat().st_size]
            if not generated:
                raise ValueError(f"CLI produced no nonempty {target} output files")
            print(f"{target}: compiled {len(generated)} nonempty files outside the checkout")


def main() -> None:
    parser = argparse.ArgumentParser()
    commands = parser.add_subparsers(dest="command", required=True)
    packages = commands.add_parser("packages")
    packages.add_argument("feed", type=pathlib.Path)
    packages.add_argument("version")
    packages.add_argument("--skip-native-check", action="store_true")
    run = commands.add_parser("smoke")
    run.add_argument("executable", type=pathlib.Path)
    run.add_argument("project", type=pathlib.Path)
    arguments = parser.parse_args()
    try:
        if arguments.command == "packages":
            check_packages(arguments.feed, arguments.version,
                           not arguments.skip_native_check)
        else:
            smoke(arguments.executable, arguments.project)
    except (ValueError, subprocess.CalledProcessError, subprocess.TimeoutExpired) as error:
        print(error, file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
