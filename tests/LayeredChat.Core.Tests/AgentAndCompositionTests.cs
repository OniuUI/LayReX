namespace LayeredChat.Tests;

public sealed class AgentAndCompositionTests
{
    [Fact]
    public void CompositeToolCatalog_prefers_first_catalog_on_name_collision()
    {
        var a = new ToolDefinition { Name = "t", Description = "first", ParametersSchemaJson = "{}" };
        var b = new ToolDefinition { Name = "t", Description = "second", ParametersSchemaJson = "{\"type\":\"object\"}" };
        var first = new DictionaryToolCatalog([a]);
        var second = new DictionaryToolCatalog([b]);
        var composite = new CompositeToolCatalog([first, second]);

        Assert.True(composite.TryGet("t", out var def));
        Assert.Equal("first", def!.Description);
    }

    [Fact]
    public void CompositeToolCatalog_resolve_allowed_pulls_from_either_layer()
    {
        var host = new DictionaryToolCatalog([new ToolDefinition { Name = "h", Description = "", ParametersSchemaJson = "{}" }]);
        var mcp = new DictionaryToolCatalog([new ToolDefinition { Name = "m", Description = "", ParametersSchemaJson = "{}" }]);
        var composite = new CompositeToolCatalog([host, mcp]);

        var resolved = composite.ResolveAllowed(["h", "m"]);
        Assert.Equal(2, resolved.Count);
    }

    [Fact]
    public async Task RoutedToolExecutor_prefix_routes_to_expected_executor()
    {
        var mcp = new RecordingExecutor("mcp");
        var host = new RecordingExecutor("host");
        var router = RoutedToolExecutor.FromPrefix("ext_", StringComparison.Ordinal, mcp, host);

        await router.ExecuteAsync("ext_echo", "{}", new OrchestrationSessionContext(), default);
        await router.ExecuteAsync("local", "{}", new OrchestrationSessionContext(), default);

        Assert.Equal("mcp", mcp.LastKind);
        Assert.Equal("host", host.LastKind);
    }

    [Fact]
    public async Task LayeredChatAgent_resolves_registered_orchestration_key()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "agent-demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Agent demo",
            AllowedToolNames = Array.Empty<string>()
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var connector = new SimpleConnector();
        var orchestrator = new LayeredChatOrchestrator(
            connector,
            new NoopToolExecutor(),
            new DictionaryToolCatalog([]),
            definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var key = OrchestrationRegistryKeys.Compose("agent-demo", "1.0.0");
        var agent = new LayeredChatAgent(orchestrator, key);

        var result = await agent.RunTurnAsync(new AgentTurnInput
        {
            UserMessageContent = "hi",
            SystemInstructionText = "sys"
        });

        Assert.Equal("ok", result.AssistantText);

        var badAgent = new LayeredChatAgent(orchestrator, "missing:none");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            badAgent.RunTurnAsync(new AgentTurnInput
            {
                UserMessageContent = "x",
                SystemInstructionText = "s"
            }));
    }

    [Fact]
    public void LayeredChatAgentRegistry_exposes_same_key_as_registered()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "x",
            SemanticVersion = "1.0.0",
            DisplayName = "x",
            AllowedToolNames = Array.Empty<string>()
        };
        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var orchestrator = new LayeredChatOrchestrator(
            new SimpleConnector(),
            new NoopToolExecutor(),
            new DictionaryToolCatalog([]),
            definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var registry = new LayeredChatAgentRegistry(orchestrator);
        var k = OrchestrationRegistryKeys.Compose("x", "1.0.0");
        registry.Register("sales", k);

        Assert.Equal(k, registry.Get("sales").OrchestrationRegistryKey);
        Assert.Throws<KeyNotFoundException>(() => registry.Get("missing"));
    }

    private sealed class RecordingExecutor : IToolExecutor
    {
        public RecordingExecutor(string kind)
        {
            Kind = kind;
        }

        private string Kind { get; }

        public string? LastKind { get; private set; }

        public Task<ToolExecutionResult> ExecuteAsync(
            string toolName,
            string argumentsJson,
            OrchestrationSessionContext session,
            CancellationToken cancellationToken = default)
        {
            LastKind = Kind;
            return Task.FromResult(new ToolExecutionResult { Success = true, SummaryText = Kind });
        }
    }

    private sealed class SimpleConnector : ILlmChatConnector
    {
        public string ConnectorKind => "Simple";

        public Task<LlmCompletionResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            LlmRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new LlmCompletionResult { TextContent = "ok" });
        }
    }

    private sealed class NoopToolExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(
            string toolName,
            string argumentsJson,
            OrchestrationSessionContext session,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ToolExecutionResult { Success = true, SummaryText = "noop" });
        }
    }
}
