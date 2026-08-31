---
name: deep-architecture-scan
description: 'Scan a project architecture in depth and produce an evidence-based map of modules, dependencies, runtime flows, data boundaries, security boundaries, and documented-versus-implemented gaps. Use for architecture analysis, codebase assessment, migration preparation, rearchitecture planning, dependency mapping, or investigating where a behavior is controlled.'
argument-hint: 'Describe the project area, architecture question, or depth of analysis required'
user-invocable: true
---

# Deep Architecture Scan

## Purpose

Build a reliable architectural picture from the repository itself. The result must distinguish:

- what the project documentation intends;
- what the build files and source code actually implement;
- what can be verified by a command or test;
- what remains unknown or requires a product/architecture decision.

Do not redesign the system during the scan. Do not silently resolve contradictions in canonical documentation. Surface them as findings or decisions needed from the project owner.

## When to Use

Use this skill when the user asks to:

- analyze or map an existing application architecture;
- understand module boundaries, dependencies, or runtime flows;
- prepare a migration, rewrite, or rearchitecture;
- explain where a behavior is controlled across layers;
- compare intended architecture with the implementation;
- produce architecture diagrams, an assessment, or an evidence-based repository map.

## Operating Rules

1. Start from the narrowest concrete anchor in the request: a file, symbol, failing behavior, command, module, or documented architecture decision.
2. Read applicable repository instructions before inspecting implementation files. Treat project handoff files and canonical architecture documents as constraints, not suggestions.
3. Prefer repository evidence over assumptions. Cite paths, symbols, project references, registrations, routes, configuration keys, and executable checks in the report.
4. Separate observed facts, inferred relationships, and open questions.
5. Keep the scan read-only unless the user explicitly requests an artifact. Generated reports or diagrams must be scoped to the requested output.
6. Do not expose secrets. Describe configuration keys and secret sources without copying credentials or tokens.
7. Do not broaden into a code review unless the user asks. Report architecture risks and inconsistencies only when they affect ownership, dependency direction, runtime behavior, security boundaries, operability, or migration feasibility.

## Procedure

### 1. Establish scope and constraints

- Restate the requested scope in one sentence: repository-wide, service, module, workflow, or behavior.
- Identify the concrete anchor and the nearest owning abstraction. If the starting file only forwards or registers behavior, follow one hop to the code that computes or mutates it.
- Locate and read applicable instruction files, handoff notes, README files, and canonical architecture/stack/domain documents.
- Record explicit decisions, constraints, known gaps, and commands supplied by the repository.

### 2. Inventory the repository

Collect a compact inventory of:

- applications, libraries, services, workers, and frontend packages;
- source, test, migration, infrastructure, scripts, and documentation directories;
- build manifests and lockfiles;
- entry points, composition roots, controllers/routes, background jobs, and CLI commands;
- persistence stores, external services, queues, vector stores, model/runtime dependencies, and deployment artifacts.

Use the repository's native tools where available (`rg`, `rg --files`, build-system dependency commands, project graph commands). Do not infer a module merely from a directory name when a project/build manifest can confirm it.

### 3. Reconstruct dependency direction

For each architectural module, record:

- declared project/package dependencies;
- important source-level imports or references;
- dependency direction and likely layering;
- composition-root registrations and adapter bindings;
- cycles, forbidden upward dependencies, duplicated responsibilities, and boundary leaks.

Distinguish declared dependencies from runtime calls. A project reference proves compile-time coupling; a registration, route, adapter call, or configuration binding provides stronger evidence of runtime participation.

### 4. Trace the important runtime flows

Select only flows relevant to the request, plus the minimum cross-cutting flows needed to explain them. Trace each from trigger to observable result:

1. entry point or event;
2. transport/controller/handler;
3. application service or use case;
4. domain decision or mutation;
5. port/interface boundary;
6. infrastructure adapter;
7. persistence or external system;
8. response, event, stream, notification, or audit output.

For each hop, capture the file and symbol that controls it. Note authorization, validation, transaction boundaries, retries, error translation, streaming, and logging where present. Mark a step `not verified` rather than filling gaps with a plausible implementation.

### 5. Map data and security boundaries

Describe:

- main entities/value objects/DTOs and their ownership;
- persistence schema, migrations, serialization, and data flow;
- read/write boundaries and consistency expectations visible in code;
- authentication, authorization, role/scope checks, secret/configuration sources;
- trust boundaries between browser, BFF/API, services, databases, model runtimes, and third parties;
- audit and technical logging paths.

Never include secret values in the output. Flag missing or unclear protection only when the evidence supports it, and label the result as an observation rather than a confirmed vulnerability unless security analysis was requested.

### 6. Compare intended and actual architecture

Create an explicit comparison with three statuses:

- `aligned`: documentation and implementation agree with evidence;
- `drift`: implementation differs from documented intent;
- `unknown`: evidence is insufficient or the decision is intentionally open.

For every drift or unknown, include the source paths, why it matters, the cheapest discriminating check, and whether the project owner must decide. Preserve already-accepted domain and architecture decisions; do not repair them as part of the scan.

### 7. Validate the map

Run the cheapest relevant checks available:

- project/package dependency listing or graph generation;
- solution/project build or type check for the scoped slice;
- targeted tests for the traced flow;
- route, migration, container, or configuration inspection commands;
- static searches that confirm registrations, references, or symbols.

A successful build validates compilation, not architectural correctness. Record command, scope, result, and limitations. If a command cannot run, explain the prerequisite or environmental blocker.

### 8. Produce the report

Return a concise but deep report in this order:

1. **Scope and conclusion**: one-paragraph architectural summary and confidence level.
2. **System map**: modules, responsibilities, entry points, and external systems.
3. **Dependency map**: table of edges, evidence, and boundary assessment.
4. **Runtime flows**: Mermaid sequence or flow diagrams for relevant paths.
5. **Data and security boundaries**: stores, trust boundaries, auth/scope, and observability.
6. **Documented-versus-implemented comparison**: `aligned`, `drift`, `unknown`.
7. **Findings**: ordered by architectural impact, each with evidence and consequence.
8. **Open decisions and unknowns**: questions that must be answered by the owner.
9. **Validation evidence**: commands run, results, and remaining test gaps.

Link to repository files using workspace-relative paths. Keep diagrams readable and avoid claiming a relationship that has no source evidence.

## Completion Criteria

The scan is complete only when:

- the requested scope and anchor are explicit;
- every reported module has repository evidence;
- dependency direction and composition-root wiring are covered;
- at least the relevant end-to-end runtime flow is traced;
- data, security, external-system, and observability boundaries are addressed where applicable;
- documented intent is compared with actual implementation;
- open questions are separated from findings and decisions are not invented;
- at least one executable or static validation check is recorded, or its absence is explained;
- the report states confidence and residual unknowns.

## Output Discipline

Use short tables and diagrams when they improve scanning. Prefer exact paths and symbols over broad prose. Keep the report proportional to scope: a focused behavior needs a focused trace, while a repository-wide request needs a full module and dependency map. Do not modify production code as a side effect of analysis.
