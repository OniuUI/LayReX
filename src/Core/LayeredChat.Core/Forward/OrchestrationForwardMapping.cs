namespace LayeredChat;

/// <summary>
/// Maps between domain requests and forward DTOs.
/// </summary>
public static class OrchestrationForwardMapping
{
    public static LayeredChatTurnRequestDto ToDto(LayeredChatTurnRequest request)
    {
        return new LayeredChatTurnRequestDto
        {
            OrchestrationRegistryKey = request.OrchestrationRegistryKey,
            PriorMessages = request.PriorMessages.Select(ToChatMessageDto).ToList(),
            UserMessageContent = request.UserMessageContent,
            SystemInstructionText = request.SystemInstructionText,
            Session = ToDto(request.Session),
            ConnectorOptions = request.ConnectorOptions is null
                ? null
                : new LlmRequestOptionsDto
                {
                    Temperature = request.ConnectorOptions.Temperature,
                    MaxOutputTokens = request.ConnectorOptions.MaxOutputTokens,
                    MaxToolRoundIterations = request.ConnectorOptions.MaxToolRoundIterations,
                    ModelNameOverride = request.ConnectorOptions.ModelNameOverride,
                    TelemetryVerbosity = request.ConnectorOptions.TelemetryVerbosity
                }
        };
    }

    public static LayeredChatTurnRequest FromDto(LayeredChatTurnRequestDto dto)
    {
        return new LayeredChatTurnRequest
        {
            OrchestrationRegistryKey = dto.OrchestrationRegistryKey,
            PriorMessages = dto.PriorMessages.Select(FromChatMessageDto).ToList(),
            UserMessageContent = dto.UserMessageContent,
            SystemInstructionText = dto.SystemInstructionText,
            Session = FromDto(dto.Session),
            ConnectorOptions = dto.ConnectorOptions is null
                ? null
                : new LlmRequestOptions
                {
                    Temperature = dto.ConnectorOptions.Temperature,
                    MaxOutputTokens = dto.ConnectorOptions.MaxOutputTokens,
                    MaxToolRoundIterations = dto.ConnectorOptions.MaxToolRoundIterations,
                    ModelNameOverride = dto.ConnectorOptions.ModelNameOverride,
                    TelemetryVerbosity = dto.ConnectorOptions.TelemetryVerbosity
                }
        };
    }

    public static ChatMessageDto ToChatMessageDto(ChatMessage m)
    {
        return new ChatMessageDto
        {
            Role = m.Role,
            Content = m.Content,
            ToolCallId = m.ToolCallId,
            ToolName = m.ToolName,
            ToolCalls = m.ToolCalls?.Select(tc => new ToolCallRequestDto
            {
                CallId = tc.CallId,
                Name = tc.Name,
                ArgumentsJson = tc.ArgumentsJson
            }).ToList()
        };
    }

    public static ChatMessage FromChatMessageDto(ChatMessageDto dto)
    {
        return new ChatMessage
        {
            Role = dto.Role,
            Content = dto.Content,
            ToolCallId = dto.ToolCallId,
            ToolName = dto.ToolName,
            ToolCalls = dto.ToolCalls?.Select(tc => new ToolCallRequest
            {
                CallId = tc.CallId,
                Name = tc.Name,
                ArgumentsJson = tc.ArgumentsJson
            }).ToList()
        };
    }

    private static OrchestrationSessionContextDto ToDto(OrchestrationSessionContext s)
    {
        return new OrchestrationSessionContextDto
        {
            CorrelationId = s.CorrelationId,
            TenantKey = s.TenantKey,
            UserKey = s.UserKey,
            SessionKey = s.SessionKey,
            Locale = s.Locale,
            ActiveOrchestrationId = s.ActiveOrchestrationId,
            Properties = new Dictionary<string, string>(s.Properties, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static OrchestrationSessionContext FromDto(OrchestrationSessionContextDto dto)
    {
        return new OrchestrationSessionContext
        {
            CorrelationId = string.IsNullOrEmpty(dto.CorrelationId) ? Guid.NewGuid().ToString("N") : dto.CorrelationId,
            TenantKey = dto.TenantKey,
            UserKey = dto.UserKey,
            SessionKey = dto.SessionKey,
            Locale = dto.Locale,
            ActiveOrchestrationId = dto.ActiveOrchestrationId,
            Properties = new Dictionary<string, string>(dto.Properties, StringComparer.OrdinalIgnoreCase)
        };
    }
}
