#!/usr/bin/env python3
"""Checks, for every graphics API version in `Target`, that what the analyzer promises matches what real tools say.

The analyzer's per-API profiles (Core/Analyzers/YotsubaShader.IDE.Analyzer/GraphicsApi) state which features each
version has or lacks. This script compiles one tiny shader per feature (scripts/conformance/ApiConformance) with the
CLI at that API's language version, runs the real validator for that language, and compares:

  * a feature the profile says is guaranteed must compile and validate;
  * a feature the profile says is unavailable must not.

Mismatch rules:
  * works-expected but fails            -> error (a real incompatibility or a broken backend).
  * fails-expected but passes           -> error where the validator is strict, warning where the language has
                                           extension fallbacks (desktop GLSL), where the transpiler lowers the feature (Metal's
                                           `double`) or no real validator is available
                                           (the CLI succeeding is then the only evidence).

Usage:
  YOTSUBA_API_MATRIX=matrix.json dotnet test Core/Analyzers/YotsubaShader.IDE.Analyzer.Tests --filter Export_the_matrix
  python3 scripts/api_conformance.py <cli-executable> matrix.json [--only Vulkan,OpenGL] [--keep DIR]
"""
import argparse
import json
import os
import pathlib
import shutil
import subprocess
import sys
import tempfile

ROOT = pathlib.Path(__file__).resolve().parent.parent
PROJECT = ROOT / "scripts/conformance/ApiConformance/ApiConformance.csproj"

# Witness class -> feature it needs (None = must work everywhere).
WITNESSES = {"Baseline": None, "Float64": "Float64", "Compute": "Compute", "StorageBuffer": "StorageBuffer"}
ENTRY = {"Vertex": "VS", "Fragment": "FS", "Compute": "CS"}
EXTENSION = {"spirv": ".spv", "glsl": ".glsl", "gles": ".gles", "wgsl": ".wgsl", "hlsl": ".hlsl", "msl": ".msl"}

VULKAN_ENV = {"Vulkan_10": "vulkan1.0", "Vulkan_11": "vulkan1.1", "Vulkan_12": "vulkan1.2",
              "Vulkan_13": "vulkan1.3", "Vulkan_14": "vulkan1.4"}
METAL_STD = {"Metal_1": "macos-metal1.1", "Metal_2": "macos-metal2.0", "Metal_3": "metal3.0", "Metal_4": "metal4.0"}


def family(target: str) -> str:
    for prefix in ("Vulkan", "OpenGLES", "OpenGL", "DirectX", "Metal", "WebGpu"):
        if target.startswith(prefix):
            return prefix
    return target


class Result:
    def __init__(self, ok: bool, detail: str, strict: bool):
        self.ok, self.detail, self.strict = ok, detail, strict


def run(args, **kwargs):
    return subprocess.run(args, capture_output=True, text=True, timeout=300, **kwargs)


def stage_of(path: pathlib.Path) -> str:
    for stage in ("Vertex", "Fragment", "Compute"):
        if f".{stage}." in path.name:
            return stage
    return "Fragment"


def validate(entry: dict, path: pathlib.Path) -> Result:
    """Validate one generated file with the real tool for its language. strict=False means a pass proves little."""
    kind = entry["output"]
    if kind == "spirv":
        done = run(["spirv-val", "--target-env", VULKAN_ENV[entry["target"]], str(path)])
        return Result(done.returncode == 0, (done.stderr or done.stdout).strip().splitlines()[:1].__str__(), True)

    if kind in ("glsl", "gles"):
        flag = {"Vertex": "vert", "Fragment": "frag", "Compute": "comp"}[stage_of(path)]
        done = run(["glslangValidator", "-S", flag, str(path)])
        # Desktop GLSL accepts `#extension ... : enable` for things the core version lacks, so a pass proves little.
        return Result(done.returncode == 0, done.stdout.strip().splitlines()[1:2].__str__(), kind == "gles")

    if kind == "wgsl":
        # Capabilities 0: no f64, no push constants... what WebGPU allows. Naga's default would accept f64.
        done = run(["naga", "--capabilities", "0", str(path)])
        return Result(done.returncode == 0, (done.stderr or done.stdout).strip().splitlines()[:1].__str__(), True)

    if kind == "hlsl":
        major = int((entry["version"] or "6.0").split(".")[0])
        if major >= 6 and shutil.which("dxc"):
            stage = stage_of(path)
            profile = {"Vertex": "vs", "Fragment": "ps", "Compute": "cs"}[stage] + "_" + entry["version"].replace(".", "_")
            done = run(["dxc", "-T", profile, "-E", ENTRY[stage], "-Fo", os.devnull, str(path)])
            return Result(done.returncode == 0, (done.stderr or done.stdout).strip().splitlines()[:1].__str__(), True)
        return Result(True, "sin validador para Shader Model < 6", False)

    if kind == "msl":
        if metal_available():
            done = run(["xcrun", "-sdk", "macosx", "metal", f"-std={METAL_STD[entry['target']]}", "-c", str(path), "-o", os.devnull])
            # Not strict: the Metal compiler accepts what the transpiler lowers (SPIRV-Cross turns `double` into
            # `float`), so a pass does not prove that the language has the feature.
            return Result(done.returncode == 0, done.stderr.strip().splitlines()[:1].__str__(), False)
        return Result(True, "sin Metal Toolchain", False)

    return Result(True, "sin validador", False)


