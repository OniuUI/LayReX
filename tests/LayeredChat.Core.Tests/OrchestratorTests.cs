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

    private sealed class ToolCountCapturingConnector : ILlmChatConnector
    {
        public string ConnectorKind => "ToolCount";

        public List<int> ToolCountsPerCall { get; } = new();

        public int Phase { get; set; }

        public Task<LlmCompletionResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            LlmRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            ToolCountsPerCall.Add(tools.Count);
            Phase++;
            if (Phase == 1)
            {
                return Task.FromResult(new LlmCompletionResult
                {
                    ToolCalls = new[]
                    {
                        new ToolCallRequest { CallId = "1", Name = "a", ArgumentsJson = "{}" }
                    }
                });
            }

            return Task.FromResult(new LlmCompletionResult { TextContent = "ok" });
        }
    }

    private sealed class NarrowRoundZeroProvider : IToolRoundCatalogProvider
    {
        public IReadOnlyList<string>? GetActiveToolNamesForRound(
            int roundIndex,
            IReadOnlyList<ChatMessage> workingMessages,
            OrchestrationProfileManifest manifest) =>
            roundIndex == 0 ? new[] { "a" } : null;
    }

    [Fact]
    public async Task ToolRoundCatalogProvider_narrows_tools_on_first_round_only()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            AllowedToolNames = new[] { "a", "b" },
            DataSourceIdsInOrder = Array.Empty<string>()
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var connector = new ToolCountCapturingConnector();
        var toolA = new ToolDefinition
        {
            Name = "a",
            Description = "A",
            ParametersSchemaJson = "{}"
        };
        var toolB = new ToolDefinition
        {
            Name = "b",
            Description = "B",
            ParametersSchemaJson = "{}"
        };

        var tools = new DictionaryToolCatalog(new[] { toolA, toolB });
        var executor = new EchoToolExecutor();
        var dataSources = new DataSourceRegistry(Array.Empty<IDataSourceProvider>());
        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions, dataSources);

        await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Use tools.",
            UserMessageContent = "Go",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { ToolRoundCatalogProvider = new NarrowRoundZeroProvider() }
        });

        Assert.Equal(2, connector.ToolCountsPerCall.Count);
        Assert.Equal(1, connector.ToolCountsPerCall[0]);
        Assert.Equal(2, connector.ToolCountsPerCall[1]);
    }

    [Fact]
    public async Task ToolRoundCatalogProvider_rejects_name_outside_manifest()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            AllowedToolNames = new[] { "a" },
            DataSourceIdsInOrder = Array.Empty<string>()
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var badProvider = new BadProvider();
        var orchestrator = new LayeredChatOrchestrator(
            new RecordingConnector(),
            new EchoToolExecutor(),
            new DictionaryToolCatalog(Array.Empty<ToolDefinition>()),
            definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Go",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { ToolRoundCatalogProvider = badProvider }
        }));
    }

    private sealed class BadProvider : IToolRoundCatalogProvider
    {
        public IReadOnlyList<string>? GetActiveToolNamesForRound(
            int roundIndex,
            IReadOnlyList<ChatMessage> workingMessages,
            OrchestrationProfileManifest manifest) =>
            new[] { "nope" };
    }

    private sealed class ListEnvelopeTelemetry : IOrchestrationTelemetry
    {
        public List<OrchestrationStreamEnvelope> Envelopes { get; } = new();

        public ValueTask EmitAsync(OrchestrationStreamEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Envelopes.Add(envelope);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StaticContinuationEvaluator : ITurnContinuationEvaluator
    {
        public StaticContinuationEvaluator(TurnContinuationEvaluationResult result) => Result = result;

        private TurnContinuationEvaluationResult Result { get; }

        public string EvaluatorId => "static";

        public ValueTask<TurnContinuationEvaluationResult> EvaluateAfterAssistantRoundAsync(
            TurnContinuationEvaluationContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result);
    }

    private sealed class ThrowingContinuationEvaluator : ITurnContinuationEvaluator
    {
        public string EvaluatorId => "throw";

        public ValueTask<TurnContinuationEvaluationResult> EvaluateAfterAssistantRoundAsync(
            TurnContinuationEvaluationContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("eval boom");
    }

    private sealed class InjectOnceThenCompleteEvaluator : ITurnContinuationEvaluator
    {
        private int _rounds;

        public string EvaluatorId => "inject-once";

        public ValueTask<TurnContinuationEvaluationResult> EvaluateAfterAssistantRoundAsync(
            TurnContinuationEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            _rounds++;
            if (_rounds == 1)
            {
                return ValueTask.FromResult(new TurnContinuationEvaluationResult
                {
                    LoopEffect = TurnContinuationLoopEffect.ContinueWithInjectedMessages,
                    MessagesToInjectBeforeNextModelRound = new[]
                    {
                        new ChatMessage { Role = ChatRole.User, Content = "go on" }
                    }
                });
            }

            return ValueTask.FromResult(new TurnContinuationEvaluationResult
            {
                LoopEffect = TurnContinuationLoopEffect.CompleteTurn
            });
        }
    }

    [Fact]
    public async Task TurnContinuationEvaluator_absent_behaves_as_before()
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

        var connector = new RecordingConnector();
        var tools = new DictionaryToolCatalog(Array.Empty<ToolDefinition>());
        var executor = new EchoToolExecutor();
        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var tel = new ListEnvelopeTelemetry();
        await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Hello",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { Telemetry = tel }
        });

        Assert.DoesNotContain(tel.Envelopes, e => e.Kind == OrchestrationStreamKind.CompletionEvaluationStarted);
        Assert.Equal(1, connector.Calls);
    }

    [Fact]
    public async Task TurnContinuationEvaluator_AdvisoryOnly_emits_telemetry_and_metadata_on_result()
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

        var connector = new RecordingConnector();
        var tools = new DictionaryToolCatalog(Array.Empty<ToolDefinition>());
        var executor = new EchoToolExecutor();
        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var eval = new StaticContinuationEvaluator(new TurnContinuationEvaluationResult
        {
            LoopEffect = TurnContinuationLoopEffect.AdvisoryOnly,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["verdict"] = "ok" }
        });

        var tel = new ListEnvelopeTelemetry();
        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Hello",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { Telemetry = tel, TurnContinuationEvaluator = eval }
        });

        Assert.Equal(1, connector.Calls);
        Assert.Contains(tel.Envelopes, e => e.Kind == OrchestrationStreamKind.CompletionEvaluationStarted);
        var finished = tel.Envelopes.Last(e => e.Kind == OrchestrationStreamKind.CompletionEvaluationFinished);
        Assert.NotNull(finished.Attributes);
        Assert.Equal("AdvisoryOnly", finished.Attributes!["loopEffect"]);
        Assert.Equal("true", finished.Attributes["turnEnds"]);
        Assert.NotNull(result.CompletionEvaluationMetadata);
        Assert.Equal("ok", result.CompletionEvaluationMetadata!["verdict"]);
    }

    [Fact]
    public async Task TurnContinuationEvaluator_ContinueWithInjectedMessages_runs_second_model_round()
    {
        var manifest = new OrchestrationProfileManifest
        {
            OrchestrationId = "demo",
            SemanticVersion = "1.0.0",
            DisplayName = "Demo",
            AllowedToolNames = Array.Empty<string>(),
            DataSourceIdsInOrder = Array.Empty<string>(),
            MaxToolIterations = 4
        };

        var definitions = new InMemoryOrchestrationDefinitionRegistry();
        definitions.Register(new OrchestrationDefinition { Manifest = manifest });

        var calls = 0;
        var connector = new RecordingConnector
        {
            OnComplete = _ =>
            {
                calls++;
                return calls == 1
                    ? new LlmCompletionResult { TextContent = "first" }
                    : new LlmCompletionResult { TextContent = "second" };
            }
        };

        var tools = new DictionaryToolCatalog(Array.Empty<ToolDefinition>());
        var executor = new EchoToolExecutor();
        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Hello",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { TurnContinuationEvaluator = new InjectOnceThenCompleteEvaluator() }
        });

        Assert.Equal(2, connector.Calls);
        Assert.Equal("second", result.AssistantText);
        Assert.Equal(3, result.AppendedMessages.Count);
    }

    [Fact]
    public async Task TurnContinuationEvaluator_invalid_injection_ends_turn()
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

        var connector = new RecordingConnector();
        var tools = new DictionaryToolCatalog(Array.Empty<ToolDefinition>());
        var executor = new EchoToolExecutor();
        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var eval = new StaticContinuationEvaluator(new TurnContinuationEvaluationResult
        {
            LoopEffect = TurnContinuationLoopEffect.ContinueWithInjectedMessages,
            MessagesToInjectBeforeNextModelRound = Array.Empty<ChatMessage>()
        });

        var tel = new ListEnvelopeTelemetry();
        await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Hello",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { Telemetry = tel, TurnContinuationEvaluator = eval }
        });

        Assert.Equal(1, connector.Calls);
        var finished = tel.Envelopes.Last(e => e.Kind == OrchestrationStreamKind.CompletionEvaluationFinished);
        Assert.Equal("true", finished.Attributes!["invalidInjection"]);
    }

    [Fact]
    public async Task TurnContinuationEvaluator_exception_emits_finished_with_error()
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

        var connector = new RecordingConnector();
        var tools = new DictionaryToolCatalog(Array.Empty<ToolDefinition>());
        var executor = new EchoToolExecutor();
        var orchestrator = new LayeredChatOrchestrator(connector, executor, tools, definitions,
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var tel = new ListEnvelopeTelemetry();
        await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = OrchestrationRegistryKeys.Compose("demo", "1.0.0"),
            SystemInstructionText = "Hi",
            UserMessageContent = "Hello",
            PriorMessages = Array.Empty<ChatMessage>(),
            Hooks = new OrchestrationExecutionHooks { Telemetry = tel, TurnContinuationEvaluator = new ThrowingContinuationEvaluator() }
        });

        var finished = tel.Envelopes.Last(e => e.Kind == OrchestrationStreamKind.CompletionEvaluationFinished);
        Assert.Equal("true", finished.Attributes!["error"]);
        Assert.Equal(1, connector.Calls);
    }
}
