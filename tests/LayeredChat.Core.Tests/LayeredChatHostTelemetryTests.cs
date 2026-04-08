namespace LayeredChat.Tests;

public sealed class LayeredChatHostTelemetryTests
{
    private sealed class ListTelemetry : IOrchestrationTelemetry
    {
        public List<OrchestrationStreamKind> Kinds { get; } = new();

        public ValueTask EmitAsync(OrchestrationStreamEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Kinds.Add(envelope.Kind);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task RunTurnStreamingAsync_forwards_each_envelope_to_hooks_telemetry()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            AllowedToolNames = Array.Empty<string>(),
            DataSourceIdsInOrder = Array.Empty<string>()
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var connector = new SimpleTextConnector();
        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(connector)
            .UseDefinitionRegistry(definitions)
            .Build();

        var tel = new ListTelemetry();
        await foreach (var _ in host.RunTurnStreamingAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Hello",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { Telemetry = tel }
        }))
        {
        }

        Assert.Contains(OrchestrationStreamKind.ModelRoundCompleted, tel.Kinds);
        Assert.Contains(OrchestrationStreamKind.TurnResultSummary, tel.Kinds);
    }

    [Fact]
    public async Task RunTurnStreamingAsync_minimal_telemetry_skips_round_and_usage_envelopes()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            AllowedToolNames = Array.Empty<string>(),
            DataSourceIdsInOrder = Array.Empty<string>()
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var connector = new SimpleTextConnector();
        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(connector)
            .UseDefinitionRegistry(definitions)
            .Build();

        var tel = new ListTelemetry();
        await foreach (var _ in host.RunTurnStreamingAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Hello",
            PriorMessages = Array.Empty<ChatMessage>(),
            ConnectorOptions = new LlmRequestOptions { TelemetryVerbosity = OrchestrationTelemetryVerbosity.Minimal },
            Hooks = new OrchestrationExecutionHooks { Telemetry = tel }
        }))
        {
        }

        Assert.DoesNotContain(OrchestrationStreamKind.ModelRoundStarted, tel.Kinds);
        Assert.DoesNotContain(OrchestrationStreamKind.ModelRoundCompleted, tel.Kinds);
        Assert.DoesNotContain(OrchestrationStreamKind.UsageUpdate, tel.Kinds);
        Assert.Contains(OrchestrationStreamKind.AssistantTextDelta, tel.Kinds);
        Assert.Contains(OrchestrationStreamKind.TurnResultSummary, tel.Kinds);
    }

    private sealed class SimpleTextConnector : ILlmChatConnector
    {
        public string ConnectorKind => "Simple";

        public Task<LlmCompletionResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            LlmRequestOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmCompletionResult
            {
                TextContent = "done",
                InputTokens = 3,
                OutputTokens = 5
            });
    }
}
