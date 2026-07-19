using System.Net;
using System.Text;
using System.Text.Json;
using LayeredChat.Connectors.OpenAiCompatible;

namespace LayeredChat.Tests;

public sealed class HarnessPrimitivesTests
{
    private static OrchestrationDefinition Definition(
        string id,
        params string[] allowedTools)
    {
        return new OrchestrationDefinition
        {
            Manifest = new OrchestrationProfileManifest
            {
                OrchestrationId = id,
                SemanticVersion = "1.0.0",
                DisplayName = id,
                AllowedToolNames = allowedTools,
                DataSourceIdsInOrder = Array.Empty<string>()
            }
        };
    }

    private static string Key(string id) => OrchestrationRegistryKeys.Compose(id, "1.0.0");

    private sealed class ScriptedConnector : ILlmChatConnector
    {
        private readonly Func<IReadOnlyList<ChatMessage>, IReadOnlyList<ToolDefinition>, LlmRequestOptions, LlmCompletionResult> _script;

        public ScriptedConnector(
            Func<IReadOnlyList<ChatMessage>, IReadOnlyList<ToolDefinition>, LlmRequestOptions, LlmCompletionResult> script)
        {
            _script = script;
        }

        public string ConnectorKind => "Scripted";

        public int Calls { get; private set; }

        public Task<LlmCompletionResult> CompleteAsync(
            IReadOnlyList<ChatMessage> messages,
            IReadOnlyList<ToolDefinition> tools,
            LlmRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(_script(messages, tools, options));
        }
    }

    private static LlmCompletionResult ToolCallCompletion(params (string Name, string Args)[] calls)
    {
        return new LlmCompletionResult
        {
            ToolCalls = calls
                .Select((c, i) => new ToolCallRequest { CallId = $"c{i}", Name = c.Name, ArgumentsJson = c.Args })
                .ToList()
        };
    }

    // ---------- Tool lifecycle hooks ----------

    [Fact]
    public async Task PreToolUse_deny_surfaces_failure_and_fires_failure_hook()
    {
        var executed = false;
        var failureHookCalls = new List<string>();
        var executor = new DelegatingToolExecutor(
            new Dictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>
            {
                ["ping"] = (_, _, _) =>
                {
                    executed = true;
                    return Task.FromResult(new ToolExecutionResult { SummaryText = "pong" });
                }
            },
            NoOpToolExecutor.Instance);

        var round = 0;
        var connector = new ScriptedConnector((_, _, _) =>
            round++ == 0 ? ToolCallCompletion(("ping", "{}")) : new LlmCompletionResult { TextContent = "done" });

        var orchestrator = new LayeredChatOrchestrator(
            connector,
            executor,
            new DictionaryToolCatalog([new ToolDefinition { Name = "ping" }]),
            Registry(Definition("d", "ping")),
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go",
            Hooks = new OrchestrationExecutionHooks
            {
                ToolLifecycle = new ToolLifecycleHooks
                {
                    PreToolUse = (_, _) => ValueTask.FromResult(PreToolUseDecision.Deny("governance says no")),
                    PostToolUseFailure = (ctx, _) =>
                    {
                        failureHookCalls.Add(ctx.Result.SummaryText);
                        return ValueTask.CompletedTask;
                    }
                }
            }
        });

