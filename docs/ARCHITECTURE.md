# LayReX / LayeredChat — architecture

**LayReX** is the project name; **LayeredChat** is the .NET package family. This document describes the library only (no product-specific backends).

## Design goals

- **Versioned behavior** — `OrchestrationProfileManifest` + registry keys (`OrchestrationRegistryKeys`) so multiple “personalities” coexist without copy-pasting prompts.
- **Thin core** — `LayeredChat.Core` has no Npgsql, MongoDB, Qdrant, EF Core, or domain types. Adapters live in named packages.
- **Pluggable model access** — `ILlmChatConnector` and `IStreamingLlmChatConnector`; shipped implementations for OpenAI-compatible HTTP and `Microsoft.Extensions.AI`.
- **First-class tools & context** — `IToolCatalog` / `IToolExecutor`; `IDataSourceProvider` for ordered context slices before each model call.
- **Streaming or buffered** — `RunTurnStreamingAsync` emits `OrchestrationStreamEnvelope` for SSE, SignalR, or buses.
- **Optional MCP** — `LayeredChat.Integrations.Mcp` maps Model Context Protocol tools into the same catalog/executor surface.
- **Agents** — `IChatAgent` binds a stable logical agent to one orchestration registry key on a shared orchestrator.

## Layering

```text
Host application
    → ILlmChatConnector (+ optional streaming)
    → IToolCatalog + IToolExecutor (+ optional MCP bridge)
    → IOrchestrationDefinitionRegistry + IDataSourceRegistry
    → LayeredChatOrchestrator
```

Recommended entry type: **`LayeredChatHost`** via **`LayeredChatHost.CreateBuilder()`** — one object to inject instead of wiring the orchestrator constructor by hand.

Manifest **allow-lists** tool names; the orchestrator never executes a tool not in that set.

Optional **`IToolRoundCatalogProvider`** (on `OrchestrationExecutionHooks`) may supply a **subset** of those names per model round so hosts can narrow or expand tools without a second HTTP stack.

## Layers vs host stack (terminology)

The diagram above is the **infrastructure / host stack**: connector, tools, data-source registry, orchestrator.

**Orchestration layers** are a separate concept: **versioned contribution packages** (`layer.json` under a bundle layout) that merge into an **effective** `OrchestrationProfileManifest` (tool allow-list union, ordered data-source ids, parameters, instruction fragments). See [ADR-0001-layers-and-composition.md](ADR-0001-layers-and-composition.md), [LAYER_PACKAGE_FORMAT.md](LAYER_PACKAGE_FORMAT.md), and `ILayerCompositionService` in Core.

- **Not automatic domain routing** — LayReX does not infer “user is in preference mode” and swap layers; the host chooses a stack (or a single manifest) per tenant, surface, or deployment.
- **Composition is deterministic** — Same baseline + same resolved layers yields the same effective manifest and same ordered instruction fragments.

## Packages

| Package | Responsibility |
|--------|----------------|
| `LayeredChat.Core` | Orchestrator, manifests, telemetry, forwarding DTOs, agents, composition helpers |
| `LayeredChat.Connectors.OpenAiCompatible` | Sync + SSE against OpenAI-style HTTP APIs |
| `LayeredChat.Connectors.ExtensionsAi` | Bridge to `IChatClient` |
| `LayeredChat.Data.PostgreSql` | Parameterized SQL slices + guarded read-only tool |
| `LayeredChat.Data.MongoDb` | Mongo `find`-style slice + tool |
| `LayeredChat.Data.Qdrant` | Vector search slice |
| `LayeredChat.Integrations.Mcp` | MCP client session → `IToolCatalog` + `IToolExecutor` |

## Core source layout (`src/Core/LayeredChat.Core/`)

Sources are grouped by concern (public namespace remains `LayeredChat`):

| Folder | Contents |
|--------|----------|
| `Chat/` | Messages and tool-call DTOs |
| `Llm/` | Connectors, stream frames, request options |
| `Tools/` | Catalogs and executors (including `CompositeToolCatalog`, `RoutedToolExecutor`) |
| `Context/` | Data sources and registries |
| `Profiles/` | Definitions, manifests, in-memory registry |
| `Layers/` | Layer stack manifest, contributions, composition (`ILayerCompositionService`) |
| `Orchestration/` | `LayeredChatOrchestrator`, turn DTOs, stream envelopes, session context |
| `Forward/` | HTTP forward to remote “version pods” |
| `Telemetry/` | Hooks and chained telemetry |
| `Agents/` | `LayeredChatAgent`, registry, `AgentTurnInput` |

## Version pods

Manifest fields `ExternalForwardUri` / `ExternalForwardTimeoutSeconds` plus `IHttpOrchestrationForwarder` allow forwarding a turn to another deployment (e.g. container pinned to a semantic version). The **VersionHost** sample implements the receive side.

## Telemetry and LLM connectors

- **`IOrchestrationTelemetry`** receives `OrchestrationStreamEnvelope` for the full turn (same shapes as streaming). See [TELEMETRY_AND_BILLING.md](TELEMETRY_AND_BILLING.md) for token semantics (`UsageUpdate`, `ModelRoundCompleted`, `TurnResultSummary`).
- **Choosing a model backend:** see [CONNECTORS.md](CONNECTORS.md) for the OpenAI-compatible-first matrix, gateways (LiteLLM), and when to add native connector packages.

## Security notes

- Manifests must not be treated as an authorization layer; enforce tenant and tool policy in the host.
- Prefer read-only DB roles and server-side resolution of SQL / vector targets over raw strings in manifests for untrusted users.

## Build

From repository root (this folder):

```bash
dotnet build LayeredChat.sln
dotnet test tests/LayeredChat.Core.Tests/LayeredChat.Core.Tests.csproj
```

Docker (version host sample), from the same root:

```bash
docker build -f deploy/version-pod/Dockerfile -t layered-chat-version-host:latest .
```

## References

- [README](../README.md) — quick start, MCP wiring, package list
- [TELEMETRY_AND_BILLING.md](TELEMETRY_AND_BILLING.md) — observability and billing hooks
- [CONNECTORS.md](CONNECTORS.md) — provider matrix and self-hosting
- [SAMPLES.md](SAMPLES.md) — short recipes (telemetry, MCP, tools per round)
- [ADR-0001-layers-and-composition.md](ADR-0001-layers-and-composition.md) — orchestration layers vs host stack
- [LAYER_PACKAGE_FORMAT.md](LAYER_PACKAGE_FORMAT.md) — layer bundle layout and merge rules
- [ROADMAP_LAYERS_AND_CLI.md](ROADMAP_LAYERS_AND_CLI.md) — CLI, control plane, pods
- [Model Context Protocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
