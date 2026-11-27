#!/usr/bin/env python3
"""Smoke test de `yotsuba mcp`: arranca el servidor por stdio y comprueba el protocolo y el contenido.

Uso: python3 scripts/mcp_smoke.py <comando> [args...]      p. ej.  python3 scripts/mcp_smoke.py ./yotsuba mcp
"""
import json
import subprocess
import sys

TOOLS = {"search_docs", "list_docs", "get_doc", "list_diagnostics", "get_diagnostic",
         "list_examples", "get_example", "search_examples"}
STYLES = ("hlsl", "glsl", "msl", "vulkan", "wgsl")


def main() -> int:
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    proc = subprocess.Popen(sys.argv[1:], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    next_id = 0

    def send(method, params=None, notify=False):
        nonlocal next_id
        message = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            message["params"] = params
        if not notify:
            next_id += 1
            message["id"] = next_id
        proc.stdin.write(json.dumps(message) + "\n")
        proc.stdin.flush()
        if notify:
            return None
        line = proc.stdout.readline()
        if not line:
            raise SystemExit(f"El servidor MCP terminó sin responder a {method}:\n{proc.stderr.read()}")
        reply = json.loads(line)
        if "error" in reply:
            raise SystemExit(f"{method}: {reply['error']}")
        return reply["result"]

    def call(name, arguments):
        return send("tools/call", {"name": name, "arguments": arguments})

    def check(condition, text):
        if not condition:
            raise SystemExit(f"MCP smoke: {text}")
        print(f"ok  {text}")

    try:
        init = send("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                                   "clientInfo": {"name": "smoke", "version": "1"}})
        send("notifications/initialized", notify=True)
        check(init["serverInfo"]["name"] == "yotsubashader", "initialize: servidor yotsubashader")

        tools = {t["name"] for t in send("tools/list")["tools"]}
        check(tools == TOOLS, f"tools/list: {len(tools)} herramientas")

        for style in STYLES:
            examples = json.loads(call("list_examples", {"style": style})["content"][0]["text"])
            check(len(examples) == 20, f"list_examples({style}): 20 ejemplos")

        code = call("get_example", {"id": "glsl-aurora"})["content"][0]["text"]
        check("```csharp" in code and "class" in code, "get_example devuelve el código C#")

        diag = call("get_diagnostic", {"code": "YS0508"})["content"][0]["text"]
        check("YS0508" in diag, "get_diagnostic(YS0508)")

        hits = json.loads(call("search_docs", {"query": "uniform", "lang": "es"})["content"][0]["text"])
        check(len(hits) > 0, "search_docs(uniform, es)")

        missing = call("get_doc", {"slug": "no-existe"})
        check(missing.get("isError") is True, "get_doc de una página inexistente es un error")

        resources = send("resources/list")["resources"]
        check(len(resources) >= 100, f"resources/list: {len(resources)} recursos")

        prompts = {p["name"] for p in send("prompts/list")["prompts"]}
        check("write_shader" in prompts, "prompts/list: write_shader")
    finally:
        proc.stdin.close()
        try:
            proc.wait(timeout=10)
        except subprocess.TimeoutExpired:
            proc.kill()
    return 0


if __name__ == "__main__":
    sys.exit(main())
