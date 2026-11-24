#!/usr/bin/env python3
"""Stage the plugins of this checkout where the CLI looks for installed plugins.

    python scripts/stage_plugins.py <yotsuba-home> fna monogame kni

Each plugin is published from source (managed dependencies, ShadowDusk and every native under runtimes/<rid>/native)
into <yotsuba-home>/plugins/<name>/ with a plugin.json whose version is the CLI's own. The CLI then finds the plugin
installed at its own version and never goes to the network, so CI can check the plugins of a commit before they exist
on NuGet. Set YOTSUBA_HOME=<yotsuba-home> when running the CLI.
"""
import json
import pathlib
import re
import shutil
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
PLUGINS = {
    "monogame": "MonoGame",
    "kni": "KNI",
    "fna": "FNA",
    "godot": "Godot",
}
# The CLI already carries these, and the plugin must share its copy (see PluginManager).
SHARED = ("YotsubaShader.Extensions.dll", "YotsubaShader.IR.dll")


def version() -> str:
    props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    match = re.search(r"<Version>([^<]+)</Version>", props)
    if not match:
        raise SystemExit("Directory.Build.props has no <Version>")
    return match.group(1)


def stage(home: pathlib.Path, name: str) -> None:
    project = f"YotsubaShader.Extensions.{PLUGINS[name]}"
    target = home / "plugins" / name
    if target.exists():
        shutil.rmtree(target)
    subprocess.run(
        ["dotnet", "publish", str(ROOT / "Plugins" / project / f"{project}.csproj"),
         "-c", "Release", "-o", str(target), "-v", "q", "-nologo"],
        check=True)
    for item in target.iterdir():
        if item.name in SHARED or item.name.startswith("YotsubaShaderCross"):
            item.unlink()
    (target / "plugin.json").write_text(json.dumps({
        "Name": name,
        "Package": project,
        "Version": version(),
        "Assembly": f"{project}.dll",
    }, indent=2), encoding="utf-8")
    print(f"staged {name} {version()} -> {target}")


def main() -> None:
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    home = pathlib.Path(sys.argv[1]).resolve()
    for name in sys.argv[2:]:
        if name not in PLUGINS:
            raise SystemExit(f"unknown plugin '{name}'. Known: {', '.join(PLUGINS)}")
        stage(home, name)


if __name__ == "__main__":
    main()
