using System.Text.Json;

namespace LayeredChat;

/// <summary>Behavioral options for subagent dispatch.</summary>
public sealed class SubAgentDispatchOptions
{
    public const string DefaultToolName = "dispatch_agent";

    /// <summary>Session property carrying the current dispatch depth.</summary>
    public const string DepthPropertyKey = "layrex.dispatch.depth";

    /// <summary>Session property carrying the active subagent name inside a child turn.</summary>
    public const string AgentPropertyKey = "layrex.dispatch.agent";

    public string ToolName { get; init; } = DefaultToolName;

    /// <summary>Maximum nesting depth; 1 means subagents cannot dispatch further.</summary>
    public int MaxDispatchDepth { get; init; } = 1;

    /// <summary>Child turn envelopes are forwarded to this telemetry sink when set.</summary>
    public IOrchestrationTelemetry? Telemetry { get; init; }

    /// <summary>Tool lifecycle hooks (permission gate, audit) applied inside child turns.</summary>
    public ToolLifecycleHooks? ChildToolLifecycle { get; init; }

    /// <summary>Truncation limit for the child's assistant text in the tool summary.</summary>
    public int MaxResultChars { get; init; } = 20000;

    /// <summary>
    /// Marks the dispatch tool read-only so independent dispatches in one round execute concurrently.
    /// Disable when the host's tool executor or turn state is not thread-safe.
    /// </summary>
    public bool AllowParallelDispatch { get; init; } = true;

    /// <summary>
    /// When set, the dispatch executor streams child turn envelopes (text deltas, tool starts/dones)
    /// through this callback in real time instead of buffering the entire child turn.
    /// The host maps envelopes to its own wire format (SSE, polling progress, etc.).
    /// </summary>
    public Func<OrchestrationStreamEnvelope, CancellationToken, ValueTask>? StreamChildEnvelope { get; init; }
}

/// <summary>
/// Produces the <c>dispatch_agent</c> tool definition for a set of subagents.
/// The manifest allow-list must include the tool name for it to be exposed.
/// </summary>
public static class SubAgentDispatchToolFactory
{
    public static ToolDefinition CreateToolDefinition(
        IReadOnlyList<SubAgentDefinition> agents,
        string toolName = SubAgentDispatchOptions.DefaultToolName,
        bool allowParallelDispatch = true)
    {
        ArgumentNullException.ThrowIfNull(agents);
        var agentDocs = agents.Select(a => $"{a.Name}: {a.Description}").ToList();
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "agent", "task" },
            properties = new
            {
                agent = new
                {
                    type = "string",
                    description = "Specialist to dispatch. " + string.Join(" | ", agentDocs),
                    @enum = agents.Select(a => a.Name).ToList()
                },
                task = new
                {
                    type = "string",
                    description = "Complete, self-contained task description for the specialist. " +
                                  "It starts with a fresh context and sees nothing else from this conversation."
                },
                context = new
                {
                    type = "string",
                    description = "Optional compact context references (IDs, constraints, prior findings) the specialist needs."
                }
            }
        };

        return new ToolDefinition
        {
            Name = toolName,
            Description = "Delegates a task to a specialist agent with its own tools and returns its result. " +
                          "Independent dispatches may be issued in parallel.",
            ParametersSchemaJson = JsonSerializer.Serialize(schema),
            IsReadOnly = allowParallelDispatch
        };
    }
}

/// <summary>
/// Executes <c>dispatch_agent</c> by running a nested orchestration turn with a fresh transcript.
/// The builder assigns <see cref="Orchestrator"/> after construction (dispatch and loop are mutually referential).
/// </summary>
public sealed class SubAgentDispatchToolExecutor : IToolExecutor
{
    private readonly Dictionary<string, SubAgentDefinition> _agents;
    private readonly SubAgentDispatchOptions _options;

