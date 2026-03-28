# LLM connectors — provider matrix and plug-and-play

LayeredChat talks to models through **`ILlmChatConnector`** (and optional **`IStreamingLlmChatConnector`**). Prefer the smallest integration that supports **chat + tools + streaming** for your deployment.

## Tier 1 — OpenAI-compatible HTTP (default)

**Package:** `LayeredChat.Connectors.OpenAiCompatible`

Use when the upstream exposes OpenAI-style **`/v1/chat/completions`** (or configurable path) with tools in the request body.

| Deployment | Typical base URL | Notes |
|------------|------------------|--------|
| OpenAI | `https://api.openai.com/v1/` | Reference implementation |
| Azure OpenAI | `https://{resource}.openai.azure.com/openai/deployments/{deployment}/` | Often path-style; set `ChatCompletionsPath` if needed |
| Ollama | `http://localhost:11434/v1/` | Local; tool support varies by model |
| vLLM / SGLang / many inference servers | Your server URL + `/v1/` | [vLLM OpenAI server](https://docs.vllm.ai/en/stable/serving/openai_compatible_server.html) |
| LiteLLM proxy | Your proxy base | [LiteLLM](https://github.com/BerriAI/litellm) exposes OpenAI format to 100+ backends |
| Google Gemini (OpenAI adapter) | `https://generativelanguage.googleapis.com/v1beta/openai/` | Use API key; verify tool/streaming for your model — see `OpenAiCompatibleWellKnownEndpoints` |

**Host configuration:** only `HttpClient`, `OpenAiCompatibleOptions` (base URI, model, API key, path), and optional `LlmRequestOptions.ModelNameOverride` per turn.

## Tier 2 — Microsoft.Extensions.AI

**Package:** `LayeredChat.Connectors.ExtensionsAi`

Use when you already register **`IChatClient`** (Ollama MEAI, Azure AI Inference, etc.). Same orchestration and tool surfaces.

## Tier 3 — Native vendor APIs

Add a dedicated **`LayeredChat.Connectors.*`** package when:

- OpenAI compatibility is missing, incomplete, or wrong for **tools / streaming / usage**.
- You need vendor-specific auth (e.g. workload identity) without a gateway.

Backlog should be **ordered by demand** after validating Tier 1 with a gateway (LiteLLM) or vendor “OpenAI mode.”

## Self-hosting checklist

1. Confirm **tool calling** works for your model + server (some local models do not).
2. Point **`OpenAiCompatibleChatConnector`** at your `/v1/` base.
3. Set **`MaxToolRoundIterations`** in manifest or `LlmRequestOptions`.
4. For **multiple logical models** behind one URL, use **`ModelNameOverride`** per request or run LiteLLM with model routing.

## MCP and tools

Model Context Protocol tools are **not** connectors; they extend **`IToolCatalog` / `IToolExecutor`** via `LayeredChat.Integrations.Mcp`. Combine with **`CompositeToolCatalog`** and **`RoutedToolExecutor`** for host tools + MCP tools.

## Related

- [ARCHITECTURE.md](ARCHITECTURE.md) — layering
- [TELEMETRY_AND_BILLING.md](TELEMETRY_AND_BILLING.md) — usage envelopes
