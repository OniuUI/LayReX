# Telemetry, tokens, and billing (LayReX / LayeredChat)

Hosts attach `IOrchestrationTelemetry` via `LayeredChatTurnRequest.Hooks.Telemetry`.

Optional hosts may register `ITurnContinuationEvaluator` on `Hooks.TurnContinuationEvaluator` to run after an assistant message with **no tool calls** (before the turn ends). The orchestrator emits `CompletionEvaluationStarted` / `CompletionEvaluationFinished` for observability. **Extra LLM calls** performed inside an evaluator are **not** counted in LayeredChat connector token totals; hosts bill those separately.

- **`RunTurnAsync`** — the orchestrator calls `EmitAsync` for each envelope internally.
- **`RunTurnStreamingAsync` via `LayeredChatHost`** — each yielded envelope is also passed to `Hooks.Telemetry` (so SSE bridges and observability stay aligned). If you call `LayeredChatOrchestrator.RunTurnStreamingAsync` directly, register telemetry yourself in the `await foreach` loop if needed.

## Per-round vs per-turn tokens

- **`OrchestrationStreamKind.UsageUpdate`** — typically one envelope **per model completion** with `InputTokens` and `OutputTokens` for **that round only** (values depend on the connector).
- **`OrchestrationStreamKind.ModelRoundCompleted`** — follows `UsageUpdate` for the same round; `Attributes` include `round`, `cumulativeInputTokens`, and `cumulativeOutputTokens` (running sums for the whole turn so far).
- **`OrchestrationStreamKind.TurnResultSummary`** — end of turn; `Attributes` include `totalInputTokens`, `totalOutputTokens`, `assistantText`, `appendedCount`. These totals should match the last `ModelRoundCompleted` cumulative values when the connector reports usage consistently. When a continuation evaluator produced metadata, keys are also prefixed with `completionEval_` (truncated).
- **`LayeredChatTurnResult.CompletionEvaluationMetadata`** — same metadata as the last successful evaluation (host keys only, not orchestrator `meta_*` copies on the wire).

Billing policies stay in the host: charge **per UsageUpdate**, **per ModelRoundCompleted**, or **once on TurnResultSummary** — LayeredChat does not deduct money.

## Envelope kinds (observability)

| Kind | Use |
|------|-----|
| `TurnStarted` | Turn boundary; correlation + registry key |
| `ContextSlicesReady` | Data-source slices merged into system prompt (`sliceCount`) |
| `ForwardingToExternal` / `ExternalForwardCompleted` | Version pod / HTTP forward path |
| `ModelRoundStarted` | New tool/model round (`round` in Attributes) |
| `UsageUpdate` | Token usage for the round just finished |
| `ModelRoundCompleted` | Round finished; cumulative token Attributes |
| `AssistantTextDelta` | Streaming text (when using streaming connector) |
| `ToolCallFinished` / `ToolExecutionStarted` / `ToolExecutionFinished` | Tool lifecycle |
| `AssistantMessageCommitted` | Messages appended to working transcript |
| `CompletionEvaluationStarted` | After `AssistantMessageCommitted` when `Hooks.TurnContinuationEvaluator` is set; `Attributes`: `round`, `orchestrationId`, `registryKey`, `evaluatorId` |
| `CompletionEvaluationFinished` | After evaluation; `Attributes`: `round`, `evaluatorId`, `loopEffect` (`CompleteTurn`, `AdvisoryOnly`, `ContinueWithInjectedMessages`), `turnEnds` (`true`/`false`), optional `invalidInjection`, optional `meta_*` (truncated host metadata), optional `injectedMessageCount` |
| `TurnResultSummary` | Final structured summary |
| `TurnCompleted` | Turn boundary |
| `Error` | Failure |

## Persistence and PII

Do not persist raw prompts or tool payloads unless your product requires it. Prefer redacted summaries derived from `Attributes` and numeric usage fields. `ChainedOrchestrationTelemetry` fans out to multiple sinks (e.g. logs + metrics + your DB writer).

## OpenTelemetry

Map `CorrelationId` and `RegistryKey` to trace or span attributes in your sink implementation so LLM rounds correlate with HTTP requests.
