# Roadmap: versioned layers, CLI, and control plane

This document extends the approved LayReX plan with **what is implemented today** and **recommended next steps**. Host applications stay consumers of public packages; no domain coupling in `LayeredChat.Core`.

## Vision

- **Orchestration layers** — Named, versioned units that compose into an effective `OrchestrationProfileManifest` (tools, data-source order, parameters, instruction fragments) with deterministic merge rules.
- **Version control** — Immutable `layer.json` artifacts, registry storage, pins compatible with `OrchestrationRegistryKeys` and manifest `schemaVersion`.
- **LayReX CLI** — Validate, pack, push bundles; print version-pod run hints.
- **Optional control plane** — HTTP registry with local filesystem storage and OpenAPI description.

## Implemented in this repository

| Area | Location |
|------|----------|
| ADR (terminology) | [ADR-0001-layers-and-composition.md](ADR-0001-layers-and-composition.md) |
| Host stack vs layers | [ARCHITECTURE.md](ARCHITECTURE.md) |
| Layer bundle format | [LAYER_PACKAGE_FORMAT.md](LAYER_PACKAGE_FORMAT.md) |
| Core types and composition | `LayeredChat.Core` — `Layers/*`, optional `OrchestrationProfileManifest.LayerStack` |
| VersionHost bundle loading | `samples/VersionHost` — `LAYEREDCHAT_LAYER_BUNDLE_ROOT`, `LayerBundleDirectoryLoader` |
| Example bundle | `deploy/version-pod/example-bundle/` |
| Control plane API | `src/ControlPlane/LayeredChat.ControlPlane` — `PUT/POST /v1/layers/{id}/{version}`, list endpoints, `/openapi/v1.json` |
| .NET registry client | `src/ControlPlane/LayReX.ControlPlane.Client` — `LayReXRegistryClient` |
| CLI (sibling layout for separate git repo) | `../../LayReX-CLI/` — `LayReX.CLI.sln`, `layrex` global tool |

## Merge semantics (summary)

Implemented in `LayerComposition` / `ILayerCompositionService`:

- **Allowed tool names** — Union, case-insensitive dedupe, baseline order first.
- **Data source ids** — Append after baseline, dedupe.
- **Parameters** — Last writer wins per key.
- **Max tool iterations** — Maximum of baseline and layers that specify a value.
- **Default temperature** — Last non-null layer wins after baseline.
- **Output capabilities** — Bitwise OR with baseline.
- **Instruction fragments** — Collected in stack order; `LayerCompositionResult.JoinInstructionFragments` joins them.

## Open decisions

1. **Control plane MVP** — Filesystem-only registry is done; S3/Azure/Git-backed storage and auth (API keys, OIDC) are follow-ups.
2. **SemVer ranges** — Core resolves **exact** versions only; a registry or resolver service may map `1.x` to a digest before composition.
3. **Tenant overrides** — Per-tenant stacks likely live in product databases; control plane holds published catalog only.
4. **OCI / cosign** — Signing and OCI-compatible artifacts are not implemented; `.zip` pack is transport-only.

## Suggested phases (remaining)

1. **Authentication** on control plane; rate limits; TLS guidance in deploy docs.
2. **Pull path** for VersionHost — optional HTTP fetch of layers from registry into a temp bundle before `LayerBundleDirectoryLoader`.
3. **CI** — Publish `LayReX.ControlPlane.Client` and `LayReX.CLI` NuGet tools from the LayReX GitHub org.
4. **Product migration** — Move generic instruction blocks from app code into private layer packages; keep Finn/property specifics out of OSS.

## Related links

- [README](../README.md)
- [TELEMETRY_AND_BILLING.md](TELEMETRY_AND_BILLING.md)
- [LayReX-CLI README](../LayReX-CLI/README.md) (sibling folder under `libs/` in the monorepo layout)
