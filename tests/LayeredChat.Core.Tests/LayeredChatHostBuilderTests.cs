namespace LayeredChat.Tests;

public sealed class LayeredChatHostBuilderTests
{
    [Fact]
    public void Build_without_connector_throws()
    {
        var b = LayeredChatHost.CreateBuilder().UseDefinitions(MinimalDefinition());
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public void Build_without_definitions_throws()
    {
        var b = LayeredChatHost.CreateBuilder().UseConnector(new StubConnector());
        Assert.Throws<InvalidOperationException>(() => b.Build());
    }

    [Fact]
    public async Task Build_minimal_runs_turn()
    {
        var def = MinimalDefinition();
        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(new StubConnector())
            .UseDefinitions(def)
            .Build();

        var result = await host.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = def.RegistryKey,
            UserMessageContent = "hi",
            SystemInstructionText = "sys",
            PriorMessages = Array.Empty<ChatMessage>()
        });

        Assert.Equal("ok", result.AssistantText);
    }

    [Fact]
    public void CreateAgent_returns_agent_for_same_orchestrator()
    {
        var def = MinimalDefinition();
        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(new StubConnector())
            .UseDefinitions(def)
            .Build();

        var agent = host.CreateAgent(def.RegistryKey);
        Assert.Equal(def.RegistryKey, agent.OrchestrationRegistryKey);
    }

    private static OrchestrationDefinition MinimalDefinition()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "t",
            SemanticVersion = "1.0.0",
            DisplayName = "t",
            AllowedToolNames = Array.Empty<string>()
        };

        return new OrchestrationDefinition { Manifest = manifest };
    }

    private sealed class StubConnector : ILlmChatConnector
    {
        public string ConnectorKind => "stub";

        public Task<LlmCompletionResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            LlmRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmCompletionResult { TextContent = "ok" });
        }
    }
}