    public SubAgentDispatchToolExecutor(IReadOnlyList<SubAgentDefinition> agents, SubAgentDispatchOptions options)
    {
        ArgumentNullException.ThrowIfNull(agents);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _agents = new Dictionary<string, SubAgentDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents)
        {
            if (!_agents.TryAdd(agent.Name, agent))
            {
                throw new ArgumentException($"Duplicate subagent name '{agent.Name}'.", nameof(agents));
            }
        }
    }

    /// <summary>Set by <see cref="LayeredChatHostBuilder"/> after the orchestrator is constructed.</summary>
    public LayeredChatOrchestrator? Orchestrator { get; set; }

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        string argumentsJson,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken = default)
    {
        if (Orchestrator is null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                FailureKind = ToolFailureKind.Fatal,
                SummaryText = "Subagent dispatch is not wired to an orchestrator."
            };
        }

        string? agentName = null;
        string? task = null;
        string? context = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("agent", out var a) && a.ValueKind == JsonValueKind.String)
            {
                agentName = a.GetString();
            }

            if (root.TryGetProperty("task", out var t) && t.ValueKind == JsonValueKind.String)
            {
                task = t.GetString();
            }

            if (root.TryGetProperty("context", out var c) && c.ValueKind == JsonValueKind.String)
            {
                context = c.GetString();
            }
        }
        catch (JsonException)
        {
            // fall through to the invalid-arguments failure below
        }

        if (string.IsNullOrWhiteSpace(agentName) || string.IsNullOrWhiteSpace(task))
        {
            return new ToolExecutionResult
            {
                Success = false,
                FailureKind = ToolFailureKind.Invalid,
                SummaryText = "dispatch_agent requires 'agent' and 'task' arguments."
            };
        }

        if (!_agents.TryGetValue(agentName, out var definition))
        {
            return new ToolExecutionResult
            {
                Success = false,
                FailureKind = ToolFailureKind.Invalid,
                SummaryText = $"Unknown agent '{agentName}'. Available agents: {string.Join(", ", _agents.Keys)}."
            };
        }

        var depth = 0;
        if (session.Properties.TryGetValue(SubAgentDispatchOptions.DepthPropertyKey, out var depthRaw))
        {
            _ = int.TryParse(depthRaw, out depth);
        }

        if (depth >= _options.MaxDispatchDepth)
        {
            return new ToolExecutionResult
            {
                Success = false,
                FailureKind = ToolFailureKind.Invalid,
                SummaryText = $"Dispatch depth limit ({_options.MaxDispatchDepth}) reached; solve this task directly."
            };
        }

        var childRequest = BuildChildRequest(definition, task, context, session, depth);

        LayeredChatTurnResult childResult;
        try
        {
            var streamCallback = _options.StreamChildEnvelope;
            if (streamCallback is not null)
            {
                var summaryBuilder = new System.Text.StringBuilder();
                int totalIn = 0, totalOut = 0;
                string? correlationId = null;

                await foreach (var envelope in Orchestrator.RunTurnStreamingAsync(childRequest, cancellationToken)
                                   .ConfigureAwait(false))
                {
                    await streamCallback(envelope, cancellationToken).ConfigureAwait(false);

                    if (envelope.Kind == OrchestrationStreamKind.TurnResultSummary &&
                        envelope.Attributes is not null)
                    {
                        if (envelope.Attributes.TryGetValue("assistantText", out var text))
                            summaryBuilder.Append(text);

                        if (envelope.Attributes.TryGetValue("totalInputTokens", out var tin) &&
                            int.TryParse(tin, out var itin))
                            totalIn = itin;

                        if (envelope.Attributes.TryGetValue("totalOutputTokens", out var tout) &&
                            int.TryParse(tout, out var itout))
                            totalOut = itout;
                    }

                    correlationId = envelope.CorrelationId;
                }

                childResult = new LayeredChatTurnResult
                {
                    RegistryKey = childRequest.OrchestrationRegistryKey,
                    CorrelationId = correlationId ?? string.Empty,
                    AssistantText = summaryBuilder.ToString(),
                    TotalInputTokens = totalIn,
                    TotalOutputTokens = totalOut,
                    AppendedMessages = []
                };
            }
            else
            {
                childResult = await Orchestrator.RunTurnAsync(childRequest, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult
            {
                Success = false,
                FailureKind = ToolFailureKind.Fatal,
                SummaryText = $"Agent '{definition.Name}' failed: {ex.Message}"
            };
        }

        var summary = childResult.AssistantText ?? string.Empty;
        if (summary.Length > _options.MaxResultChars)
        {
            summary = summary[.._options.MaxResultChars];
        }

        var payload = JsonSerializer.Serialize(new
        {
            agent = definition.Name,
            correlationId = childResult.CorrelationId,
            inputTokens = childResult.TotalInputTokens,
            outputTokens = childResult.TotalOutputTokens
        });

        return new ToolExecutionResult
        {
            Success = true,
            SummaryText = summary,
            StructuredPayloadJson = payload
        };
    }

    private LayeredChatTurnRequest BuildChildRequest(
        SubAgentDefinition definition,
        string task,
        string? context,
        OrchestrationSessionContext parentSession,
        int parentDepth)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in parentSession.Properties)
        {
            props[kv.Key] = kv.Value;
        }

        props[SubAgentDispatchOptions.DepthPropertyKey] = (parentDepth + 1).ToString();
        props[SubAgentDispatchOptions.AgentPropertyKey] = definition.Name;

        var childSession = new OrchestrationSessionContext
        {
            CorrelationId = parentSession.CorrelationId + "/" + definition.Name,
            TenantKey = parentSession.TenantKey,
            UserKey = parentSession.UserKey,
            SessionKey = parentSession.SessionKey,
            Locale = parentSession.Locale,
            Properties = props
        };

        var userContent = string.IsNullOrWhiteSpace(context)
            ? task
            : $"{task}\n\nContext references:\n{context}";

        var baseOptions = definition.ConnectorOptions;
        var connectorOptions = new LlmRequestOptions
        {
            Temperature = baseOptions?.Temperature ?? 0.2,
            MaxOutputTokens = baseOptions?.MaxOutputTokens,
            ModelNameOverride = definition.ModelNameOverride ?? baseOptions?.ModelNameOverride,
            AdapterProfile = baseOptions?.AdapterProfile,
            TelemetryVerbosity = baseOptions?.TelemetryVerbosity ?? OrchestrationTelemetryVerbosity.Normal,
            ResponseSchema = definition.ResultSchema ?? baseOptions?.ResponseSchema
        };

        return new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = definition.OrchestrationRegistryKey,
            SystemInstructionText = definition.SystemInstructionText,
            UserMessageContent = userContent,
            Session = childSession,
            ConnectorOptions = connectorOptions,
            Hooks = _options.Telemetry is null && _options.ChildToolLifecycle is null
                ? null
                : new OrchestrationExecutionHooks
                {
                    Telemetry = _options.Telemetry,
                    ToolLifecycle = _options.ChildToolLifecycle
                }
        };
    }
}