_metal = None


def metal_available() -> bool:
    global _metal
    if _metal is None:
        _metal = False
        if sys.platform == "darwin" and shutil.which("xcrun"):
            with tempfile.TemporaryDirectory() as scratch:
                probe = pathlib.Path(scratch) / "probe.metal"
                probe.write_text("#include <metal_stdlib>\nkernel void k() {}\n")
                _metal = run(["xcrun", "-sdk", "macosx", "metal", "-c", str(probe), "-o", os.devnull]).returncode == 0
    return _metal


def compile_all(cli: str, entry: dict, scratch: pathlib.Path) -> dict:
    """Compile every witness for this API. Returns {class name: Result}; a class the CLI did not generate fails."""
    target = entry["output"] + (":" + entry["version"] if entry["version"] else "")
    output = scratch / "out"
    shutil.rmtree(output, ignore_errors=True)
    compiled = run([cli, "compile", str(PROJECT), "--target", target, "--output", str(output)], cwd=scratch)
    cli_output = " | ".join((compiled.stdout + compiled.stderr).strip().splitlines()[-3:])

    results = {}
    for name in WITNESSES:
        files = sorted(p for p in output.rglob(name + ".*") if p.suffix == EXTENSION[entry["output"]] and p.stat().st_size)
        if not files:
            results[name] = Result(False, "el CLI no generó el archivo" + (f" ({cli_output})" if cli_output else ""), True)
            continue
        checked = [validate(entry, path) for path in files]
        failed = next((r for r in checked if not r.ok), None)
        strict = all(r.strict for r in checked)
        results[name] = failed if failed else Result(True, "", strict)
    return results


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("cli", type=pathlib.Path)
    parser.add_argument("matrix", type=pathlib.Path)
    parser.add_argument("--only", help="familias separadas por comas: Vulkan,OpenGL,OpenGLES,DirectX,Metal,WebGpu")
    arguments = parser.parse_args()

    entries = json.loads(arguments.matrix.read_text())
    only = {name.strip() for name in arguments.only.split(",")} if arguments.only else None
    cache = {}
    errors, warnings, checks = [], [], 0
    github = os.environ.get("GITHUB_ACTIONS") == "true"

    with tempfile.TemporaryDirectory(prefix="yotsuba-api-conformance-") as scratch_name:
        scratch = pathlib.Path(scratch_name)
        for entry in entries:
            if only and family(entry["target"]) not in only:
                continue
            if entry["output"] is None:
                print(f"{entry['name']}: ningún backend lo genera (el analizador da YS0712)")
                continue

            key = (entry["target"] if entry["output"] in ("spirv", "msl") else "", entry["output"], entry["version"])
            if key not in cache:
                cache[key] = compile_all(str(arguments.cli.resolve()), entry, scratch)
            results = cache[key]

            line = []
            for name, feature in WITNESSES.items():
                if feature is None or feature in entry["guaranteed"]:
                    expected = True
                elif feature in entry["unavailable"]:
                    expected = False
                else:
                    line.append(f"{name}=-")
                    continue

                result = results[name]
                checks += 1
                line.append(f"{name}={'ok' if result.ok else 'falla'}")
                if expected and not result.ok:
                    errors.append(f"{entry['name']}: {name} debería funcionar y falla: {result.detail}")
                elif not expected and result.ok:
                    message = f"{entry['name']}: {name} debería fallar y valida ({result.detail or 'sin diagnóstico'})"
                    (errors if result.strict else warnings).append(message)
            print(f"{entry['name']:<16} {entry['output']}:{entry['version'] or '-':<5} " + "  ".join(line))

    for message in warnings:
        print(f"::warning::{message}" if github else f"AVISO {message}")
    for message in errors:
        print(f"::error::{message}" if github else f"ERROR {message}")
    print(f"\n{checks} comprobaciones, {len(errors)} errores, {len(warnings)} avisos")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