        Assert.False(executed);
        Assert.Equal("done", result.AssistantText);
        var toolMessage = result.AppendedMessages.Single(m => m.Role == ChatRole.Tool);
        Assert.Contains("governance says no", toolMessage.Content);
        Assert.Single(failureHookCalls);
    }

    [Fact]
    public async Task PreToolUse_rewrite_replaces_arguments()
    {
        string? seenArgs = null;
        var executor = new DelegatingToolExecutor(
            new Dictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>
            {
                ["ping"] = (args, _, _) =>
                {
                    seenArgs = args;
                    return Task.FromResult(new ToolExecutionResult { SummaryText = "pong" });
                }
            },
            NoOpToolExecutor.Instance);

        var round = 0;
        var connector = new ScriptedConnector((_, _, _) =>
            round++ == 0 ? ToolCallCompletion(("ping", "{\"a\":1}")) : new LlmCompletionResult { TextContent = "done" });

        var orchestrator = new LayeredChatOrchestrator(
            connector,
            executor,
            new DictionaryToolCatalog([new ToolDefinition { Name = "ping" }]),
            Registry(Definition("d", "ping")),
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go",
            Hooks = new OrchestrationExecutionHooks
            {
                ToolLifecycle = new ToolLifecycleHooks
                {
                    PreToolUse = (_, _) => ValueTask.FromResult(PreToolUseDecision.Rewrite("{\"a\":2}"))
                }
            }
        });

        Assert.Equal("{\"a\":2}", seenArgs);
    }

    // ---------- Transient retry + exception capture ----------

    [Fact]
    public async Task Transient_failure_is_retried_once_silently()
    {
        var attempts = 0;
        var executor = new DelegatingToolExecutor(
            new Dictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>
            {
                ["flaky"] = (_, _, _) =>
                {
                    attempts++;
                    return Task.FromResult(attempts == 1
                        ? new ToolExecutionResult
                        {
                            Success = false,
                            FailureKind = ToolFailureKind.Transient,
                            SummaryText = "timeout"
                        }
                        : new ToolExecutionResult { SummaryText = "recovered" });
                }
            },
            NoOpToolExecutor.Instance);

        var round = 0;
        var connector = new ScriptedConnector((_, _, _) =>
            round++ == 0 ? ToolCallCompletion(("flaky", "{}")) : new LlmCompletionResult { TextContent = "done" });

        var orchestrator = new LayeredChatOrchestrator(
            connector,
            executor,
            new DictionaryToolCatalog([new ToolDefinition { Name = "flaky" }]),
            Registry(Definition("d", "flaky")),
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go"
        });

        Assert.Equal(2, attempts);
        var toolMessage = result.AppendedMessages.Single(m => m.Role == ChatRole.Tool);
        Assert.Contains("recovered", toolMessage.Content);
    }

    [Fact]
    public async Task Executor_exception_is_captured_as_fatal_failure()
    {
        var executor = new DelegatingToolExecutor(
            new Dictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>
            {
                ["boom"] = (_, _, _) => throw new InvalidOperationException("kaputt")
            },
            NoOpToolExecutor.Instance);

        Exception? captured = null;
        var round = 0;
        var connector = new ScriptedConnector((_, _, _) =>
            round++ == 0 ? ToolCallCompletion(("boom", "{}")) : new LlmCompletionResult { TextContent = "done" });

        var orchestrator = new LayeredChatOrchestrator(
            connector,
            executor,
            new DictionaryToolCatalog([new ToolDefinition { Name = "boom" }]),
            Registry(Definition("d", "boom")),
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go",
            Hooks = new OrchestrationExecutionHooks
            {
                ToolLifecycle = new ToolLifecycleHooks
                {
                    PostToolUseFailure = (ctx, _) =>
                    {
                        captured = ctx.Exception;
                        return ValueTask.CompletedTask;
                    }
                }
            }
        });

        Assert.Equal("done", result.AssistantText);
        Assert.IsType<InvalidOperationException>(captured);
        var toolMessage = result.AppendedMessages.Single(m => m.Role == ChatRole.Tool);
        Assert.Contains("kaputt", toolMessage.Content);
    }

    // ---------- Parallel read-only execution ----------

    [Fact]
    public async Task Consecutive_readonly_calls_execute_in_parallel_with_ordered_results()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        async Task<ToolExecutionResult> SlowRead(string name)
        {
            if (Interlocked.Increment(ref started) == 2)
            {
                gate.TrySetResult();
            }

            await gate.Task.WaitAsync(TimeSpan.FromSeconds(5));
            return new ToolExecutionResult { SummaryText = $"read:{name}" };
        }

        var executor = new DelegatingToolExecutor(
            new Dictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>
            {
                ["read_a"] = (_, _, _) => SlowRead("a"),
                ["read_b"] = (_, _, _) => SlowRead("b")
            },
            NoOpToolExecutor.Instance);

        var round = 0;
        var connector = new ScriptedConnector((_, _, _) =>
            round++ == 0
                ? ToolCallCompletion(("read_a", "{}"), ("read_b", "{}"))
                : new LlmCompletionResult { TextContent = "done" });

        var orchestrator = new LayeredChatOrchestrator(
            connector,
            executor,
            new DictionaryToolCatalog(
            [
                new ToolDefinition { Name = "read_a", IsReadOnly = true },
                new ToolDefinition { Name = "read_b", IsReadOnly = true }
            ]),
            Registry(Definition("d", "read_a", "read_b")),
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go"
        });

        // Both started before either could finish => parallel. If sequential, the first would
        // deadlock waiting for the gate that only the second start releases (guarded by timeout).
        var toolMessages = result.AppendedMessages.Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Equal(2, toolMessages.Count);
        Assert.Contains("read:a", toolMessages[0].Content);
        Assert.Contains("read:b", toolMessages[1].Content);
    }

    [Fact]
    public async Task Mutation_calls_are_not_parallelized()
    {
        var concurrent = 0;
        var maxConcurrent = 0;

        async Task<ToolExecutionResult> Mutate(string name)
        {
            var now = Interlocked.Increment(ref concurrent);
            InterlockedMax(ref maxConcurrent, now);
            await Task.Delay(30);
            Interlocked.Decrement(ref concurrent);
            return new ToolExecutionResult { SummaryText = $"mut:{name}" };
        }

        var executor = new DelegatingToolExecutor(
            new Dictionary<string, Func<string, OrchestrationSessionContext, CancellationToken, Task<ToolExecutionResult>>>
            {
                ["mut_a"] = (_, _, _) => Mutate("a"),
                ["mut_b"] = (_, _, _) => Mutate("b")
            },
            NoOpToolExecutor.Instance);

        var round = 0;
        var connector = new ScriptedConnector((_, _, _) =>
            round++ == 0
                ? ToolCallCompletion(("mut_a", "{}"), ("mut_b", "{}"))
                : new LlmCompletionResult { TextContent = "done" });

        var orchestrator = new LayeredChatOrchestrator(
            connector,
            executor,
            new DictionaryToolCatalog(
            [
                new ToolDefinition { Name = "mut_a" },
                new ToolDefinition { Name = "mut_b" }
            ]),
            Registry(Definition("d", "mut_a", "mut_b")),
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go"
        });

        Assert.Equal(1, maxConcurrent);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int snapshot;
        do
        {
            snapshot = Volatile.Read(ref target);
            if (value <= snapshot)
            {
                return;
            }
        } while (Interlocked.CompareExchange(ref target, value, snapshot) != snapshot);
    }

    // ---------- Structured output fold ----------

    [Fact]
    public async Task Emit_result_tool_call_is_folded_into_text_content()
    {
        var connector = new ScriptedConnector((_, _, _) =>
            ToolCallCompletion((ResponseSchemaSpec.EmitToolName, "{\"answer\":\"42\"}")));

        var orchestrator = new LayeredChatOrchestrator(
            connector,
            NoOpToolExecutor.Instance,
            new DictionaryToolCatalog([]),
            Registry(Definition("d")),
            new DataSourceRegistry(Array.Empty<IDataSourceProvider>()));

        var result = await orchestrator.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go",
            ConnectorOptions = new LlmRequestOptions
            {
                ResponseSchema = new ResponseSchemaSpec
                {
                    SchemaName = "answer@1",
                    SchemaJson = "{\"type\":\"object\"}"
                }
            }
        });

        Assert.Equal(1, connector.Calls);
        Assert.Equal("{\"answer\":\"42\"}", result.AssistantText);
    }

    // ---------- Subagent dispatch ----------

    [Fact]
    public async Task Dispatch_agent_runs_child_turn_with_fresh_context_and_returns_summary()
    {
        IReadOnlyList<ChatMessage>? childMessages = null;
        var connector = new ScriptedConnector((messages, _, _) =>
        {
            if (messages[0].Content.Contains("CHILD-SYS", StringComparison.Ordinal))
            {
                childMessages = messages;
                return new LlmCompletionResult { TextContent = "child says: found 3 listings" };
            }

            return messages.Any(m => m.Role == ChatRole.Tool)
                ? new LlmCompletionResult { TextContent = "final answer" }
                : ToolCallCompletion(("dispatch_agent",
                    "{\"agent\":\"scout\",\"task\":\"find listings\",\"context\":\"budget 4M\"}"));
        });

        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(connector)
            .UseDefinitions(
                Definition("parent", "dispatch_agent"),
                Definition("child"))
            .UseSubAgents(
            [
                new SubAgentDefinition
                {
                    Name = "scout",
                    Description = "Finds listings",
                    OrchestrationRegistryKey = Key("child"),
                    SystemInstructionText = "CHILD-SYS: you are the scout."
                }
            ])
            .Build();

        var result = await host.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("parent"),
            SystemInstructionText = "PARENT-SYS",
            UserMessageContent = "find me a home"
        });

        Assert.Equal("final answer", result.AssistantText);
        Assert.NotNull(childMessages);
        Assert.DoesNotContain(childMessages!, m => m.Content.Contains("find me a home"));
        Assert.Contains(childMessages!, m =>
            m.Role == ChatRole.User &&
            m.Content.Contains("find listings") &&
            m.Content.Contains("budget 4M"));

        var toolMessage = result.AppendedMessages.Single(m => m.Role == ChatRole.Tool);
        Assert.Contains("child says: found 3 listings", toolMessage.Content);
        Assert.Contains("\"agent\":\"scout\"", toolMessage.Content);
    }

    [Fact]
    public async Task Dispatch_depth_limit_blocks_nested_dispatch()
    {
        var toolMessagesSeen = new List<string>();
        var connector = new ScriptedConnector((messages, _, _) =>
        {
            foreach (var m in messages.Where(m => m.Role == ChatRole.Tool))
            {
                toolMessagesSeen.Add(m.Content);
            }

            if (messages.Any(m => m.Role == ChatRole.Tool))
            {
                return new LlmCompletionResult { TextContent = "done" };
            }

            return ToolCallCompletion(("dispatch_agent", "{\"agent\":\"scout\",\"task\":\"again\"}"));
        });

        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(connector)
            .UseDefinitions(
                Definition("parent", "dispatch_agent"),
                Definition("child", "dispatch_agent"))
            .UseSubAgents(
            [
                new SubAgentDefinition
                {
                    Name = "scout",
                    Description = "Finds listings",
                    OrchestrationRegistryKey = Key("child"),
                    SystemInstructionText = "child"
                }
            ])
            .Build();

        var result = await host.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("parent"),
            UserMessageContent = "go"
        });

        Assert.Equal("done", result.AssistantText);
        // parent r0 (dispatch) -> child r0 (nested dispatch refused by depth guard) -> child r1 -> parent r1.
        // Without the guard a grandchild turn would add further connector calls.
        Assert.Equal(4, connector.Calls);
        Assert.Contains(toolMessagesSeen, c => c.Contains("Dispatch depth limit"));
    }

    [Fact]
    public async Task Dispatch_unknown_agent_fails_with_available_list()
    {
        var round = 0;
        var connector = new ScriptedConnector((_, _, _) =>
            round++ == 0
                ? ToolCallCompletion(("dispatch_agent", "{\"agent\":\"nope\",\"task\":\"x\"}"))
                : new LlmCompletionResult { TextContent = "done" });

        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(connector)
            .UseDefinitions(Definition("parent", "dispatch_agent"), Definition("child"))
            .UseSubAgents(
            [
                new SubAgentDefinition
                {
                    Name = "scout",
                    Description = "Finds listings",
                    OrchestrationRegistryKey = Key("child")
                }
            ])
            .Build();

        var result = await host.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("parent"),
            UserMessageContent = "go"
        });

        var toolMessage = result.AppendedMessages.Single(m => m.Role == ChatRole.Tool);
        Assert.Contains("Unknown agent 'nope'", toolMessage.Content);
        Assert.Contains("scout", toolMessage.Content);
        Assert.Equal("done", result.AssistantText);
    }

    // ---------- Skills ----------

    [Fact]
    public async Task Load_skill_injects_body_once_then_returns_marker()
    {
        var round = 0;
        var connector = new ScriptedConnector((_, _, _) => round++ switch
        {
            0 => ToolCallCompletion(("load_skill", "{\"skill\":\"finn-search\"}")),
            1 => ToolCallCompletion(("load_skill", "{\"skill\":\"finn-search\"}")),
            _ => new LlmCompletionResult { TextContent = "done" }
        });

        var host = LayeredChatHost.CreateBuilder()
            .UseConnector(connector)
            .UseDefinitions(Definition("d", "load_skill"))
            .UseSkills(new InMemoryKnowledgeSkillRegistry(
            [
                new KnowledgeSkillDefinition
                {
                    Name = "finn-search",
                    Description = "How to search Finn.",
                    BodyMarkdown = "Always widen the radius before giving up."
                }
            ]))
            .Build();

        var result = await host.RunTurnAsync(new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = Key("d"),
            UserMessageContent = "go"
        });

        var toolMessages = result.AppendedMessages.Where(m => m.Role == ChatRole.Tool).ToList();
        Assert.Equal(2, toolMessages.Count);
        Assert.Contains("Always widen the radius", toolMessages[0].Content);
        Assert.Contains("already loaded", toolMessages[1].Content);
    }

    [Fact]
    public void Skill_registry_scopes_by_agent_and_renders_index()
    {
        var registry = new InMemoryKnowledgeSkillRegistry(
        [
            new KnowledgeSkillDefinition
            {
                Name = "shared",
                Description = "For everyone.",
                BodyMarkdown = "x"
            },
            new KnowledgeSkillDefinition
            {
                Name = "scout-only",
                Description = "Scout knowledge.",
                BodyMarkdown = "y",
                Agents = ["property-scout"]
            }
        ]);

        Assert.Equal(2, registry.ListFor("property-scout").Count);
        Assert.Single(registry.ListFor("dashboard-builder"));

        var index = registry.RenderIndexFor("dashboard-builder");
        Assert.Contains("shared: For everyone.", index);
        Assert.DoesNotContain("scout-only", index);
    }

    // ---------- Connector request shapes ----------

    [Fact]
    public async Task OpenAi_native_schema_mode_sends_response_format()
    {
        var handler = new CaptureHandler();
        var sut = new OpenAiCompatibleChatConnector(new HttpClient(handler), new OpenAiCompatibleOptions
        {
            BaseUri = new Uri("https://example.com/v1/"),
            Model = "gpt-4o-mini"
        });

        await sut.CompleteAsync(
            [new ChatMessage { Role = ChatRole.User, Content = "hi" }],
            [],
            new LlmRequestOptions
            {
                ResponseSchema = new ResponseSchemaSpec
                {
                    SchemaName = "grounded-answer@1",
                    SchemaJson = "{\"type\":\"object\",\"properties\":{\"a\":{\"type\":\"string\"}}}"
                }
            });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var rf = doc.RootElement.GetProperty("response_format");
        Assert.Equal("json_schema", rf.GetProperty("type").GetString());
        var js = rf.GetProperty("json_schema");
        Assert.Equal("grounded-answer_1", js.GetProperty("name").GetString());
        Assert.True(js.GetProperty("strict").GetBoolean());
        Assert.Equal("object", js.GetProperty("schema").GetProperty("type").GetString());
        Assert.False(doc.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task OpenAi_forced_tool_mode_adds_emit_tool_and_forces_choice_when_no_tools()
    {
        var handler = new CaptureHandler();
        var sut = new OpenAiCompatibleChatConnector(new HttpClient(handler), new OpenAiCompatibleOptions
        {
            BaseUri = new Uri("https://example.com/v1/"),
            Model = "gpt-4o-mini"
        });

        await sut.CompleteAsync(
            [new ChatMessage { Role = ChatRole.User, Content = "hi" }],
            [],
            new LlmRequestOptions
            {
                ResponseSchema = new ResponseSchemaSpec
                {
                    SchemaName = "x",
                    SchemaJson = "{\"type\":\"object\"}",
                    Mode = ResponseSchemaMode.ForcedTool
                }
            });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.Equal(1, tools.GetArrayLength());
        Assert.Equal(
            ResponseSchemaSpec.EmitToolName,
            tools[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(
            ResponseSchemaSpec.EmitToolName,
            doc.RootElement.GetProperty("tool_choice").GetProperty("function").GetProperty("name").GetString());
    }

    [Fact]
    public async Task OpenAi_prompted_json_mode_appends_system_instruction()
    {
        var handler = new CaptureHandler();
        var sut = new OpenAiCompatibleChatConnector(new HttpClient(handler), new OpenAiCompatibleOptions
        {
            BaseUri = new Uri("https://example.com/v1/"),
            Model = "small-local"
        });

        await sut.CompleteAsync(
            [new ChatMessage { Role = ChatRole.User, Content = "hi" }],
            [],
            new LlmRequestOptions
            {
                ResponseSchema = new ResponseSchemaSpec
                {
                    SchemaName = "x",
                    SchemaJson = "{\"type\":\"object\"}",
                    Mode = ResponseSchemaMode.PromptedJson
                }
            });

        using var doc = JsonDocument.Parse(handler.LastRequestBody);
        var messages = doc.RootElement.GetProperty("messages");
        var last = messages[messages.GetArrayLength() - 1];
        Assert.Equal("system", last.GetProperty("role").GetString());
        Assert.Contains("JSON Schema", last.GetProperty("content").GetString());
        Assert.False(doc.RootElement.TryGetProperty("response_format", out _));
        Assert.False(doc.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public void Auto_mode_resolution_follows_profile_capabilities()
    {
        var spec = new ResponseSchemaSpec { SchemaName = "x", SchemaJson = "{}" };

        Assert.Equal(
            ResponseSchemaMode.NativeJsonSchema,
            spec.ResolveMode(null, ResponseSchemaMode.NativeJsonSchema));
        Assert.Equal(
            ResponseSchemaMode.ForcedTool,
            spec.ResolveMode(
                new LlmModelAdapterProfile { SupportsNativeJsonSchema = false },
                ResponseSchemaMode.NativeJsonSchema));
        Assert.Equal(
            ResponseSchemaMode.PromptedJson,
            spec.ResolveMode(
                new LlmModelAdapterProfile { SupportsNativeJsonSchema = false, SupportsForcedToolChoice = false },
                ResponseSchemaMode.NativeJsonSchema));
    }

    private static InMemoryOrchestrationDefinitionRegistry Registry(params OrchestrationDefinition[] definitions)
    {
        var registry = new InMemoryOrchestrationDefinitionRegistry();
        foreach (var definition in definitions)
        {
            registry.Register(definition);
        }

        return registry;
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            const string responseJson =
                """{"choices":[{"message":{"content":"ok"}}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
