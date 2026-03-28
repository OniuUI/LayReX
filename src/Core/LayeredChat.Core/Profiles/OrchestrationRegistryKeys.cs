namespace LayeredChat;

/// <summary>
/// Builds stable registry keys for versioned orchestration definitions.
/// </summary>
public static class OrchestrationRegistryKeys
{
    public static string Compose(string orchestrationId, string? semanticVersion)
    {
        if (string.IsNullOrWhiteSpace(orchestrationId))
        {
            throw new ArgumentException("Orchestration id is required.", nameof(orchestrationId));
        }

        return string.IsNullOrWhiteSpace(semanticVersion)
            ? orchestrationId.Trim()
            : $"{orchestrationId.Trim()}@{semanticVersion.Trim()}";
    }
}
