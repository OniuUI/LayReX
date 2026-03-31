using System.Runtime.CompilerServices;
using System.Text;

namespace LayeredChat;

/// <summary>
/// Default tool loop: merges data-source slices, optional HTTP forwarding to version pods, telemetry, and streaming or buffered model calls.
/// </summary>
public sealed class LayeredChatOrchestrator
{
    private readonly ILlmChatConnector _connector;
    private readonly IToolExecutor _toolExecutor;
    private readonly IToolCatalog _toolCatalog;
    private readonly IOrchestrationDefinitionRegistry _orchestrationRegistry;
    private readonly IDataSourceRegistry _dataSourceRegistry;

    public LayeredChatOrchestrator(
        ILlmChatConnector connector,
        IToolExecutor toolExecutor,
        IToolCatalog toolCatalog,
        IOrchestrationDefinitionRegistry orchestrationRegistry,
        IDataSourceRegistry dataSourceRegistry)
    {
        _connector = connector;
        _toolExecutor = toolExecutor;
        _toolCatalog = toolCatalog;
        _orchestrationRegistry = orchestrationRegistry;
        _dataSourceRegistry = dataSourceRegistry;
    }

    public async Task<LayeredChatTurnResult> RunTurnAsync(
        LayeredChatTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var telemetry = request.Hooks?.Telemetry;
        var prep = await PrepareTurnAsync(request, cancellationToken).ConfigureAwait(false);
        var manifest = prep.Manifest;
        var session = prep.Session;
        long seq = 0;
        var detailed = TelemetryIsDetailed(request);

        if (detailed)
        {
            await EmitAsync(telemetry, new OrchestrationStreamEnvelope
            {
                Kind = OrchestrationStreamKind.TurnStarted,
                Sequence = ++seq,
                CorrelationId = session.CorrelationId,
                RegistryKey = request.OrchestrationRegistryKey,
                OrchestrationId = manifest.OrchestrationId,
                SemanticVersion = manifest.SemanticVersion
            }, cancellationToken).ConfigureAwait(false);
        }

        if (ShouldAttemptForward(request, manifest))
        {
            await EmitForwardingStartAsync(telemetry, request, manifest, ++seq, cancellationToken).ConfigureAwait(false);
        }

        var forwarded = await ExecuteForwardCoreAsync(request, manifest, cancellationToken).ConfigureAwait(false);
        if (forwarded is not null)
        {
            await EmitAsync(telemetry, new OrchestrationStreamEnvelope
            {
                Kind = OrchestrationStreamKind.ExternalForwardCompleted,
                Sequence = ++seq,
                CorrelationId = session.CorrelationId,
                RegistryKey = request.OrchestrationRegistryKey,
                OrchestrationId = manifest.OrchestrationId,
                SemanticVersion = manifest.SemanticVersion,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["via"] = "externalForward"
                }
            }, cancellationToken).ConfigureAwait(false);

            var ensured = EnsureCorrelation(forwarded, session, manifest);
            await EmitTurnSummaryAsync(telemetry, session, request, manifest, ensured, ++seq, cancellationToken)
                .ConfigureAwait(false);
            await EmitAsync(telemetry, new OrchestrationStreamEnvelope
            {
                Kind = OrchestrationStreamKind.TurnCompleted,
                Sequence = ++seq,
                CorrelationId = session.CorrelationId,
                RegistryKey = request.OrchestrationRegistryKey,
                OrchestrationId = manifest.OrchestrationId,
                SemanticVersion = manifest.SemanticVersion
            }, cancellationToken).ConfigureAwait(false);

            return ensured;
        }

        await EmitContextSlicesAsync(telemetry, request, prep, detailed, ++seq, cancellationToken).ConfigureAwait(false);

        var appended = new List<ChatMessage>();
        var totalIn = 0;
        var totalOut = 0;
        var maxIterations = request.ConnectorOptions?.MaxToolRoundIterations ?? manifest.MaxToolIterations;
        if (maxIterations < 1)
        {
            maxIterations = 1;
        }

        string? lastAssistantText = null;

