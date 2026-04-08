# ADR-0001: Orchestration layers and composition

## Status

Accepted.

## Context

LayReX (LayeredChat) already uses the word “layering” for the **host stack** (connector, tools, registry, orchestrator). Product teams also want **versioned behavioral layers**: composable slices (instructions, tool policy, data-source order, parameters) that merge into one **effective** `OrchestrationProfileManifest` before a turn.

This ADR defines terminology and where composition lives so we do not conflate:

- **Infrastructure stack** — wiring types (`ILlmChatConnector`, `IToolCatalog`, `LayeredChatOrchestrator`).
- **Orchestration layer** — an immutable, versioned **contribution** package (`layer.json` + optional assets) merged by rules into an effective manifest.

## Decision

1. **Layer** — A published unit identified by `layerId` and `semanticVersion`, packaged as described in [LAYER_PACKAGE_FORMAT.md](LAYER_PACKAGE_FORMAT.md). It contributes zero or more deltas to orchestration (instruction text fragment, tool names, data source ids, manifest parameters, optional loop/temperature hints).

2. **Layer stack** — Ordered list of layer references (`LayerStackManifest` or embedded `OrchestrationProfileManifest.LayerStack`). Resolution (path, URL, registry) is host-specific; **Core** only defines merge semantics via `ILayerCompositionService` / `LayerComposition`.

3. **Effective manifest** — Result of composing a **baseline** manifest (identity, forward URI, display metadata) with resolved `LayerContribution` instances. If no stack is present, the baseline is the effective manifest (backward compatible).

4. **Schema versioning** — `OrchestrationProfileManifest.schemaVersion` remains the manifest JSON evolution counter. `LayerStackManifest` and `layer.json` carry their own `schemaVersion` fields starting at `1`. Breaking changes increment the relevant schema, not NuGet version alone.

5. **Security** — Layers do not replace host authorization. Secrets stay out of layer packages; hosts may still override `SystemInstructionText` at turn time for tenant-specific content.

## Consequences

- Hosts and tools (CLI, VersionHost) resolve references → contributions; Core stays free of storage and network.
- Downstream hosts and products remain consumers; no product domains belong in `LayeredChat.Core`.
