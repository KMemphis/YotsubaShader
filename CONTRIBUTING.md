# Contributing to Yotsuba Shader

Thank you for your interest in Yotsuba Shader. Contributions are welcome, from a shader that does not compile to a new backend.

## Before you start

- **Small fixes** (typos, documentation, an obvious bug): open a pull request directly.
- **Design changes** (new attributes, new types, new backends, changes to the IR): open an issue first. Describe:
  1. the problem you want to solve,
  2. your proposal, and
  3. a motivating shader or backend example.

  Starting from an issue saves you from writing code that does not fit the design.

## Set up

You need the **.NET 8 SDK** (`global.json` asks for 8.0 and rolls forward to the latest minor version).

```bash
git clone https://github.com/KMemphis/YotsubaShader.git
cd YotsubaShader
dotnet build YotsubaShader.sln
dotnet test YotsubaShader.Frontend.Tests
```

## How the repository is organised

| Project | Role |
| --- | --- |
| `YotsubaShader.Language` | Authoring language: stages, attributes, resources and types. |
| `YotsubaShader.Roslyn` | Roslyn-based discovery and semantic analysis of C# shaders. |
| `YotsubaShader.IR` | Shared model between the frontend and the backends. |
| `YotsubaShader.Compiler.SpirV` | SPIR-V module construction and encoding. |
| `YotsubaShader.Generator` | SPIR-V operation table generated from the Khronos grammar. |
| `YotsubaShader.Example` | Executable example of low-level SPIR-V assembly. |
| `YotsubaShader.Frontend.Tests` | Discovery and analysis tests for the Roslyn frontend. |
| `Web` (separate repository) | The website and the in-browser playground; see [Documentation and website](#documentation-and-website). |

## Attribute changes

Yotsuba Shader has five authoring vocabularies (HLSL, GLSL, Metal, Vulkan and WGSL) that must mean the same thing. When you add or change an attribute:

- Explain its **semantic equivalence** with the language that inspires it.
- Make sure an attribute in one style means the same as its counterparts in the other four. If a style has no equivalent, say so in the pull request.
- Include **tests** where appropriate.
- Check that the names you use are valid C#. Names that clash between styles need care; for example, `[Binding]` in the Vulkan style is ambiguous with the shared `BindingAttribute` unless it is written `[@Binding]`.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep the change focused: one topic per pull request.
3. Run `dotnet build YotsubaShader.sln` and `dotnet test YotsubaShader.Frontend.Tests` and make sure they pass.
4. Describe **what** changes and **why**, and link the issue if there is one.

Write commit messages that say what the change does, in the present tense ("Add texture usage attribute").

## Documentation and website

The website lives in its own repository, [YotsubaShader-Web](https://github.com/KMemphis/YotsubaShader-Web), published at [yotsubashader.com](https://yotsubashader.com). It is a static site with no dependencies besides Node. Clone it as `Web/` inside this repository's folder: the build reads `../YotsubaShader.Language` to generate the attribute reference, and `Web/` is ignored by this repository's Git.

```bash
git clone https://github.com/KMemphis/YotsubaShader-Web.git Web
cd Web
node build.mjs --serve        # builds both languages and serves http://localhost:4173
node build.mjs --strict-i18n  # fails if a Spanish translation is missing or unused
```

Two rules for the documentation:

- **Explain with C# examples.** Show the concept in code, even when the explanation gets longer. A table of names is a complement, never the explanation.
- **Keep the examples valid.** Every example should compile. You can check it in the playground.

The site is published in English and Spanish. Every English text has its Spanish translation in `Web/src/i18n/es/`; the build tells you what is missing.

## Licensing of contributions

Yotsuba Shader is released under the [MIT License](LICENSE). By submitting a contribution, you agree to license it under the same MIT License, and you confirm that you have the right to do so.

## Using AI tools

AI assistants may be used to understand or review the code. This repository asks agents to work in read-only mode outside `Web/`; see [`AGENTS.md`](AGENTS.md). The website repository has its own history. You remain responsible for everything you submit.

## Questions

If you are not sure whether an idea fits, open an issue and ask. A short question is better than a large pull request that cannot be merged.
