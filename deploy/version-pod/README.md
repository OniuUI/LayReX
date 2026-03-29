# Version pod (Docker / Kubernetes)

Build context must be the **LayReX repository root** (folder containing `LayeredChat.sln`).

## Environment variables

| Variable | Description |
|----------|-------------|
| `LAYEREDCHAT_MANIFEST_PATH` | Path inside the container to baseline `OrchestrationProfileManifest` JSON. |
| `LAYEREDCHAT_LAYER_BUNDLE_ROOT` | Optional. Directory containing `stack.json` and `layers/{layerId}/{version}/layer.json`. When set, layers are composed at startup into the effective manifest; instruction fragments prefix forwarded turns. See [docs/LAYER_PACKAGE_FORMAT.md](../../docs/LAYER_PACKAGE_FORMAT.md). |
| `OPENAI_COMPATIBLE_BASE_URL` | OpenAI-compatible API base URL. |
| `OPENAI_COMPATIBLE_MODEL` | Default model id. |
| `OPENAI_API_KEY` | API key if required. |

## Example bundle

`example-bundle/` is a minimal stack for local testing. Mount it read-only and set `LAYEREDCHAT_LAYER_BUNDLE_ROOT=/layers`.

## Compose snippet (layers enabled)

```yaml
environment:
  LAYEREDCHAT_MANIFEST_PATH: /config/manifest.json
  LAYEREDCHAT_LAYER_BUNDLE_ROOT: /layers
volumes:
  - ./example-manifest.json:/config/manifest.json:ro
  - ./example-bundle:/layers:ro
```
