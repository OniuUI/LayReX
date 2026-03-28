namespace LayeredChat.Tests;

public sealed class OrchestratorTests
{
    private sealed class RecordingConnector : ILlmChatConnector
    {
        public string ConnectorKind => "Recording";

        public int Calls { get; private set; }

        public Func<IReadOnlyList<ChatMessage>, LlmCompletionResult>? OnComplete { get; init; }

        public Task<LlmCompletionResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            LlmRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var handler = OnComplete ?? (_ => new LlmCompletionResult { TextContent = "ok" });
            return Task.FromResult(handler(messages));
        }
    }

    private sealed class EchoToolExecutor : IToolExecutor
    {
        public List<(string Name, string Args)> Invocations { get; } = new();

        public Task<ToolExecutionResult> ExecuteAsync(
            string toolName,
            string argumentsJson,
            OrchestrationSessionContext session,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add((toolName, argumentsJson));
            return Task.FromResult(new ToolExecutionResult
            {
                Success = true,
                SummaryText = $"executed:{toolName}"
            });
        }
    }

    private sealed class StaticSliceProvider : IDataSourceProvider
    {
        public StaticSliceProvider(string id, ContextSlice slice)
        {
            Id = id;
            Slice = slice;
        }

        public string Id { get; }

        public DataSourceKind Kind => DataSourceKind.Custom;

        private ContextSlice Slice { get; }

        public Task<ContextSlice> GetSliceAsync(
            OrchestrationSessionContext session,
            IReadOnlyDictionary<string, string> parameters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Slice);
        }
    }

    [Fact]
    public async Task Merges_data_source_into_system_then_returns_assistant_text()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            DataSourceIdsInOrder = new[] { "facts" },
            AllowedToolNames = Array.Empty<string>()
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var connector = new RecordingConnector();
        var tools = new DictionaryToolCatalog(Array.Empty<ToolDefinition>());
        var executor = new EchoToolExecutor();
        var slice = new ContextSlice { Label = "Facts", Text = "Oslo is in Norway." };
        var dataSources = new DataSourceRegistry(new[] { new StaticSliceProvider("facts", slice) });

        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions, dataSources);

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "You are helpful.",
            UserMessageContent = "Hi",
            PriorMessages = Array.Empty<ChatMessage>()
        });

        Assert.Equal("ok", result.AssistantText);
        Assert.Single(result.AppendedMessages);
        Assert.Equal(ChatRole.Assistant, result.AppendedMessages[0].Role);
        Assert.Equal(1, connector.Calls);
        Assert.Empty(executor.Invocations);
    }

    [Fact]
    public async Task Runs_tool_round_then_finishes_with_text()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            AllowedToolNames = new[] { "ping" },
            DataSourceIdsInOrder = Array.Empty<string>()
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var calls = 0;
        var connector = new RecordingConnector
        {
            OnComplete = _ =>
            {
                calls++;
                if (calls == 1)
                {
                    return new LlmCompletionResult
                    {
                        ToolCalls = new[]
                        {
                            new ToolCallRequest
                            {
                                CallId = "1",
                                Name = "ping",
                                ArgumentsJson = "{}"
                            }
                        }
                    };
                }

                return new LlmCompletionResult { TextContent = "done" };
            }
        };

        var tool = new ToolDefinition
        {
            Name = "ping",
            Description = "Ping",
            ParametersSchemaJson = "{\"type\":\"object\"}"
        };

        var tools = new DictionaryToolCatalog(new[] { tool });
        var executor = new EchoToolExecutor();
        var dataSources = new DataSourceRegistry(Array.Empty<IDataSourceProvider>());

        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions, dataSources);

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Use tools when needed.",
            UserMessageContent = "Go",
            PriorMessages = Array.Empty<ChatMessage>()
        });

        Assert.Equal("done", result.AssistantText);
        Assert.Equal(3, result.AppendedMessages.Count);
        Assert.Equal(ChatRole.Assistant, result.AppendedMessages[0].Role);
        Assert.NotNull(result.AppendedMessages[0].ToolCalls);
        Assert.Equal(ChatRole.Tool, result.AppendedMessages[1].Role);
        Assert.Equal(ChatRole.Assistant, result.AppendedMessages[2].Role);
        Assert.Single(executor.Invocations);
        Assert.Equal(2, connector.Calls);
    }
}
