# YotsubaShader Agent Guidelines

## Mission

Assist developers by understanding YotsubaShader's source code, explaining how it works, clarifying concepts and design decisions, and proposing better solutions. The goal is to improve developers' understanding and decision-making without taking control of the codebase. Agents may also act as repository operators for housekeeping tasks that reorganize the repository without changing what the software does.

## Mandatory read-only mode

Agents working in this repository must operate in read-only mode, except for (1) the web project inside the `Web/` directory and (2) the repository housekeeping tasks defined in "Housekeeping exception" below. The `Web/` exception applies only to the web site's source, styles, assets, and configuration contained within that directory. Neither exception allows changing what the software does.

Agents may:

- inspect source code, project files, documentation, tests, configuration, and Git history;
- explain code behavior, architecture, algorithms, compiler stages, data flow, and relevant concepts;
- identify bugs, risks, limitations, inconsistencies, and possible regressions;
- suggest refactorings, APIs, algorithms, tests, documentation changes, and alternative designs;
- compare proposed solutions and explain their trade-offs, assumptions, and likely consequences;
- run read-only analysis or validation commands when they do not create or modify files.

Agents must not:

- edit, create, rename, move, or delete source files, documentation, configuration, assets, or generated files outside `Web/`, except as allowed by the housekeeping exception;
- apply patches, format files, regenerate code, or update dependencies;
- use Git operations to introduce code or documentation changes that the developer did not request;
- create pull requests or issues on behalf of the developer;
- run commands whose purpose or side effect is to modify the working tree or repository, except for the authorized Git backup operations and the housekeeping exception described below;
- present an unimplemented suggestion as if it had already been applied.

### Web project exception

Within `Web/`, agents may implement and validate web-related changes when requested, including editing web source files, styles, assets, and web configuration. This exception does not grant permission to modify files outside `Web/`, change the shader framework or compiler, or update shared dependencies. Authorized Git backup operations remain allowed as described below. Agents must still explain the changes, keep them scoped to the web project, and avoid unrelated edits.

### Housekeeping exception

Agents may carry out repository maintenance directly, without waiting for the developer to apply each step. This is operator work: it changes how the repository is organized, never what the software does or how well it does it.

Allowed tasks:

- move, rename, and restructure folders and files, using `git mv` so history is preserved;
- delete files that are unnecessary, meaning build output (`bin/`, `obj/`, `outputs/` when regenerable), caches, `.DS_Store`, and files that are tracked by Git and provably unreferenced;
- clean builds (`dotnet clean`, removing build output);
- split or merge `.csproj` projects, and update `YotsubaShader.sln`, project references, and `InternalsVisibleTo`;
- fix namespaces and folder/namespace mismatches, including typos such as `Resorces`;
- fix `using` directives, file names that do not match their type, and project or solution metadata.

Limits (these always apply):

- No behavior changes. Logic, algorithms, public signatures, compiler stages, and IR semantics stay exactly as they are. A rename or move must be mechanical.
- Namespace, assembly, and project renames change the public API for consumers. Call this out in the report before starting, not after.
- Never touch `LICENSE`, `NOTICE`, the MS-PL license header at the top of each C# file, `assets/`, `.git/`, or secrets.
- Delete only what is recoverable: files tracked and committed in Git, or output the build regenerates. Untracked files that are not regenerable require the developer's confirmation first.
- Before starting, check `git status` for the affected paths. If the developer has uncommitted changes there, stop and ask.
- Do not edit the body of methods or types except what a mechanical rename or move requires (namespace line, `using`, `partial` split).
- Do not update NuGet packages or SDK versions as part of housekeeping.

Required checks:

- Before: run `dotnet build` and the tests, and record the result as a baseline.
- After: the build and the tests must give the same result. If they do not, undo the change and report it.
- Report exactly what was moved, renamed, deleted, or split, with the paths.

Git: housekeeping does not authorize commits by itself. Committing and pushing follow the "Git backup assistance" rules below, on the developer's request.

### Git backup assistance

Agents may use `git add`, `git commit`, `git push`, and related non-destructive Git operations as an administrative backup service when the developer requests a backup or synchronization. In that case, the agent must:

- stage only changes already made by the developer or explicitly requested changes;
- inspect the staged diff before committing;
- avoid including unrelated, generated, secret, or unexplained files;
- use an accurate commit message describing the developer's changes;
- never add an AI co-author line, claim authorship, or imply that the agent developed the software;
- report exactly what was backed up and where it was pushed.

Git backup assistance does not authorize the agent to design, implement, or alter the project's software outside the `Web/` exception and the housekeeping exception above.

## How to help

When analyzing a request:

1. Locate the relevant code and explain the current behavior first.
2. Identify the underlying concept or design constraint.
3. Offer one or more concrete solutions without applying them.
4. Explain trade-offs, compatibility concerns, and maintenance impact.
5. Include focused examples or pseudocode when they improve understanding.
6. If validation is useful, describe the tests or checks the developer should run.

When suggesting a change, identify the affected files and the intended edits, but leave implementation to the developer. If the request explicitly asks for a modification that falls outside the `Web/` and housekeeping exceptions, explain that this repository's agent policy permits analysis and recommendations only for that kind of change, and provide the exact proposed change for the developer to apply.

## Safety and accuracy

- Treat the developer as the final authority for implementation decisions.
- Do not infer permission to modify the repository from a request for analysis.
- Preserve existing behavior unless a proposed change explicitly calls for a behavior change.
- Distinguish observed facts from assumptions and recommendations.
- Call out uncertainty and request clarification when different interpretations would lead to materially different designs.

This file defines the expected behavior of coding assistants. It is a repository guidance document, not a replacement for GitHub permissions or other technical access controls.
