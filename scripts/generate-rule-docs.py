#!/usr/bin/env python3
"""Genera docs/rules/YSxxxx.md (una página por diagnóstico) a partir del catálogo
Core/Analyzers/YotsubaShader.Rules/ShaderRules.cs. Los textos del catálogo están en inglés; la página los muestra en
español (tabla Localization/Loc.Es.cs) y añade el original en inglés. Ejecutar desde la raíz del repo."""
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CATALOG = os.path.join(ROOT, "Core/Analyzers/YotsubaShader.Rules/ShaderRules.cs")
TRANSLATIONS = os.path.join(ROOT, "Core/Analyzers/YotsubaShader.Rules/Localization/Loc.Es.cs")
OUT = os.path.join(ROOT, "docs/rules")

AREAS = {
    "00": ("Tipos de GPU", "Cómo se declaran vectores y qué tipos existen en la GPU."),
    "01": ("Puntos de entrada", "Métodos con [ShaderStage]: forma, etapa y tamaño de grupo de compute."),
    "02": ("Interfaz entre etapas", "Entradas y salidas de cada etapa: built-ins, locations y semánticas."),
    "03": ("Recursos", "Texturas, samplers y buffers: bindings y layout de memoria."),
    "04": ("Métodos", "Funciones auxiliares que se compilan a la GPU."),
    "05": ("C# sin traducción", "Construcciones de C# que no existen en la GPU."),
    "06": ("Uso por etapa", "Operaciones que solo existen en una etapa del pipeline."),
    "07": ("Portabilidad", "Funciona en unas APIs y no en otras; se valida contra las APIs de [ShaderTargets] (Vulkan 1.3 si el shader no declara ninguna)."),
}

source = open(CATALOG, encoding="utf-8").read()
pattern = re.compile(
    r'public static readonly DiagnosticDescriptor (\w+) = (Error|Warning|Portability)\("(YS\d{4})", "([^"]+)",\s*"((?:[^"\\]|\\.)*)"',
    re.S)

spanish = dict(re.findall(r'\["((?:[^"\\]|\\.)*)"\]\s*=\s*"((?:[^"\\]|\\.)*)"', open(TRANSLATIONS, encoding="utf-8").read()))

os.makedirs(OUT, exist_ok=True)
rules = []
for name, kind, rule_id, title_en, message_en in pattern.findall(source):
    title = spanish.get(title_en, title_en)
    message = spanish.get(message_en, message_en)
    severity = "Error" if kind == "Error" else "Warning"
    area, area_text = AREAS.get(rule_id[2:4], ("General", ""))
    readable = re.sub(r"\{(\d)\}", lambda m: "`…`", message)
    readable_en = re.sub(r"\{(\d)\}", lambda m: "`…`", message_en)
    rules.append((rule_id, title, severity, area))
    with open(os.path.join(OUT, rule_id + ".md"), "w", encoding="utf-8") as page:
        page.write(f"# {rule_id}: {title}\n\n")
        page.write(f"| | |\n|---|---|\n| Severidad | {severity} |\n| Área | {area} |\n")
        page.write(f"| Categoría | {'YotsubaShader.Portability' if kind == 'Portability' else 'YotsubaShader'} |\n\n")
        page.write(f"## Mensaje\n\n> {readable}\n\nEn inglés (idioma base del analizador): *{title_en}*: {readable_en}\n\n")
        page.write(f"## Contexto\n\n{area_text}\n\n")
        if severity == "Error":
            page.write("Un error YS significa que el shader no compilaría para la GPU: el compilador de YotsubaShader lo "
                       "rechaza con el mismo código, así que lo que el IDE acepta compila.\n\n")
        else:
            page.write("Es una alerta: el shader compila, pero puede fallar o comportarse distinto en alguna API o GPU. "
                       "Se puede ajustar con `.editorconfig` (`dotnet_diagnostic." + rule_id + ".severity`) y, para las "
                       "alertas de portabilidad, eligiendo las APIs con `[ShaderTargets(...)]` en la clase o el ensamblado. Si el shader "
                       "declara una API que no tiene lo que usa, la alerta pasa a ser un error.\n\n")
        page.write("## Cómo se arregla\n\nEl propio mensaje indica la corrección. Las reglas se implementan en "
                   "`Core/Analyzers/YotsubaShader.Rules` (descriptor `ShaderRules." + name + "`).\n")

with open(os.path.join(OUT, "README.md"), "w", encoding="utf-8") as index:
    index.write("# Diagnósticos de YotsubaShader\n\n")
    index.write("Generado por `scripts/generate-rule-docs.py` desde `ShaderRules.cs`.\n\n")
    index.write("| ID | Título | Severidad | Área |\n|---|---|---|---|\n")
    for rule_id, title, severity, area in sorted(rules):
        index.write(f"| [{rule_id}]({rule_id}.md) | {title} | {severity} | {area} |\n")

print(f"{len(rules)} reglas documentadas en {OUT}")
