using System.Text.Json.Serialization;

namespace LayeredChat;

/// <summary>
/// Wire DTO for version-host and pod-to-pod forwarding. Stable JSON for HTTP integration tests and container bridges.
/// </summary>
public sealed class OrchestrationForwardPayload
{
    public LayeredChatTurnRequestDto Request { get; init; } = new();

    public string RegistryKey { get; init; } = string.Empty;

    public OrchestrationProfileManifest? Manifest { get; init; }
}

/// <summary>
/// JSON-friendly copy of <see cref="LayeredChatTurnRequest"/> for forward contracts.
/// </summary>
public sealed class LayeredChatTurnRequestDto
{
    public string OrchestrationRegistryKey { get; init; } = string.Empty;

    public IReadOnlyList<ChatMessageDto> PriorMessages { get; init; } = [];

    public string UserMessageContent { get; init; } = string.Empty;

    public string SystemInstructionText { get; init; } = string.Empty;

    public OrchestrationSessionContextDto Session { get; init; } = new();

    public LlmRequestOptionsDto? ConnectorOptions { get; init; }
}

public sealed class ChatMessageDto
{
    public ChatRole Role { get; init; }

    public string Content { get; init; } = string.Empty;

    public string? ToolCallId { get; init; }

    public string? ToolName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IReadOnlyList<ToolCallRequestDto>? ToolCalls { get; init; }
}

public sealed class ToolCallRequestDto
{
    public string CallId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string ArgumentsJson { get; init; } = "{}";
}

public sealed class OrchestrationSessionContextDto
{
    public string CorrelationId { get; init; } = string.Empty;

    public string? TenantKey { get; init; }

    public string? UserKey { get; init; }

    public string? SessionKey { get; init; }

    public string Locale { get; init; } = "en";

    public string ActiveOrchestrationId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed class LlmRequestOptionsDto
{
    public double Temperature { get; init; } = 0.2;

    public int? MaxOutputTokens { get; init; }

    public int? MaxToolRoundIterations { get; init; }

    public string? ModelNameOverride { get; init; }

    public OrchestrationTelemetryVerbosity TelemetryVerbosity { get; init; } = OrchestrationTelemetryVerbosity.Normal;
}
