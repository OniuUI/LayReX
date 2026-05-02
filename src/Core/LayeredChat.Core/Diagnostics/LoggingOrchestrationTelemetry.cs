using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LayeredChat.Diagnostics;

/// <summary>
/// Emits every <see cref="OrchestrationStreamKind"/> to structured logs with size caps. Suitable for development and ops; do not log raw user content here.
/// </summary>
public sealed class LoggingOrchestrationTelemetry : IOrchestrationTelemetry
{
    private readonly ILogger _logger;
    private readonly int _maxCharsPerField;

    public LoggingOrchestrationTelemetry(ILogger logger, int maxCharsPerField = 1024)
    {
        _logger = logger;
        _maxCharsPerField = Math.Clamp(maxCharsPerField, 128, 8192);
    }

    /// <inheritdoc />
    public ValueTask EmitAsync(OrchestrationStreamEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var kind = envelope.Kind.ToString();
        switch (envelope.Kind)
        {
            case OrchestrationStreamKind.ToolExecutionStarted:
                _logger.LogInformation(
                    "LayeredChat {Kind} CorrelationId={CorrelationId} Tool={Tool} Seq={Seq}",
                    kind,
                    Trim(envelope.CorrelationId),
                    Trim(envelope.ToolName),
                    envelope.Sequence);
                break;
            case OrchestrationStreamKind.ToolExecutionFinished:
                var summaryLen = envelope.ToolResult?.SummaryText?.Length ?? 0;
                _logger.LogInformation(
                    "LayeredChat {Kind} CorrelationId={CorrelationId} Tool={Tool} Success={Success} SummaryLen={Len} Seq={Seq}",
                    kind,
                    Trim(envelope.CorrelationId),
                    Trim(envelope.ToolName),
                    envelope.ToolResult?.Success,
                    summaryLen,
                    envelope.Sequence);
                break;
            case OrchestrationStreamKind.TurnResultSummary:
                _logger.LogInformation(
                    "LayeredChat {Kind} CorrelationId={CorrelationId} InTok={In} OutTok={Out} Attrs={Attrs}",
                    kind,
                    Trim(envelope.CorrelationId),
                    envelope.InputTokens,
                    envelope.OutputTokens,
                    Trim(SerializeAttributes(envelope.Attributes)));
                break;
            case OrchestrationStreamKind.CompletionEvaluationFinished:
                _logger.LogInformation(
                    "LayeredChat {Kind} CorrelationId={CorrelationId} Attrs={Attrs} Seq={Seq}",
                    kind,
                    Trim(envelope.CorrelationId),
                    Trim(SerializeAttributes(envelope.Attributes)),
                    envelope.Sequence);
                break;
            case OrchestrationStreamKind.UsageUpdate:
                _logger.LogDebug(
                    "LayeredChat {Kind} CorrelationId={CorrelationId} InTok={In} OutTok={Out}",
                    kind,
                    Trim(envelope.CorrelationId),
                    envelope.InputTokens,
                    envelope.OutputTokens);
                break;
            case OrchestrationStreamKind.Error:
                _logger.LogWarning(
                    "LayeredChat {Kind} CorrelationId={CorrelationId} Message={Message}",
                    kind,
                    Trim(envelope.CorrelationId),
                    Trim(envelope.ErrorMessage));
                break;
            default:
                _logger.LogInformation(
                    "LayeredChat {Kind} CorrelationId={CorrelationId} Seq={Seq}",
                    kind,
                    Trim(envelope.CorrelationId),
                    envelope.Sequence);
                break;
        }

        return ValueTask.CompletedTask;
    }

    private string Trim(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        var t = value.Trim();
        if (t.Length <= _maxCharsPerField)
            return t;
        return t[.._maxCharsPerField] + "…";
    }

    private string SerializeAttributes(IReadOnlyDictionary<string, string>? attributes)
    {
        if (attributes is not { Count: > 0 })
            return "{}";
        try
        {
            var json = JsonSerializer.Serialize(attributes);
            return json.Length <= _maxCharsPerField ? json : json[.._maxCharsPerField] + "…";
        }
        catch
        {
            return "{}";
        }
    }
}
