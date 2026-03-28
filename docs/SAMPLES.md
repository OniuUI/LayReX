# Sample integration patterns

Concise recipes; full projects live under `samples/` in this repository.

## OpenAI-compatible: swap base URL only

```csharp
var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
var opts = OpenAiCompatibleWellKnownEndpoints.Ollama(new Uri("http://127.0.0.1:11434"), "llama3.2", apiKey: null);
var connector = new OpenAiCompatibleChatConnector(http, opts);
```

Use `OpenAiCompatibleWellKnownEndpoints.GoogleGeminiOpenAiCompatible(key, model)` when targeting Gemini’s OpenAI adapter URL.

## Telemetry + billing fan-out

```csharp
var telemetry = new ChainedOrchestrationTelemetry(new IOrchestrationTelemetry[]
{
    new MyMetricsSink(),
    new MyAuditLogSink()
});

var request = new LayeredChatTurnRequest
{
    // ...
    Hooks = new OrchestrationExecutionHooks { Telemetry = telemetry }
};
```

Implement each sink’s `EmitAsync` by switching on `OrchestrationStreamEnvelope.Kind` (see [TELEMETRY_AND_BILLING.md](TELEMETRY_AND_BILLING.md)).

## MCP + host tools

Follow the MCP section in [README](../README.md): `McpOrchestrationWiring.CombineHostCatalogThenMcp` and `RoutePrefixedMcpThenHost`.

## Data sources + manifest

Register `IDataSourceProvider` instances on `DataSourceRegistry`, list IDs in manifest `DataSourceIdsInOrder`, and pass SQL or vector parameters via `OrchestrationProfileManifest.Parameters`.

## Per-round tool narrowing

```csharp
Hooks = new OrchestrationExecutionHooks
{
    ToolRoundCatalogProvider = new MyRouter() // implements IToolRoundCatalogProvider
}
```

Return a subset of `AllowedToolNames` for early rounds, or `null` to use the full allow-list.