        for (var i = 0; i < maxIterations; i++)
        {
            var options = BuildConnectorOptions(request, manifest);
            var (roundTools, roundAllowed) = ResolveToolsForModelRound(i, prep, request);
            if (detailed)
            {
                await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                {
                    Kind = OrchestrationStreamKind.ModelRoundStarted,
                    Sequence = ++seq,
                    CorrelationId = session.CorrelationId,
                    RegistryKey = request.OrchestrationRegistryKey,
                    OrchestrationId = manifest.OrchestrationId,
                    SemanticVersion = manifest.SemanticVersion,
                    Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["round"] = i.ToString()
                    }
                }, cancellationToken).ConfigureAwait(false);
            }

            var completion = await _connector
                .CompleteAsync(prep.Working, roundTools, options, cancellationToken)
                .ConfigureAwait(false);

            totalIn += completion.InputTokens;
            totalOut += completion.OutputTokens;

            if (detailed)
            {
                await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                {
                    Kind = OrchestrationStreamKind.UsageUpdate,
                    Sequence = ++seq,
                    CorrelationId = session.CorrelationId,
                    RegistryKey = request.OrchestrationRegistryKey,
                    InputTokens = completion.InputTokens,
                    OutputTokens = completion.OutputTokens
                }, cancellationToken).ConfigureAwait(false);

                await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                {
                    Kind = OrchestrationStreamKind.ModelRoundCompleted,
                    Sequence = ++seq,
                    CorrelationId = session.CorrelationId,
                    RegistryKey = request.OrchestrationRegistryKey,
                    OrchestrationId = manifest.OrchestrationId,
                    SemanticVersion = manifest.SemanticVersion,
                    Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["round"] = i.ToString(),
                        ["cumulativeInputTokens"] = totalIn.ToString(),
                        ["cumulativeOutputTokens"] = totalOut.ToString()
                    }
                }, cancellationToken).ConfigureAwait(false);
            }

            if (completion.ToolCalls.Count > 0)
            {
                var assistantWithTools = new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = completion.TextContent ?? string.Empty,
                    ToolCalls = completion.ToolCalls
                };

                prep.Working.Add(assistantWithTools);
                appended.Add(assistantWithTools);
                lastAssistantText = completion.TextContent;

                if (detailed)
                {
                    await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                    {
                        Kind = OrchestrationStreamKind.AssistantMessageCommitted,
                        Sequence = ++seq,
                        CorrelationId = session.CorrelationId,
                        RegistryKey = request.OrchestrationRegistryKey,
                        AppendedMessage = assistantWithTools
                    }, cancellationToken).ConfigureAwait(false);
                }

                foreach (var call in completion.ToolCalls)
                {
                    if (detailed)
                    {
                        await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                        {
                            Kind = OrchestrationStreamKind.ToolCallFinished,
                            Sequence = ++seq,
                            CorrelationId = session.CorrelationId,
                            RegistryKey = request.OrchestrationRegistryKey,
                            ToolCall = call,
                            ToolName = call.Name
                        }, cancellationToken).ConfigureAwait(false);
                    }

                    await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                    {
                        Kind = OrchestrationStreamKind.ToolExecutionStarted,
                        Sequence = ++seq,
                        CorrelationId = session.CorrelationId,
                        RegistryKey = request.OrchestrationRegistryKey,
                        ToolName = call.Name
                    }, cancellationToken).ConfigureAwait(false);

                    var exec = await ExecuteToolAsync(call, roundAllowed, request.OrchestrationRegistryKey, session, cancellationToken)
                        .ConfigureAwait(false);

                    await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                    {
                        Kind = OrchestrationStreamKind.ToolExecutionFinished,
                        Sequence = ++seq,
                        CorrelationId = session.CorrelationId,
                        RegistryKey = request.OrchestrationRegistryKey,
                        ToolName = call.Name,
                        ToolResult = exec
                    }, cancellationToken).ConfigureAwait(false);

                    var toolBody = exec.StructuredPayloadJson is { Length: > 0 }
                        ? $"{exec.SummaryText}\n\n{exec.StructuredPayloadJson}"
                        : exec.SummaryText;

                    var toolMessage = new ChatMessage
                    {
                        Role = ChatRole.Tool,
                        ToolCallId = call.CallId,
                        ToolName = call.Name,
                        Content = toolBody
                    };

                    prep.Working.Add(toolMessage);
                    appended.Add(toolMessage);
                }

                continue;
            }

            var assistantFinal = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = completion.TextContent ?? string.Empty
            };

            prep.Working.Add(assistantFinal);
            appended.Add(assistantFinal);
            lastAssistantText = completion.TextContent;

            if (detailed)
            {
                await EmitAsync(telemetry, new OrchestrationStreamEnvelope
                {
                    Kind = OrchestrationStreamKind.AssistantMessageCommitted,
                    Sequence = ++seq,
                    CorrelationId = session.CorrelationId,
                    RegistryKey = request.OrchestrationRegistryKey,
                    AppendedMessage = assistantFinal
                }, cancellationToken).ConfigureAwait(false);
            }

            break;
        }

        var result = BuildResult(request.OrchestrationRegistryKey, session, manifest, lastAssistantText, appended, totalIn, totalOut);

        await EmitTurnSummaryAsync(telemetry, session, request, manifest, result, ++seq, cancellationToken)
            .ConfigureAwait(false);
        await EmitAsync(telemetry, new OrchestrationStreamEnvelope
        {
            Kind = OrchestrationStreamKind.TurnCompleted,
            Sequence = ++seq,
            CorrelationId = session.CorrelationId,
            RegistryKey = request.OrchestrationRegistryKey,
            OrchestrationId = manifest.OrchestrationId,
            SemanticVersion = manifest.SemanticVersion
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    public async IAsyncEnumerable<OrchestrationStreamEnvelope> RunTurnStreamingAsync(
        LayeredChatTurnRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var telemetry = request.Hooks?.Telemetry;
        var prep = await PrepareTurnAsync(request, cancellationToken).ConfigureAwait(false);
        var manifest = prep.Manifest;
        var session = prep.Session;
        long seq = 0;
        var detailed = TelemetryIsDetailed(request);

        if (detailed)
        {
            yield return Envelope(++seq, OrchestrationStreamKind.TurnStarted, session, request, manifest, null);
        }

        if (ShouldAttemptForward(request, manifest))
        {
            yield return Envelope(++seq, OrchestrationStreamKind.ForwardingToExternal, session, request, manifest,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["endpoint"] = manifest.ExternalForwardUri ?? string.Empty
                });
        }

        var forwarded = await ExecuteForwardCoreAsync(request, manifest, cancellationToken).ConfigureAwait(false);
        if (forwarded is not null)
        {
            var ensured = EnsureCorrelation(forwarded, session, manifest);
            yield return Envelope(++seq, OrchestrationStreamKind.ExternalForwardCompleted, session, request, manifest,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["via"] = "externalForward" });
            yield return SummaryEnvelope(++seq, session, request, manifest, ensured);
            yield return Envelope(++seq, OrchestrationStreamKind.TurnCompleted, session, request, manifest, null);
            yield break;
        }

        if (detailed)
        {
            yield return new OrchestrationStreamEnvelope
            {
                Kind = OrchestrationStreamKind.ContextSlicesReady,
                Sequence = ++seq,
                CorrelationId = session.CorrelationId,
                RegistryKey = request.OrchestrationRegistryKey,
                OrchestrationId = manifest.OrchestrationId,
                SemanticVersion = manifest.SemanticVersion,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sliceCount"] = prep.Slices.Count.ToString()
                }
            };
        }

        var appended = new List<ChatMessage>();
        var totalIn = 0;
        var totalOut = 0;
        var maxIterations = request.ConnectorOptions?.MaxToolRoundIterations ?? manifest.MaxToolIterations;
        if (maxIterations < 1)
        {
            maxIterations = 1;
        }

        string? lastAssistantText = null;

        for (var round = 0; round < maxIterations; round++)
        {
            var options = BuildConnectorOptions(request, manifest);
            var (roundTools, roundAllowed) = ResolveToolsForModelRound(round, prep, request);
            if (detailed)
            {
                yield return Envelope(++seq, OrchestrationStreamKind.ModelRoundStarted, session, request, manifest,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["round"] = round.ToString() });
            }

            LlmCompletionResult completion;
            if (_connector is IStreamingLlmChatConnector streaming &&
                (options.AdapterProfile?.SupportsStreaming ?? true))
            {
                var acc = new LlmStreamAccumulatorSession();
                await foreach (var frame in streaming
                                   .CompleteStreamingAsync(prep.Working, roundTools, options, cancellationToken)
                                   .ConfigureAwait(false))
                {
                    acc.Accept(frame);
                    if (frame.Kind == LlmStreamFrameKind.TextDelta && !string.IsNullOrEmpty(frame.TextDelta))
                    {
                        yield return new OrchestrationStreamEnvelope
                        {
                            Kind = OrchestrationStreamKind.AssistantTextDelta,
                            Sequence = ++seq,
                            CorrelationId = session.CorrelationId,
                            RegistryKey = request.OrchestrationRegistryKey,
                            OrchestrationId = manifest.OrchestrationId,
                            SemanticVersion = manifest.SemanticVersion,
                            TextDelta = frame.TextDelta
                        };
                    }

                    if (detailed && frame.Kind == LlmStreamFrameKind.Usage)
                    {
                        yield return new OrchestrationStreamEnvelope
                        {
                            Kind = OrchestrationStreamKind.UsageUpdate,
                            Sequence = ++seq,
                            CorrelationId = session.CorrelationId,
                            RegistryKey = request.OrchestrationRegistryKey,
                            InputTokens = frame.InputTokens,
                            OutputTokens = frame.OutputTokens
                        };
                    }
                }

                completion = acc.Build();
            }
            else
            {
                completion = await _connector
                    .CompleteAsync(prep.Working, roundTools, options, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(completion.TextContent))
                {
                    yield return new OrchestrationStreamEnvelope
                    {
                        Kind = OrchestrationStreamKind.AssistantTextDelta,
                        Sequence = ++seq,
                        CorrelationId = session.CorrelationId,
                        RegistryKey = request.OrchestrationRegistryKey,
                        OrchestrationId = manifest.OrchestrationId,
                        SemanticVersion = manifest.SemanticVersion,
                        TextDelta = completion.TextContent
                    };
                }
            }

            totalIn += completion.InputTokens;
            totalOut += completion.OutputTokens;

            if (detailed)
            {
                yield return new OrchestrationStreamEnvelope
                {
                    Kind = OrchestrationStreamKind.UsageUpdate,
                    Sequence = ++seq,
                    CorrelationId = session.CorrelationId,
                    RegistryKey = request.OrchestrationRegistryKey,
                    InputTokens = completion.InputTokens,
                    OutputTokens = completion.OutputTokens
                };

                yield return new OrchestrationStreamEnvelope
                {
                    Kind = OrchestrationStreamKind.ModelRoundCompleted,
                    Sequence = ++seq,
                    CorrelationId = session.CorrelationId,
                    RegistryKey = request.OrchestrationRegistryKey,
                    OrchestrationId = manifest.OrchestrationId,
                    SemanticVersion = manifest.SemanticVersion,
                    Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["round"] = round.ToString(),
                        ["cumulativeInputTokens"] = totalIn.ToString(),
                        ["cumulativeOutputTokens"] = totalOut.ToString()
                    }
                };
            }

            if (completion.ToolCalls.Count > 0)
            {
                var assistantWithTools = new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = completion.TextContent ?? string.Empty,
                    ToolCalls = completion.ToolCalls
                };

                prep.Working.Add(assistantWithTools);
                appended.Add(assistantWithTools);
                lastAssistantText = completion.TextContent;

                if (detailed)
                {
                    yield return new OrchestrationStreamEnvelope
                    {
                        Kind = OrchestrationStreamKind.AssistantMessageCommitted,
                        Sequence = ++seq,
                        CorrelationId = session.CorrelationId,
                        RegistryKey = request.OrchestrationRegistryKey,
                        AppendedMessage = assistantWithTools
                    };
                }

                foreach (var call in completion.ToolCalls)
                {
                    if (detailed)
                    {
                        yield return new OrchestrationStreamEnvelope
                        {
                            Kind = OrchestrationStreamKind.ToolCallFinished,
                            Sequence = ++seq,
                            CorrelationId = session.CorrelationId,
                            RegistryKey = request.OrchestrationRegistryKey,
                            ToolCall = call,
                            ToolName = call.Name
                        };
                    }

                    yield return new OrchestrationStreamEnvelope
                    {
                        Kind = OrchestrationStreamKind.ToolExecutionStarted,
                        Sequence = ++seq,
                        CorrelationId = session.CorrelationId,
                        RegistryKey = request.OrchestrationRegistryKey,
                        ToolName = call.Name
                    };

                    var exec = await ExecuteToolAsync(call, roundAllowed, request.OrchestrationRegistryKey, session, cancellationToken)
                        .ConfigureAwait(false);

                    yield return new OrchestrationStreamEnvelope
                    {
                        Kind = OrchestrationStreamKind.ToolExecutionFinished,
                        Sequence = ++seq,
                        CorrelationId = session.CorrelationId,
                        RegistryKey = request.OrchestrationRegistryKey,
                        ToolName = call.Name,
                        ToolResult = exec
                    };

                    var toolBody = exec.StructuredPayloadJson is { Length: > 0 }
                        ? $"{exec.SummaryText}\n\n{exec.StructuredPayloadJson}"
                        : exec.SummaryText;

                    var toolMessage = new ChatMessage
                    {
                        Role = ChatRole.Tool,
                        ToolCallId = call.CallId,
                        ToolName = call.Name,
                        Content = toolBody
                    };

                    prep.Working.Add(toolMessage);
                    appended.Add(toolMessage);
                }

                continue;
            }

            var assistantFinal = new ChatMessage
            {
                Role = ChatRole.Assistant,
                Content = completion.TextContent ?? string.Empty
            };

            prep.Working.Add(assistantFinal);
            appended.Add(assistantFinal);
            lastAssistantText = completion.TextContent;

            if (detailed)
            {
                yield return new OrchestrationStreamEnvelope
                {
                    Kind = OrchestrationStreamKind.AssistantMessageCommitted,
                    Sequence = ++seq,
                    CorrelationId = session.CorrelationId,
                    RegistryKey = request.OrchestrationRegistryKey,
                    AppendedMessage = assistantFinal
                };
            }

            break;
        }

        var result = BuildResult(request.OrchestrationRegistryKey, session, manifest, lastAssistantText, appended, totalIn, totalOut);
        yield return SummaryEnvelope(++seq, session, request, manifest, result);
        yield return Envelope(++seq, OrchestrationStreamKind.TurnCompleted, session, request, manifest, null);
    }

    private static OrchestrationStreamEnvelope SummaryEnvelope(
        long sequence,
        OrchestrationSessionContext session,
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest,
        LayeredChatTurnResult result)
    {
        return new OrchestrationStreamEnvelope
        {
            Kind = OrchestrationStreamKind.TurnResultSummary,
            Sequence = sequence,
            CorrelationId = session.CorrelationId,
            RegistryKey = request.OrchestrationRegistryKey,
            OrchestrationId = manifest.OrchestrationId,
            SemanticVersion = manifest.SemanticVersion,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["assistantText"] = result.AssistantText ?? string.Empty,
                ["totalInputTokens"] = result.TotalInputTokens.ToString(),
                ["totalOutputTokens"] = result.TotalOutputTokens.ToString(),
                ["appendedCount"] = result.AppendedMessages.Count.ToString()
            }
        };
    }

    private static OrchestrationStreamEnvelope Envelope(
        long sequence,
        OrchestrationStreamKind kind,
        OrchestrationSessionContext session,
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest,
        IReadOnlyDictionary<string, string>? attributes)
    {
        return new OrchestrationStreamEnvelope
        {
            Kind = kind,
            Sequence = sequence,
            CorrelationId = session.CorrelationId,
            RegistryKey = request.OrchestrationRegistryKey,
            OrchestrationId = manifest.OrchestrationId,
            SemanticVersion = manifest.SemanticVersion,
            Attributes = attributes
        };
    }

    private static bool TelemetryIsDetailed(LayeredChatTurnRequest request) =>
        request.ConnectorOptions?.TelemetryVerbosity != OrchestrationTelemetryVerbosity.Minimal;

    private async Task EmitContextSlicesAsync(
        IOrchestrationTelemetry? telemetry,
        LayeredChatTurnRequest request,
        TurnPreparation prep,
        bool detailed,
        long sequence,
        CancellationToken cancellationToken)
    {
        if (!detailed)
        {
            return;
        }

        await EmitAsync(telemetry, new OrchestrationStreamEnvelope
        {
            Kind = OrchestrationStreamKind.ContextSlicesReady,
            Sequence = sequence,
            CorrelationId = prep.Session.CorrelationId,
            RegistryKey = request.OrchestrationRegistryKey,
            OrchestrationId = prep.Manifest.OrchestrationId,
            SemanticVersion = prep.Manifest.SemanticVersion,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["sliceCount"] = prep.Slices.Count.ToString()
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static bool ShouldAttemptForward(LayeredChatTurnRequest request, OrchestrationProfileManifest manifest)
    {
        return !string.IsNullOrWhiteSpace(manifest.ExternalForwardUri) &&
               request.Hooks?.HttpForwarder is not null &&
               Uri.TryCreate(manifest.ExternalForwardUri, UriKind.Absolute, out _);
    }

    private async Task EmitTurnSummaryAsync(
        IOrchestrationTelemetry? telemetry,
        OrchestrationSessionContext session,
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest,
        LayeredChatTurnResult result,
        long sequence,
        CancellationToken cancellationToken)
    {
        await EmitAsync(telemetry, SummaryEnvelope(sequence, session, request, manifest, result), cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task EmitForwardingStartAsync(
        IOrchestrationTelemetry? telemetry,
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest,
        long sequence,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(manifest.ExternalForwardUri, UriKind.Absolute, out var endpoint))
        {
            return;
        }

        await EmitAsync(telemetry, new OrchestrationStreamEnvelope
        {
            Kind = OrchestrationStreamKind.ForwardingToExternal,
            Sequence = sequence,
            CorrelationId = request.Session.CorrelationId,
            RegistryKey = request.OrchestrationRegistryKey,
            OrchestrationId = manifest.OrchestrationId,
            SemanticVersion = manifest.SemanticVersion,
            Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["endpoint"] = endpoint.ToString()
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<LayeredChatTurnResult?> ExecuteForwardCoreAsync(
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest,
        CancellationToken cancellationToken)
    {
        if (!ShouldAttemptForward(request, manifest) ||
            request.Hooks?.HttpForwarder is not { } forwarder ||
            !Uri.TryCreate(manifest.ExternalForwardUri, UriKind.Absolute, out var endpoint))
        {
            return null;
        }

        var timeout = TimeSpan.FromSeconds(Math.Max(1, manifest.ExternalForwardTimeoutSeconds));
        return await forwarder
            .TryForwardTurnAsync(request, manifest, endpoint, timeout, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves tool definitions and the allow-set for tool execution for one model round.
    /// </summary>
    private (IReadOnlyList<ToolDefinition> Tools, HashSet<string> AllowedSet) ResolveToolsForModelRound(
        int roundIndex,
        TurnPreparation prep,
        LayeredChatTurnRequest request)
    {
        var manifest = prep.Manifest;
        IReadOnlyList<string> names = manifest.AllowedToolNames;
        var provider = request.Hooks?.ToolRoundCatalogProvider;
        if (provider is not null)
        {
            var custom = provider.GetActiveToolNamesForRound(roundIndex, prep.Working, manifest);
            if (custom is { Count: > 0 })
            {
                var allowedSuperset = new HashSet<string>(manifest.AllowedToolNames, StringComparer.Ordinal);
                foreach (var n in custom)
                {
                    if (!allowedSuperset.Contains(n))
                    {
                        throw new InvalidOperationException(
                            $"Tool round catalog returned '{n}' which is not in the manifest allow-list for orchestration '{manifest.OrchestrationId}'.");
                    }
                }

                names = custom;
            }
        }

        var tools = _toolCatalog.ResolveAllowed(names);
        return (tools, new HashSet<string>(names, StringComparer.Ordinal));
    }

    private async Task<TurnPreparation> PrepareTurnAsync(LayeredChatTurnRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrchestrationRegistryKey))
        {
            throw new ArgumentException("Orchestration registry key is required.", nameof(request));
        }

        if (!_orchestrationRegistry.TryGet(request.OrchestrationRegistryKey, out var definition) || definition is null)
        {
            throw new InvalidOperationException(
                $"Unknown orchestration registry key '{request.OrchestrationRegistryKey}'.");
        }

        var manifest = definition.Manifest;
        var session = SessionForManifest(request.Session, manifest);
        var slices = await CollectSlicesAsync(manifest, session, cancellationToken).ConfigureAwait(false);
        var mergedSystem = MergeSystemInstruction(request.SystemInstructionText, slices);
        var allowedSet = new HashSet<string>(manifest.AllowedToolNames, StringComparer.Ordinal);
        var tools = _toolCatalog.ResolveAllowed(manifest.AllowedToolNames);

        var working = new List<ChatMessage>
        {
            new()
            {
                Role = ChatRole.System,
                Content = mergedSystem
            }
        };

        working.AddRange(request.PriorMessages);
        working.Add(new ChatMessage
        {
            Role = ChatRole.User,
            Content = request.UserMessageContent
        });

        return new TurnPreparation(definition, manifest, session, mergedSystem, working, tools, allowedSet, slices);
    }

    private sealed record TurnPreparation(
        OrchestrationDefinition Definition,
        OrchestrationProfileManifest Manifest,
        OrchestrationSessionContext Session,
        string MergedSystem,
        List<ChatMessage> Working,
        IReadOnlyList<ToolDefinition> Tools,
        HashSet<string> AllowedSet,
        IReadOnlyList<ContextSlice> Slices);

    private async Task<ToolExecutionResult> ExecuteToolAsync(
        ToolCallRequest call,
        HashSet<string> allowedSet,
        string registryKey,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken)
    {
        if (!allowedSet.Contains(call.Name))
        {
            return new ToolExecutionResult
            {
                Success = false,
                SummaryText = $"Tool '{call.Name}' is not allowed for orchestration '{registryKey}'."
            };
        }

        return await _toolExecutor
            .ExecuteAsync(call.Name, call.ArgumentsJson, session, cancellationToken)
            .ConfigureAwait(false);
    }

    private static LlmRequestOptions BuildConnectorOptions(
        LayeredChatTurnRequest request,
        OrchestrationProfileManifest manifest)
    {
        var c = request.ConnectorOptions;
        var adapter = MergeAdapterProfile(manifest, request);
        return new LlmRequestOptions
        {
            Temperature = c?.Temperature ?? manifest.DefaultTemperature,
            MaxOutputTokens = c?.MaxOutputTokens ?? adapter?.DefaultMaxOutputTokens,
            MaxToolRoundIterations = c?.MaxToolRoundIterations,
            ModelNameOverride = c?.ModelNameOverride,
            AdapterProfile = adapter,
            TelemetryVerbosity = c?.TelemetryVerbosity ?? OrchestrationTelemetryVerbosity.Normal
        };
    }

    private static LlmModelAdapterProfile? MergeAdapterProfile(
        OrchestrationProfileManifest manifest,
        LayeredChatTurnRequest request)
    {
        var r = request.ModelAdapterProfile;
        if (r is null && string.IsNullOrEmpty(manifest.LlmAdapterProfileId))
        {
            return null;
        }

        return new LlmModelAdapterProfile
        {
            ProfileId = manifest.LlmAdapterProfileId ?? r?.ProfileId ?? "default",
            SupportsStreaming = r?.SupportsStreaming ?? true,
            SupportsParallelToolCalls = r?.SupportsParallelToolCalls ?? true,
            DefaultMaxOutputTokens = r?.DefaultMaxOutputTokens,
            ReasoningEffortHint = r?.ReasoningEffortHint
        };
    }

    private static LayeredChatTurnResult BuildResult(
        string registryKey,
        OrchestrationSessionContext session,
        OrchestrationProfileManifest manifest,
        string? assistantText,
        List<ChatMessage> appended,
        int totalIn,
        int totalOut)
    {
        return new LayeredChatTurnResult
        {
            RegistryKey = registryKey,
            CorrelationId = session.CorrelationId,
            OrchestrationId = manifest.OrchestrationId,
            SemanticVersion = manifest.SemanticVersion,
            AssistantText = assistantText,
            AppendedMessages = appended,
            TotalInputTokens = totalIn,
            TotalOutputTokens = totalOut
        };
    }

    private static LayeredChatTurnResult EnsureCorrelation(
        LayeredChatTurnResult forwarded,
        OrchestrationSessionContext session,
        OrchestrationProfileManifest manifest)
    {
        return new LayeredChatTurnResult
        {
            RegistryKey = forwarded.RegistryKey,
            CorrelationId = string.IsNullOrEmpty(forwarded.CorrelationId) ? session.CorrelationId : forwarded.CorrelationId,
            OrchestrationId = forwarded.OrchestrationId ?? manifest.OrchestrationId,
            SemanticVersion = forwarded.SemanticVersion ?? manifest.SemanticVersion,
            AssistantText = forwarded.AssistantText,
            AppendedMessages = forwarded.AppendedMessages,
            TotalInputTokens = forwarded.TotalInputTokens,
            TotalOutputTokens = forwarded.TotalOutputTokens
        };
    }

    private static async Task EmitAsync(
        IOrchestrationTelemetry? telemetry,
        OrchestrationStreamEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (telemetry is null)
        {
            return;
        }

        await telemetry.EmitAsync(envelope, cancellationToken).ConfigureAwait(false);
    }

    private static OrchestrationSessionContext SessionForManifest(
        OrchestrationSessionContext session,
        OrchestrationProfileManifest manifest)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in session.Properties)
        {
            props[kv.Key] = kv.Value;
        }

        return new OrchestrationSessionContext
        {
            CorrelationId = session.CorrelationId,
            TenantKey = session.TenantKey,
            UserKey = session.UserKey,
            SessionKey = session.SessionKey,
            Locale = session.Locale,
            ActiveOrchestrationId = manifest.OrchestrationId,
            Properties = props
        };
    }

    private async Task<IReadOnlyList<ContextSlice>> CollectSlicesAsync(
        OrchestrationProfileManifest manifest,
        OrchestrationSessionContext session,
        CancellationToken cancellationToken)
    {
        var list = new List<ContextSlice>();
        foreach (var id in manifest.DataSourceIdsInOrder)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!_dataSourceRegistry.TryGet(id, out var provider) || provider is null)
            {
                list.Add(new ContextSlice
                {
                    Label = $"Missing data source: {id}",
                    Text = $"Data source '{id}' is listed in the manifest but not registered."
                });
                continue;
            }

            var slice = await provider
                .GetSliceAsync(session, manifest.Parameters, cancellationToken)
                .ConfigureAwait(false);

            list.Add(slice);
        }

        return list;
    }

    private static string MergeSystemInstruction(string baseInstruction, IReadOnlyList<ContextSlice> slices)
    {
        if (slices.Count == 0)
        {
            return baseInstruction;
        }

        var sb = new StringBuilder(baseInstruction);
        if (!string.IsNullOrEmpty(baseInstruction))
        {
            sb.AppendLine();
            sb.AppendLine();
        }

        foreach (var slice in slices)
        {
            if (!string.IsNullOrWhiteSpace(slice.Label))
            {
                sb.Append("## ");
                sb.AppendLine(slice.Label);
            }

            sb.AppendLine(slice.Text);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
