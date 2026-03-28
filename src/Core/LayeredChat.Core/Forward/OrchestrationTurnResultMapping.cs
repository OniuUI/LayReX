namespace LayeredChat;

/// <summary>
/// Maps turn results to and from DTOs for HTTP integration.
/// </summary>
public static class OrchestrationTurnResultMapping
{
    public static LayeredChatTurnResultDto ToDto(LayeredChatTurnResult result)
    {
        return new LayeredChatTurnResultDto
        {
            RegistryKey = result.RegistryKey,
            CorrelationId = result.CorrelationId,
            OrchestrationId = result.OrchestrationId,
            SemanticVersion = result.SemanticVersion,
            AssistantText = result.AssistantText,
            AppendedMessages = result.AppendedMessages.Select(OrchestrationForwardMapping.ToChatMessageDto).ToList(),
            TotalInputTokens = result.TotalInputTokens,
            TotalOutputTokens = result.TotalOutputTokens
        };
    }

    public static LayeredChatTurnResult FromDto(LayeredChatTurnResultDto dto)
    {
        return new LayeredChatTurnResult
        {
            RegistryKey = dto.RegistryKey,
            CorrelationId = dto.CorrelationId,
            OrchestrationId = dto.OrchestrationId,
            SemanticVersion = dto.SemanticVersion,
            AssistantText = dto.AssistantText,
            AppendedMessages = dto.AppendedMessages.Select(OrchestrationForwardMapping.FromChatMessageDto).ToList(),
            TotalInputTokens = dto.TotalInputTokens,
            TotalOutputTokens = dto.TotalOutputTokens
        };
    }
}
