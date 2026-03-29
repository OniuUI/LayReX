# Layer package format (v1)

LayReX **orchestration layers** are immutable versioned units merged into an effective `OrchestrationProfileManifest`. This document specifies the on-disk **bundle** layout, `layer.json` fields, and merge semantics (see `LayerComposition` in `LayeredChat.Core`).

## Bundle directory layout

```text
bundle-root/
  stack.json                 # LayerStackManifest — ordered layer references
  layers/
    {layerId}/
      {version}/
        layer.json           # LayerContribution metadata + deltas
        *.md                 # optional instruction files referenced from layer.json
```

- **`layerId`** and **`version`** are path segments. Use URL-safe identifiers (e.g. `my.product`, `1.0.0`). Avoid `..` or separators inside ids.
- **`stack.json`** lists `{ "layerId", "version" }` entries in merge order (baseline manifest is applied first; see below).

### stack.json

Top-level object (`LayerStackManifest`):

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `schemaVersion` | number | yes | Document format; use `1` for this spec. |
| `layers` | array | yes | Ordered `LayerReferenceEntry` objects. |

Each reference:

| Field | Type | Required |
|-------|------|----------|
| `layerId` | string | yes |
| `version` | string | yes (exact SemVer string for v1 resolvers) |

### layer.json

Maps to `LayerContribution`:

| Field | Type | Description |
|-------|------|-------------|
| `schemaVersion` | number | Layer document format; use `1`. |
| `layerId` | string | Must match parent directory id. |
| `semanticVersion` | string | Must match parent version directory. |
| `instructionFragment` | string | Optional inline instruction text. |
| `instructionMarkdownFiles` | string[] | Optional files relative to this directory (e.g. `policies.md`). Loaded and appended after `instructionFragment` by the host. |
| `allowedToolNames` | string[] | Unioned with baseline (case-insensitive dedupe, order preserved). |
| `dataSourceIdsInOrder` | string[] | Appended after baseline ids (dedupe). |
| `parameters` | object | String map; **last writer wins** per key across layers (after baseline). |
| `maxToolIterations` | number | Optional; effective value is **maximum** of baseline and all layers that set it. |
| `defaultTemperature` | number | Optional; **last** non-null layer wins (after baseline). |
| `outputCapabilities` | string / number | Optional flags; combined with baseline via bitwise **OR**. |

Secrets (API keys, connection strings) must **not** appear in layer packages.

## Embedded stack on orchestration manifest

`OrchestrationProfileManifest` may include optional `layerStack`:

```json
{
  "schemaVersion": 1,
  "orchestrationId": "demo",
  "semanticVersion": "1.0.0",
  "layerStack": {
    "schemaVersion": 1,
    "entries": [
      { "layerId": "base", "version": "1.0.0" }
    ]
  }
}
```

Hosts may use either a standalone `stack.json` or this embedded block, not both, depending on deployment (VersionHost supports a bundle directory with `stack.json`).

## Versioning

- **Manifest `schemaVersion`** (`OrchestrationProfileManifest`) — breaking changes to orchestration manifest JSON.
- **Layer stack / contribution `schemaVersion`** — breaking changes to `stack.json` or `layer.json` shapes (`LayerManifestVersions` in Core).
- **Layer identity** — `layerId` + `semanticVersion` (and/or registry digest when using a remote store).

SemVer **ranges** in references are not resolved by Core; a registry, CLI, or control plane may resolve `1.x` to a concrete version before calling `ILayerCompositionService`.

## Packed artifact (.layrex.zip)

The LayReX CLI packs the same tree (see `layrex layer pack`). Zip is a transport format; merge rules are identical to the expanded bundle.

## References

- [ADR-0001-layers-and-composition.md](ADR-0001-layers-and-composition.md)
- `LayerComposition`, `ILayerCompositionService`, `LayerContributionJson`, `LayerStackManifestJson` in `LayeredChat.Core`
