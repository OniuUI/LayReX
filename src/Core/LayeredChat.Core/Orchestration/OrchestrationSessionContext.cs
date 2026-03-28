namespace LayeredChat;

/// <summary>
/// Per-turn session state passed to tools and data sources. Hosts populate product-specific data via <see cref="Properties"/>.
/// </summary>
public sealed class OrchestrationSessionContext
{
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    public string? TenantKey { get; init; }

    public string? UserKey { get; init; }

    public string? SessionKey { get; init; }

    public string Locale { get; init; } = "en";

    public string ActiveOrchestrationId { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
